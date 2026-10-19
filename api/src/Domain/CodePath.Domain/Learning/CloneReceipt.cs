namespace CodePath.Domain.Learning;
public sealed class CloneReceipt
{
    public Guid TargetClassId { get; set; }
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Result { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
}
