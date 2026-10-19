using CodePath.Domain.Common.Exceptions;

namespace CodePath.Domain.Users.Entities;

public sealed partial class Class
{
    public static Class CreateTemplate(string name, string description, Guid ownerId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name) || ownerId == Guid.Empty) throw new DomainRuleViolationException("Template owner is required.", "CONTENT_INVALID");
        var c = CreateManaged(name, ownerId, now);
        c.ConfigureTemplate(name, description, null, null, now);
        c.IsTemplate = true;
        return c;
    }

    public void ConfigureTemplate(string name, string description, string? thumbnail, Guid? facultyId, DateTime now)
    {
        if (description == null || description.Length > 5000 || facultyId == Guid.Empty ||
            thumbnail != null && (thumbnail.Length > 2048 || !Uri.TryCreate(thumbnail, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new DomainRuleViolationException("Invalid template metadata.", "CONTENT_INVALID");
        Configure(name, description, thumbnail, ClassAccessType.Public, null, null, null, null, null, now);
        FacultyId = facultyId;
    }
}
