using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodePath.Application.Users;
using Microsoft.Extensions.Configuration;

namespace CodePath.Infrastructure.Users;

public sealed class CloudinaryAvatarStorage(HttpClient client, IConfiguration configuration) : IAvatarStorage
{
    public async Task<string> UploadAsync(Guid userId, byte[] content, string extension, CancellationToken ct)
    {
        var cloud = configuration["Cloudinary:CloudName"];
        var key = configuration["Cloudinary:ApiKey"];
        var secret = configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloud) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Cloudinary is not configured.");
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(content), "file", "avatar" + extension);
        body.Add(new StringContent($"codepath/avatars/{userId}/{Guid.NewGuid():N}"), "public_id");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(cloud)}/image/upload") { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":" + secret)));
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Avatar storage is unavailable.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var url = json.RootElement.GetProperty("secure_url").GetString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new HttpRequestException("Avatar storage returned an invalid URL.");
        return url!;
    }
}
