using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GeneradorAnexos.Application.Abstractions.Security;
using GeneradorAnexos.Infrastructure.Windows.Diagnostics;

namespace GeneradorAnexos.Infrastructure.Windows.Security;

/// <summary>
/// Implements the Python application's exact Windows envelope:
/// <c>enc1:</c> + Base64(CryptProtectData(UTF8, CurrentUser, no entropy, flags 0)).
/// </summary>
public sealed class DpapiDataProtectionService : IDataProtectionService
{
    public const string EnvelopePrefix = "enc1:";

    private const int ErrorInvalidData = 13;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly ISecurityEventSink _events;

    public DpapiDataProtectionService(ISecurityEventSink? events = null)
    {
        _events = events ?? NullSecurityEventSink.Instance;
    }

    public bool IsProtected(string? value) =>
        value?.StartsWith(EnvelopePrefix, StringComparison.Ordinal) is true;

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        EnsureWindows(DataProtectionFailure.ProtectionFailed);

        byte[]? plaintextBytes = null;
        byte[]? protectedBytes = null;
        try
        {
            plaintextBytes = StrictUtf8.GetBytes(plaintext);
            protectedBytes = Transform(plaintextBytes, protect: true);
            return string.Concat(EnvelopePrefix, Convert.ToBase64String(protectedBytes));
        }
        catch (ExternalException)
        {
            _events.Write(SecurityEventId.DataProtectionProtectFailed);
            throw new DataProtectionException(DataProtectionFailure.ProtectionFailed);
        }
        catch (EncoderFallbackException)
        {
            _events.Write(SecurityEventId.DataProtectionProtectFailed);
            throw new DataProtectionException(DataProtectionFailure.ProtectionFailed);
        }
        finally
        {
            Zero(plaintextBytes);
            Zero(protectedBytes);
        }
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);
        EnsureWindows(DataProtectionFailure.UnprotectionFailed);

        if (!IsProtected(protectedValue))
        {
            _events.Write(SecurityEventId.DataProtectionEnvelopeRejected);
            throw new DataProtectionException(DataProtectionFailure.InvalidEnvelope);
        }

        var base64 = protectedValue[EnvelopePrefix.Length..];
        byte[]? protectedBytes = null;
        byte[]? plaintextBytes = null;
        try
        {
            protectedBytes = Convert.FromBase64String(base64);
            if (protectedBytes.Length == 0 ||
                !string.Equals(
                    Convert.ToBase64String(protectedBytes),
                    base64,
                    StringComparison.Ordinal))
            {
                throw new FormatException();
            }

            plaintextBytes = Transform(protectedBytes, protect: false);
            return StrictUtf8.GetString(plaintextBytes);
        }
        catch (FormatException)
        {
            _events.Write(SecurityEventId.DataProtectionEnvelopeRejected);
            throw new DataProtectionException(DataProtectionFailure.InvalidEnvelope);
        }
        catch (ExternalException)
        {
            _events.Write(SecurityEventId.DataProtectionUnprotectFailed);
            throw new DataProtectionException(DataProtectionFailure.UnprotectionFailed);
        }
        catch (DecoderFallbackException)
        {
            _events.Write(SecurityEventId.DataProtectionUnprotectFailed);
            throw new DataProtectionException(DataProtectionFailure.UnprotectionFailed);
        }
        finally
        {
            Zero(protectedBytes);
            Zero(plaintextBytes);
        }
    }

    private void EnsureWindows(DataProtectionFailure operationFailure)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _events.Write(operationFailure == DataProtectionFailure.ProtectionFailed
            ? SecurityEventId.DataProtectionProtectFailed
            : SecurityEventId.DataProtectionUnprotectFailed);
        throw new DataProtectionException(DataProtectionFailure.UnsupportedPlatform);
    }

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inputPointer = IntPtr.Zero;
        var output = default(NativeMethods.DataBlob);
        try
        {
            inputPointer = Marshal.AllocHGlobal(Math.Max(input.Length, 1));
            if (input.Length > 0)
            {
                Marshal.Copy(input, 0, inputPointer, input.Length);
            }

            var inputBlob = new NativeMethods.DataBlob(input.Length, inputPointer);
            var succeeded = protect
                ? NativeMethods.CryptProtectData(
                    ref inputBlob,
                    null,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    out output)
                : NativeMethods.CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    out output);

            if (!succeeded)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (output.Size < 0 || (output.Size > 0 && output.Data == IntPtr.Zero))
            {
                throw new Win32Exception(
                    ErrorInvalidData,
                    "DPAPI returned an invalid output buffer.");
            }

            var result = GC.AllocateUninitializedArray<byte>(output.Size);
            if (output.Size > 0)
            {
                Marshal.Copy(output.Data, result, 0, output.Size);
            }

            return result;
        }
        finally
        {
            SecureZero(inputPointer, input.Length);
            if (inputPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inputPointer);
            }

            SecureZero(output.Data, output.Size);
            if (output.Data != IntPtr.Zero)
            {
                _ = NativeMethods.LocalFree(output.Data);
            }
        }
    }

    private static void SecureZero(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero || length <= 0)
        {
            return;
        }

        // RtlSecureZeroMemory no es un punto de entrada exportado de forma
        // consistente por kernel32.dll. Limpiar por bloques administrados
        // evita EntryPointNotFoundException en Windows 10/11 y conserva el
        // borrado de los buffers antes de liberarlos.
        var ceros = new byte[Math.Min(length, 4096)];
        try
        {
            var desplazamiento = 0;
            while (desplazamiento < length)
            {
                var cantidad = Math.Min(ceros.Length, length - desplazamiento);
                Marshal.Copy(ceros, 0, IntPtr.Add(pointer, desplazamiento), cantidad);
                desplazamiento += cantidad;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ceros);
        }
    }

    private static void Zero(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct DataBlob
        {
            internal DataBlob(int size, IntPtr data)
            {
                Size = size;
                Data = data;
            }

            internal int Size { get; }

            internal IntPtr Data { get; }
        }

#pragma warning disable SYSLIB1054 // Exact legacy Win32 signatures; project cannot require unsafe generation.
        [DllImport(
            "crypt32.dll",
            EntryPoint = "CryptProtectData",
            ExactSpelling = true,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            uint flags,
            out DataBlob dataOut);

        [DllImport(
            "crypt32.dll",
            EntryPoint = "CryptUnprotectData",
            ExactSpelling = true,
            SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            uint flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll", EntryPoint = "LocalFree", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern IntPtr LocalFree(IntPtr memory);

#pragma warning restore SYSLIB1054
    }
}
