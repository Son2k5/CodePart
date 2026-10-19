using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Domain.Users.Entities;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ClassStatus>))]
public enum ClassStatus { Draft, Active, Archived }
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ClassAccessType>))]
public enum ClassAccessType { Public, CodeRequired }
public enum ClassJoinMethod { Public, Code, AdminAdded }
public enum ClassEnrollmentStatus { Active, Removed }

public sealed partial class Class
{
    public string Description { get; private set; } = "";
    public string? Thumbnail { get; private set; }
    public ClassStatus Status { get; private set; }
    public ClassAccessType AccessType { get; private set; }
    public string? JoinCode { get; private set; }
    public DateTime? JoinCodeExpiresAt { get; private set; }
    public int? MaxStudents { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    // Application-managed concurrency token: MySQL has no SQL Server rowversion.
    public Guid RowVersion { get; private set; } = Guid.NewGuid();
    public ICollection<ClassTeacher> Teachers { get; private set; } = new List<ClassTeacher>();
    public ICollection<ClassEnrollment> ClassEnrollments { get; private set; } = new List<ClassEnrollment>();

    public static Class CreateManaged(string name, Guid actor, DateTime now)
    {
        var c = new Class { Name = name.Trim(), AcademicYear = "" };
        c.SetCreatedAudit(now, actor.ToString());
        return c;
    }
    public void Configure(string name, string description, string? thumbnail, ClassAccessType access,
        int? maxStudents, DateTime? start, DateTime? end, string? newCode, DateTime? expiry, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 3 or > 100 || maxStudents <= 0
            || (start.HasValue && end.HasValue && end <= start) || !Enum.IsDefined(access))
            throw new DomainRuleViolationException("Thông tin lớp không hợp lệ.", "VALIDATION_ERROR");
        if (IsTemplate && (maxStudents != null || start != null || end != null || access != ClassAccessType.Public))
            throw new DomainRuleViolationException("Templates cannot have enrollment settings.", "CLASS_IS_TEMPLATE");
        Name = name.Trim(); Description = description; Thumbnail = thumbnail;
        MaxStudents = maxStudents; StartDate = start; EndDate = end;
        if (access == ClassAccessType.Public) { JoinCode = null; JoinCodeExpiresAt = null; }
        else if (AccessType != access || newCode != null) { JoinCode = newCode; JoinCodeExpiresAt = expiry; }
        else JoinCodeExpiresAt = JoinCode == null ? null : expiry;
        AccessType = access;
        Changed(now);
    }
    public void SetStatus(ClassStatus status, DateTime now)
    {
        if (!Enum.IsDefined(status)) throw new DomainRuleViolationException("Trạng thái không hợp lệ.");
        Status = status; Changed(now);
    }
    public void SetJoinCode(string? code, DateTime? expiry, DateTime now)
    {
        if (IsTemplate) throw new DomainRuleViolationException("Templates cannot have join codes.", "CLASS_IS_TEMPLATE");
        if (AccessType != ClassAccessType.CodeRequired)
            throw new DomainRuleViolationException("Lớp Public không sử dụng mã.", "CLASS_ACCESS_MISMATCH");
        JoinCode = code; JoinCodeExpiresAt = code == null ? null : expiry; Changed(now);
    }
    public void EnsureCanJoin(bool byCode, int count, bool alreadyEnrolled, DateTime now)
    {
        if (IsDeleted) throw new DomainRuleViolationException("Không tìm thấy lớp.", "CLASS_NOT_FOUND");
        if (IsTemplate) throw new DomainRuleViolationException("Templates cannot enroll students.", "CLASS_IS_TEMPLATE");
        if (Status != ClassStatus.Active) throw new DomainRuleViolationException("Lớp chưa mở đăng ký.", "CLASS_NOT_ACTIVE");
        if (byCode != (AccessType == ClassAccessType.CodeRequired))
            throw new DomainRuleViolationException("Phương thức tham gia không phù hợp.", "INVALID_JOIN_CODE");
        if (byCode && JoinCode == null) throw new DomainRuleViolationException("Mã lớp không hợp lệ.", "INVALID_JOIN_CODE");
        if (byCode && JoinCodeExpiresAt <= now) throw new DomainRuleViolationException("Mã lớp đã hết hạn.", "JOIN_CODE_EXPIRED");
        if (alreadyEnrolled) throw new DomainRuleViolationException("Bạn đã tham gia lớp.", "ALREADY_ENROLLED");
        if (MaxStudents.HasValue && count >= MaxStudents) throw new DomainRuleViolationException("Lớp đã đủ học sinh.", "CLASS_FULL");
    }
    public void Delete(int count, bool force, bool confirmed, DateTime now)
    {
        if (count > 0 && !(force && confirmed))
            throw new DomainRuleViolationException("Lớp còn học sinh. Cần force=true và confirm=true để xóa.", "CLASS_HAS_STUDENTS");
        IsDeleted = true; DeletedAt = now; JoinCode = null; JoinCodeExpiresAt = null; Changed(now);
    }
    public static void EnsureTeacher(UserRole? role)
    {
        if (role != UserRole.Teacher) throw new DomainRuleViolationException("Chỉ được gán người dùng có vai trò Teacher.", "TEACHER_ROLE_REQUIRED");
    }
    public void Changed(DateTime now) { RowVersion = Guid.NewGuid(); Touch(now); }
}

public sealed class ClassTeacher
{
    public Guid ClassId { get; private set; }
    public Class Class { get; private set; } = default!;
    public Guid TeacherId { get; private set; }
    public User Teacher { get; private set; } = default!;
    public DateTime AssignedAt { get; private set; }
    public Guid AssignedBy { get; private set; }
    private ClassTeacher() { }
    public static ClassTeacher Create(Guid classId, Guid teacherId, Guid actor, DateTime now) =>
        new() { ClassId = classId, TeacherId = teacherId, AssignedAt = now, AssignedBy = actor };
}

public sealed class ClassEnrollment
{
    public Guid ClassId { get; private set; }
    public Class Class { get; private set; } = default!;
    public Guid StudentId { get; private set; }
    public User Student { get; private set; } = default!;
    public DateTime JoinedAt { get; private set; }
    public ClassJoinMethod JoinMethod { get; private set; }
    public ClassEnrollmentStatus Status { get; private set; }
    private ClassEnrollment() { }
    public static ClassEnrollment Create(Guid classId, Guid student, ClassJoinMethod method, DateTime now) =>
        new() { ClassId = classId, StudentId = student, JoinMethod = method, JoinedAt = now };
    public void Rejoin(ClassJoinMethod method, DateTime now) { Status = ClassEnrollmentStatus.Active; JoinedAt = now; JoinMethod = method; }
    public void Remove() => Status = ClassEnrollmentStatus.Removed;
}
