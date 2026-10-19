using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Learning;
namespace CodePath.Infrastructure.Learning;

public sealed class RagIndexException(string code) : Exception(code);
public sealed class PythonRagClient(HttpClient http, IHttpClientFactory factory) : IRagClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<int> Index(RagTopic topic, object[] documents, CancellationToken ct)
    {
        using var indexer = factory.CreateClient("rag-indexer");
        using var response = await indexer.PostAsJsonAsync("internal/rag/index/topic", new {
            topicId = topic.Id, topic.ClassId, topic.Revision, topic.Version, documents }, ct);
        if ((int)response.StatusCode == 422)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var code = body.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : "INDEX_INVALID";
            throw new RagIndexException(code ?? "INDEX_INVALID");
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("chunks").GetInt32();
    }
    public async Task Delete(RagScope scope, CancellationToken ct)
    {
        using var indexer = factory.CreateClient("rag-indexer");
        using var r = await indexer.PostAsJsonAsync("internal/rag/index/delete", scope, ct); r.EnsureSuccessStatusCode();
    }
    public async Task<RagSearchResult> Search(string query, RagScope[] scopes, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(6));
        using var r = await http.PostAsJsonAsync("internal/rag/search", new { query, scopes, limit = 5 }, deadline.Token);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<RagSearchResult>(Json, deadline.Token) ?? throw new JsonException();
    }
    public async Task<RagAnswer> Answer(string question, RagPassage[] passages, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var r = await http.PostAsJsonAsync("internal/rag/answer", new { question, passages }, deadline.Token);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<RagAnswer>(Json, deadline.Token) ?? throw new JsonException();
    }
    public async Task<TeachingDraftContent> Draft(string question, string kind, string language, RagPassage[] passages, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(50));
        using var r = await http.PostAsJsonAsync("internal/rag/draft", new { question, kind, language, passages }, deadline.Token);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<TeachingDraftContent>(Json, deadline.Token) ?? throw new JsonException();
    }
}
