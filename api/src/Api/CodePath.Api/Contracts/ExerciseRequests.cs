namespace CodePath.Api.Contracts;

public sealed record RunCodeRequest(string Language, string SourceCode, string? CustomInput);
public sealed record SubmitCodeRequest(string Language, string SourceCode);
