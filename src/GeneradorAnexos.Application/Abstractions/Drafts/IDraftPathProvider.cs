namespace GeneradorAnexos.Application.Abstractions.Drafts;

/// <summary>Supplies the application-owned autosave path.</summary>
public interface IDraftPathProvider
{
    string AutosavePath { get; }
}
