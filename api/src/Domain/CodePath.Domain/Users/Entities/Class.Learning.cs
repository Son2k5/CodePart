using CodePath.Domain.Common.Exceptions;
namespace CodePath.Domain.Users.Entities;
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ClassMode>))]
public enum ClassMode { Managed, SelfStudy }
public sealed partial class Class
{
    public ClassMode Mode { get; private set; } = ClassMode.Managed;
    public bool IsTemplate { get; private set; }
    public void SetMode(ClassMode mode, DateTime now)
    {
        if (!Enum.IsDefined(mode)) throw new DomainRuleViolationException("Invalid mode.", "VALIDATION_ERROR");
        if (mode == ClassMode.SelfStudy && Teachers.Count != 0)
            throw new DomainRuleViolationException("Remove teachers before selecting SelfStudy.", "TEACHER_NOT_ALLOWED_IN_SELFSTUDY");
        Mode = mode; Changed(now);
    }
    public void EnsureTeacherAssignmentAllowed()
    {
        if (Mode == ClassMode.SelfStudy) throw new DomainRuleViolationException("SelfStudy classes cannot have teachers.", "TEACHER_NOT_ALLOWED_IN_SELFSTUDY");
    }
}
