namespace CodePath.Domain.Ai;

public sealed class AiInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ProblemId { get; set; }
    public string Language { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Level { get; set; }
    public string Stage { get; set; } = "";
    public string Response { get; set; } = "";
    public string GuardStatus { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public bool? Helpful { get; set; }
}
