using System.Security.Cryptography;

namespace CodePath.Application.Classes;

public class JoinCodeGenerator
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    protected virtual string Candidate() => new(Enumerable.Range(0, 8)
        .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
    public async Task<string> GenerateAsync(Func<string, CancellationToken, Task<bool>> exists, CancellationToken ct)
    {
        for (var i = 0; i < 32; i++)
        {
            ct.ThrowIfCancellationRequested();
            var code = Candidate();
            if (!await exists(code, ct)) return code;
        }
        throw new InvalidOperationException("Unable to allocate a unique class code.");
    }
}
