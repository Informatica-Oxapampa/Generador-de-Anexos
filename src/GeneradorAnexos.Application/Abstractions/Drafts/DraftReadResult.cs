namespace GeneradorAnexos.Application.Abstractions.Drafts;

/// <summary>A validated draft and whether it still requires encrypted promotion.</summary>
public sealed record DraftReadResult(string Json, bool WasLegacyPlaintext);
