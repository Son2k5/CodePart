using System.Security.Claims;
using CodePath.Application.Users;
using CodePath.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodePath.Api.Controllers;

[ApiController]
[Authorize(Policy = "ActiveUser")]
[Route("api/users/me")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAppException("Authentication required.");

    [HttpGet]
    public Task<UserProfileResponse> Get(CancellationToken ct) => users.GetMyProfile(Actor, ct);

    [HttpGet("options")]
    public Task<ProfileOptionsResponse> Options(CancellationToken ct) => users.GetProfileOptions(ct);

    [HttpPut]
    public Task<UserProfileResponse> Update(UpdateProfileRequest request, CancellationToken ct) => users.UpdateMyProfile(Actor, request, ct);

    [HttpPut("password")]
    [EnableRateLimiting("users-password")]
    public async Task<IActionResult> Password(ChangePasswordRequest request, CancellationToken ct)
    {
        await users.ChangePassword(Actor, request, ct);
        return NoContent();
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(2 * 1024 * 1024 + 16384)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024 + 16384)]
    public async Task<ActionResult<UserProfileResponse>> Avatar([FromForm] IFormFile file, [FromForm] int version, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 2097152)
            return Problem(statusCode: 400, title: "Avatar must be at most 2 MB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return await users.UploadAvatar(Actor, buffer.ToArray(), file.FileName, version, ct);
    }
}

[ApiController]
[Authorize(Policy = "ActiveUser", Roles = "Admin")]
[Route("api/admin/users")]
public sealed class AdminUsersController(IUserService users) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<UserProfileResponse> Get(Guid id, CancellationToken ct) => users.AdminGetUser(id, ct);

    [HttpPut("{id:guid}")]
    public Task<UserProfileResponse> Update(Guid id, AdminUpdateUserRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor))
            throw new UnauthorizedAppException("Authentication required.");
        return users.AdminUpdateUser(actor, id, request, ct);
    }
}
