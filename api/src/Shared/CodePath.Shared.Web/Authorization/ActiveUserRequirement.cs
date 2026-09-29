using CodePath.Shared.Kernel.Enums;
using Microsoft.AspNetCore.Authorization;

namespace CodePath.Shared.Web.Authorization;

/// <summary>
/// Requirement dùng chung cho policy ActiveUser/ActiveStudent/ActiveTeacher/AdminOnly.
/// Định nghĩa ở Shared.Web để Api không phụ thuộc type từ Infrastructure.
/// Handler implement ở Infrastructure.
/// </summary>
public sealed class ActiveUserRequirement : IAuthorizationRequirement
{
    public UserRole? RequiredRole { get; }
    public ActiveUserRequirement(UserRole? requiredRole = null) => RequiredRole = requiredRole;
}
