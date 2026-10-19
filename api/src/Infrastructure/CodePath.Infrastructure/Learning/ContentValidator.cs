using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodePath.Application.Judge;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Learning;
using CodePath.Infrastructure.Persistence;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace CodePath.Infrastructure.Learning;
public sealed class ContentValidator(AppDbContext db, IConfiguration config)
{
    private static HtmlSanitizer Sanitizer()
    {
        var s = new HtmlSanitizer(); s.AllowedTags.Clear(); s.AllowedAttributes.Clear(); s.AllowedCssProperties.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "em", "b", "i", "ul", "ol", "li", "blockquote", "code", "pre", "h1", "h2", "h3", "h4", "table", "thead", "tbody", "tr", "th", "td", "a" }) s.AllowedTags.Add(tag);
        s.AllowedAttributes.Add("href"); s.AllowedAttributes.Add("title"); return s;
    }
    public static string Plain(string text) => System.Net.WebUtility.HtmlEncode(text);
    public static string Markdown(string text)
    {
        // Protect fenced code before HTML parsing; unpredictable placeholders cannot be injected by authors.
        var code = new Dictionary<string, string>(); var prefix = "CODE" + Guid.NewGuid().ToString("N");
        var protectedText = Regex.Replace(text, @"(?m)^ {0,3}(`{3,}|~{3,})[^\r\n]*\r?\n[\s\S]*?^ {0,3}\1[ \t]*(?:\r?\n|$)", m => { var key = prefix + code.Count; code[key] = m.Value; return key; }, RegexOptions.None, TimeSpan.FromSeconds(1));
        var result = Sanitizer().Sanitize(protectedText);
        foreach (var item in code) result = result.Replace(item.Key, item.Value, StringComparison.Ordinal);
        return result;
    }
    public async Task<string> Validate(ContentBlock[] blocks, Actor actor, Topic topic, CancellationToken ct)
    {
        void Invalid(string message, string code = "BLOCK_INVALID") => throw new DomainRuleViolationException(message, code);
        if (blocks == null || blocks.Length > 300 || blocks.Any(b => b == null || b.BlockId == Guid.Empty) || blocks.Select(b => b.BlockId).Distinct().Count() != blocks.Length) Invalid("Invalid or duplicate block IDs.", "CONTENT_INVALID");
        var old = JsonSerializer.Deserialize<ContentBlock[]>(topic.DraftContent, LearningService.Json)!.ToDictionary(b => b.BlockId);
        var result = new List<ContentBlock>();
        foreach (var b in blocks!)
        {
            if (old.TryGetValue(b.BlockId, out var previous) && previous.Type != b.Type) Invalid("Existing block type cannot change; use a new block ID.");
            try
            {
                var d = b.Data; object data;
                switch (b.Type)
                {
                    case "Markdown":
                        var text = d.GetProperty("markdown").GetString()!; if (text.Length > 200000) Invalid("Markdown too long.");
                        data = new { markdown = Markdown(text) }; break;
                    case "Video":
                        var provider = d.GetProperty("provider").GetString(); var duration = d.GetProperty("durationSec").GetDouble();
                        if (!double.IsFinite(duration) || duration <= 0 || duration > 86400) Invalid("Invalid duration.");
                        var url = d.GetProperty("url").GetString();
                        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || !uri.IsDefaultPort) Invalid("HTTPS video URL required.");
                        var hosts = config.GetSection("Learning:Video:AllowedHosts").Get<string[]>() ?? ["www.youtube.com", "youtube.com", "youtu.be", "vimeo.com", "player.vimeo.com", "res.cloudinary.com"];
                        if (!hosts.Contains(uri!.Host, StringComparer.OrdinalIgnoreCase)) Invalid("Video host is not allowed.", "VIDEO_HOST_NOT_ALLOWED");
                        var parts = uri.AbsolutePath.Trim('/').Split('/');
                        if (provider == "YouTube" && uri.Host is "www.youtube.com" or "youtube.com" or "youtu.be")
                        {
                            var videoId = uri.Host == "youtu.be" ? parts[0] : parts[0] is "embed" or "shorts" && parts.Length == 2 ? parts[1] : Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query).GetValueOrDefault("v").ToString();
                            if (!Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$")) Invalid("Invalid YouTube ID.");
                            url = "https://www.youtube.com/embed/" + videoId;
                        }
                        else if (provider == "Vimeo" && uri.Host is "vimeo.com" or "player.vimeo.com")
                        { var videoId = parts[^1]; if (!Regex.IsMatch(videoId, "^[0-9]+$")) Invalid("Invalid Vimeo ID."); url = "https://player.vimeo.com/video/" + videoId; }
                        else if (provider != "Cloudinary" || uri.Host != "res.cloudinary.com") Invalid("Provider and host do not match.");
                        data = new { provider, url, durationSec = duration, title = d.TryGetProperty("title", out var title) ? Plain(title.GetString() ?? "") : null }; break;
                    case "Pdf": case "Image":
                        var assetId = d.GetProperty("assetId").GetGuid();
                        var asset = await db.LearningAssets.SingleOrDefaultAsync(x => x.Id == assetId, ct);
                        if (asset == null || asset.Status != AssetStatus.Ready) Invalid("Asset not ready.", "ASSET_NOT_READY");
                        var referenced = old.Values.Any(x => x.Data.TryGetProperty("assetId", out var v) && v.TryGetGuid(out var g) && g == assetId);
                        if (!actor.IsAdmin && asset!.OwnerId != actor.Id && !referenced) Invalid("Asset access denied.");
                        if ((b.Type == "Pdf") != (asset!.Kind == AssetKind.Pdf)) Invalid("Asset type mismatch.");
                        data = b.Type == "Pdf" ? new { assetId, title = Plain(d.GetProperty("title").GetString() ?? ""), pageCount = asset.PageCount ?? d.GetProperty("pageCount").GetInt32() } :
                            (object)new { assetId, alt = Plain(d.GetProperty("alt").GetString() ?? ""), caption = d.TryGetProperty("caption", out var cap) ? Plain(cap.GetString() ?? "") : null };
                        if (b.Type == "Pdf" && JsonSerializer.SerializeToElement(data).GetProperty("pageCount").GetInt32() <= 0) Invalid("Invalid PDF page count.");
                        break;
                    default: Invalid("Unsupported block type."); return "[]";
                }
                result.Add(new(b.BlockId, b.Type, JsonSerializer.SerializeToElement(data)));
            }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or NullReferenceException) { Invalid("Missing or invalid block data."); }
        }
        var json = JsonSerializer.Serialize(result, LearningService.Json);
        if (Encoding.UTF8.GetByteCount(json) > 2_000_000) Invalid("Content too large.", "CONTENT_INVALID");
        return json;
    }
    public static string SearchText(Topic topic) => topic.Title + " " + topic.Summary + " " + string.Join(" ",
        JsonSerializer.Deserialize<ContentBlock[]>(topic.PublishedContent, LearningService.Json)!.Where(b => b.Type == "Markdown")
            .Select(b => Regex.Replace(b.Data.GetProperty("markdown").GetString() ?? "", "<[^>]*>", " ", RegexOptions.None, TimeSpan.FromSeconds(1))));
}
