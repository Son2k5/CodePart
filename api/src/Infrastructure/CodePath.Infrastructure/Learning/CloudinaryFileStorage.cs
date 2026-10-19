using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using Microsoft.Extensions.Configuration;
namespace CodePath.Infrastructure.Learning;
public sealed class CloudinaryFileStorage(HttpClient http, IConfiguration config, TimeProvider clock) : IFileStorage
{
    private string Setting(string key) => config["Learning:Cloudinary:" + key] is { Length: > 0 } value ? value : throw new InvalidOperationException("Configure Learning:Cloudinary:" + key);
    private string Endpoint(string action) => "https://api.cloudinary.com/v1_1/" + Uri.EscapeDataString(Setting("CloudName")) + "/image/" + action;
    private Dictionary<string, string> Sign(Dictionary<string, string> fields)
    {
        var payload = string.Join("&", fields.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value)) + Setting("ApiSecret");
        fields["signature"] = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        fields["api_key"] = Setting("ApiKey"); return fields;
    }
    private string Timestamp => clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Format(Asset asset) => asset.MimeType switch { "application/pdf" => "pdf", "image/png" => "png", "image/jpeg" => "jpg", "image/webp" => "webp", _ => throw new InvalidDataException("Unsupported format.") };
    public Task<object> SignUpload(Asset asset, CancellationToken ct) => Task.FromResult<object>(new { url = Endpoint("upload"), parameters = Sign(new() {
        ["timestamp"] = Timestamp, ["public_id"] = asset.StorageKey, ["type"] = "private", ["overwrite"] = "false", ["allowed_formats"] = Format(asset)
    }) });
    public Task<string> SignedUrl(Asset asset, CancellationToken ct)
    {
        var fields = Sign(new() { ["timestamp"] = Timestamp, ["public_id"] = asset.StorageKey, ["type"] = "private", ["format"] = Format(asset),
            ["expires_at"] = clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ["attachment"] = "false" });
        return Task.FromResult(Endpoint("download") + "?" + string.Join("&", fields.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))));
    }
    public async Task<StoredFile> Inspect(Asset asset, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.cloudinary.com/v1_1/" + Uri.EscapeDataString(Setting("CloudName")) + "/resources/image/private/" + Uri.EscapeDataString(asset.StorageKey));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Setting("ApiKey") + ":" + Setting("ApiSecret"))));
        using var metadata = await http.SendAsync(request, ct); metadata.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync(ct)); var root = json.RootElement;
        var size = root.GetProperty("bytes").GetInt64();
        if (size <= 0 || size > (asset.Kind == AssetKind.Pdf ? 50L : 10L) * 1024 * 1024) throw new InvalidDataException("FILE_TOO_LARGE");
        using var file = await http.GetAsync(await SignedUrl(asset, ct), HttpCompletionOption.ResponseHeadersRead, ct); file.EnsureSuccessStatusCode();
        await using var stream = await file.Content.ReadAsStreamAsync(ct); var header = new byte[16]; var count = await stream.ReadAtLeastAsync(header, 12, false, ct);
        int? Number(string key) => root.TryGetProperty(key, out var value) ? value.GetInt32() : null;
        return new(file.Content.Headers.ContentType?.MediaType ?? "", size, header[..count], Number("pages"), Number("width"), Number("height"));
    }
    public async Task Delete(Asset asset, CancellationToken ct)
    {
        using var response = await http.PostAsync(Endpoint("destroy"), new FormUrlEncodedContent(Sign(new() { ["timestamp"] = Timestamp, ["public_id"] = asset.StorageKey, ["type"] = "private", ["invalidate"] = "true" })), ct);
        response.EnsureSuccessStatusCode();
    }
}
