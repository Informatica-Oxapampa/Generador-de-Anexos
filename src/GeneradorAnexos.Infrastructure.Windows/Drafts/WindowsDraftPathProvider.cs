using System.Runtime.Versioning;
using GeneradorAnexos.Application.Abstractions.Drafts;

namespace GeneradorAnexos.Infrastructure.Windows.Drafts;

/// <summary>Resolves the legacy-compatible per-user autosave location.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDraftPathProvider : IDraftPathProvider
{
    private const string ApplicationDirectoryName = "GeneradorAnexos";
    private const string AutosaveFileName = "autoguardado.json";

    /// <summary>
    /// Creates a provider from an injected application-data directory. Tests
    /// should pass an isolated directory instead of using the current profile.
    /// </summary>
    public WindowsDraftPathProvider(string applicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataDirectory);
        if (!Path.IsPathFullyQualified(applicationDataDirectory))
        {
            throw new ArgumentException(
                "The application data directory must be fully qualified.",
                nameof(applicationDataDirectory));
        }

        var normalizedDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(applicationDataDirectory));
        AutosavePath = Path.Combine(normalizedDirectory, AutosaveFileName);
    }

    public string AutosavePath { get; }

    /// <summary>Creates the production provider without creating any directory.</summary>
    public static WindowsDraftPathProvider CreateForCurrentUser()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Windows did not provide a LocalApplicationData directory.");
        }

        return new WindowsDraftPathProvider(
            Path.Combine(localApplicationData, ApplicationDirectoryName));
    }
}
