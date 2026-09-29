using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using Xunit;

namespace CodePath.Architecture.Tests;

public class ResultPatternTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResultWithValue()
    {
        // Act
        var result = Result<string>.Success("ok_value");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok_value");
        result.Error.Should().BeNull();
        result.ErrorCode.Should().BeNull();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResultWithSpecifiedErrorCode()
    {
        // Act
        var result = Result<string>.Failure("Unauthorized access", ErrorCodes.Unauthorized);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be("Unauthorized access");
        result.ErrorCode.Should().Be(ErrorCodes.Unauthorized);
    }

    [Fact]
    public void ValidationFailure_ShouldIncludeDetailedValidationErrors()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new("Email", "Email không đúng định dạng."),
            new("Password", "Mật khẩu tối thiểu 8 ký tự.")
        };

        // Act
        var result = Result<string>.ValidationFailure(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationError);
        result.ValidationErrors.Should().HaveCount(2);
        result.ValidationErrors[0].PropertyName.Should().Be("Email");
        result.ValidationErrors[1].PropertyName.Should().Be("Password");
    }
}
