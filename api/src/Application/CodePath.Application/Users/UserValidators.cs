using FluentValidation;

namespace CodePath.Application.Users;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.FullName).NotEmpty().Must(x => x is not null && x.Trim().Length is >= 2 and <= 100);
        RuleFor(x => x.PhoneNumber).Matches(@"^(0|\+84)\d{9}$").When(x => x.PhoneNumber is not null);
        RuleFor(x => x.DateOfBirth).Must(x => x is null || x <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        RuleFor(x => x.Gender).Must(x => x is null or "Male" or "Female" or "Other");
        RuleFor(x => x.AvatarUrl).MaximumLength(2048).Must(x => x is null || (Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https"));
        RuleFor(x => x.AcademicTitle).MaximumLength(100);
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.AdministrativeClassId).Must(x => x != Guid.Empty);
        RuleFor(x => x.FacultyId).Must(x => x != Guid.Empty);
    }
}
public sealed class AdminUpdateUserRequestValidator : AbstractValidator<AdminUpdateUserRequest>
{
    public AdminUpdateUserRequestValidator(TimeProvider clock)
    {
        Include(new UpdateProfileRequestValidator(clock));
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.OldPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Must(x => x is not null && System.Text.Encoding.UTF8.GetByteCount(x) <= 72)
            .WithMessage("Password must not exceed bcrypt's 72-byte limit.")
            .Matches("[A-Z]").Matches("[0-9]").NotEqual(x => x.OldPassword).WithMessage("New password must differ from the old password.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Password confirmation does not match.");
    }
}
