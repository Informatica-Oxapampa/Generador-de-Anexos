using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>Valida la firma Authenticode y fija el certificado institucional.</summary>
internal static class VerificadorAuthenticode
{
    private static readonly Guid AccionVerificarV2 =
        new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    public static bool EsConfiable(string ruta)
    {
        if (!OperatingSystem.IsWindows() ||
            !ConfiguracionActualizaciones.FirmaInstitucionalConfigurada ||
            !FirmaValidaParaWindows(ruta))
        {
            return false;
        }

        try
        {
            using var certificadoBase = X509Certificate.CreateFromSignedFile(ruta);
#pragma warning disable SYSLIB0026
            using var certificado = new X509Certificate2(certificadoBase);
#pragma warning restore SYSLIB0026
            var huella = certificado.GetCertHashString(HashAlgorithmName.SHA256);
            return ConfiguracionActualizaciones.FirmantesPermitidosSha256.Any(
                permitida => string.Equals(
                    NormalizarHuella(permitida), huella, StringComparison.OrdinalIgnoreCase));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool FirmaValidaParaWindows(string ruta)
    {
        var rutaPtr = IntPtr.Zero;
        var archivoPtr = IntPtr.Zero;
        var datos = new DatosConfianza
        {
            Tamano = (uint)Marshal.SizeOf<DatosConfianza>(),
            EleccionUi = 2, // WTD_UI_NONE
            Revocacion = 1, // WTD_REVOKE_WHOLECHAIN
            EleccionUnion = 1, // WTD_CHOICE_FILE
            AccionEstado = 1, // WTD_STATEACTION_VERIFY
            OpcionesProveedor = 0x00000080, // WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT
        };

        try
        {
            rutaPtr = Marshal.StringToCoTaskMemUni(ruta);
            var archivo = new InformacionArchivo
            {
                Tamano = (uint)Marshal.SizeOf<InformacionArchivo>(),
                RutaArchivo = rutaPtr,
            };

            archivoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<InformacionArchivo>());
            Marshal.StructureToPtr(archivo, archivoPtr, fDeleteOld: false);
            datos.Archivo = archivoPtr;

            var accion = AccionVerificarV2;
            return WinVerifyTrust(IntPtr.Zero, ref accion, ref datos) == 0;
        }
        finally
        {
            if (datos.Estado != IntPtr.Zero)
            {
                datos.AccionEstado = 2; // WTD_STATEACTION_CLOSE
                var accion = AccionVerificarV2;
                _ = WinVerifyTrust(IntPtr.Zero, ref accion, ref datos);
            }

            if (archivoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(archivoPtr);
            }

            if (rutaPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(rutaPtr);
            }
        }
    }

    private static string NormalizarHuella(string value)
        => value.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InformacionArchivo
    {
        public uint Tamano;
        public IntPtr RutaArchivo;
        public IntPtr Archivo;
        public IntPtr SujetoConocido;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DatosConfianza
    {
        public uint Tamano;
        public IntPtr DatosPolitica;
        public IntPtr DatosSip;
        public uint EleccionUi;
        public uint Revocacion;
        public uint EleccionUnion;
        public IntPtr Archivo;
        public uint AccionEstado;
        public IntPtr Estado;
        public IntPtr ReferenciaUrl;
        public uint OpcionesProveedor;
        public uint ContextoUi;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd,
        [In] ref Guid accion,
        ref DatosConfianza datos);
}
