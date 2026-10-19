using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
namespace CodePath.Infrastructure.Learning;
public sealed class LearningAssets(AppDbContext db, IFileStorage storage, ILearningService learning, TimeProvider clock) : ILearningAssets
{
    public static bool ValidMagic(string mime, byte[] h) => mime switch {
        "application/pdf" => h.AsSpan().StartsWith("%PDF-"u8),
        "image/png" => h.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => h.Length >= 3 && h[0] == 255 && h[1] == 216 && h[2] == 255,
        "image/webp" => h.Length >= 12 && h.AsSpan(0, 4).SequenceEqual("RIFF"u8) && h.AsSpan(8, 4).SequenceEqual("WEBP"u8), _ => false
    };
    public async Task<Result<object>> Sign(UploadRequest r, Actor a, CancellationToken ct)
    {
        if (!a.IsAdmin && !a.IsTeacher) return Result<object>.Failure("Managers only.", "FORBIDDEN");
        if (!Enum.IsDefined(r.Kind) || (r.Kind == AssetKind.Pdf ? r.MimeType != "application/pdf" : r.MimeType is not ("image/png" or "image/jpeg" or "image/webp"))) return Result<object>.Failure("Unsupported MIME.", "CONTENT_INVALID");
        if (r.SizeBytes <= 0 || r.SizeBytes > (r.Kind == AssetKind.Pdf ? 50L : 10L) * 1024 * 1024) return Result<object>.Failure("File too large.", "FILE_TOO_LARGE");
        var asset = new Asset { OwnerId = a.Id, Kind = r.Kind, MimeType = r.MimeType, SizeBytes = r.SizeBytes, CreatedAt = clock.GetUtcNow().UtcDateTime };
        asset.StorageKey = "learning/" + asset.Id.ToString("N"); var upload = await storage.SignUpload(asset, ct);
        db.LearningAssets.Add(asset); await db.SaveChangesAsync(ct); return Result<object>.Success(new { asset.Id, upload });
    }
    public async Task<Result<object>> Complete(Guid id, Actor a, CancellationToken ct)
    {
        var asset = await db.LearningAssets.SingleOrDefaultAsync(x => x.Id == id && (x.OwnerId == a.Id || a.IsAdmin), ct);
        if (asset == null) return Result<object>.Failure("Asset not found.", "NOT_FOUND");
        if (asset.Status == AssetStatus.Ready) return Result<object>.Success(asset);
        StoredFile file;
        try { file = await storage.Inspect(asset, ct); }
        catch (InvalidDataException) { asset.Status = AssetStatus.Failed; await db.SaveChangesAsync(ct); return Result<object>.Failure("File too large.", "FILE_TOO_LARGE"); }
        if (file.MimeType != asset.MimeType || file.SizeBytes != asset.SizeBytes || !ValidMagic(asset.MimeType, file.Header) || asset.Kind == AssetKind.Pdf && file.PageCount is not > 0)
        { asset.Status = AssetStatus.Failed; await db.SaveChangesAsync(ct); return Result<object>.Failure("Uploaded file does not match declared metadata.", "CONTENT_INVALID"); }
        asset.Status = AssetStatus.Ready; asset.PageCount = file.PageCount; asset.Width = file.Width; asset.Height = file.Height;
        await db.SaveChangesAsync(ct); return Result<object>.Success(new { asset.Id, asset.Status, asset.PageCount, asset.Width, asset.Height });
    }
    public async Task<Result<object>> Get(Guid id, Actor a, CancellationToken ct)
    {
        var asset = await db.LearningAssets.SingleOrDefaultAsync(x => x.Id == id && x.Status == AssetStatus.Ready, ct);
        if (asset == null) return Result<object>.Failure("Asset not ready.", "ASSET_NOT_READY");
        var allowed = a.IsAdmin || a.IsTeacher && asset.OwnerId == a.Id;
        if (!allowed && a.IsTeacher)
        {
            var candidates = await db.Topics.Where(t => (t.DraftContent.Contains(id.ToString()) || t.PublishedContent.Contains(id.ToString())) &&
                db.Chapters.Any(ch => ch.Id == t.ChapterId)).ToListAsync(ct);
            foreach (var t in candidates)
            {
                var blocks = JsonSerializer.Deserialize<ContentBlock[]>(t.DraftContent, LearningService.Json)!
                    .Concat(JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, LearningService.Json)!);
                if (!blocks.Any(b => b.Data.TryGetProperty("assetId", out var v) && v.TryGetGuid(out var assetId) && assetId == id)) continue;
                try
                {
                    var result = await learning.Chapters(t.ClassId, null, "list", null, null, a, ct);
                    if (result.IsSuccess) { allowed = true; break; }
                }
                catch (CodePath.Domain.Common.Exceptions.DomainRuleViolationException) { }
            }
        }
        if (!allowed)
        {
            var candidates = await db.Topics.Where(t => t.Status == TopicStatus.Published && t.PublishedContent.Contains(id.ToString()) &&
                db.ClassEnrollments.Any(e => e.ClassId == t.ClassId && e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active)).ToListAsync(ct);
            foreach (var t in candidates)
            {
                var blocks = JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, LearningService.Json)!;
                if (!blocks.Any(b => b.Data.TryGetProperty("assetId", out var value) && value.TryGetGuid(out var aid) && aid == id)) continue;
                try { var result = await learning.Read(t.ClassId, t.Slug, a, ct); if (result.IsSuccess) { allowed = true; break; } }
                catch (CodePath.Domain.Common.Exceptions.DomainRuleViolationException) { }
            }
        }
        if (!allowed) return Result<object>.Failure("Asset access denied.", "FORBIDDEN");
        return Result<object>.Success(new { asset.Id, url = await storage.SignedUrl(asset, ct), expiresAt = clock.GetUtcNow().AddMinutes(5) });
    }
}
