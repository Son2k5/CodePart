using CodePath.Api.Extensions;
using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CodePath.Architecture.Tests;

public class ResultExtensionsTests
{
    [Fact]
    public void ToHttpResult_WhenSuccessString_ShouldReturnOkWithMessageObject()
    {
        // Arrange
        var result = Result<string>.Success("Thành công");

        // Act
        var httpResult = result.ToHttpResult();

        // Assert
        httpResult.Should().BeAssignableTo<IValueHttpResult>();
        var valueResult = (IValueHttpResult)httpResult;
        valueResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void ToHttpResult_WhenNotFound_ShouldReturnProblem404()
    {
        // Arrange
        var result = Result<string>.Failure("Không tìm thấy user", ErrorCodes.NotFound);

        // Act
        var httpResult = result.ToHttpResult();

        // Assert
        httpResult.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)httpResult;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Extensions["code"].Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public void ToHttpResult_WhenValidationFailure_ShouldReturnValidationProblem400()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new("Email", "Email sai định dạng.")
        };
        var result = Result<string>.ValidationFailure(errors);

        // Act
        var httpResult = result.ToHttpResult();

        // Assert
        httpResult.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)httpResult;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Should().BeOfType<HttpValidationProblemDetails>();
        var validationProblem = (HttpValidationProblemDetails)problem.ProblemDetails;
        validationProblem.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public void ToHttpResult_WhenConflict_ShouldReturnProblem409()
    {
        // Arrange
        var result = Result<string>.Failure("Email đã tồn tại", ErrorCodes.Conflict);

        // Act
        var httpResult = result.ToHttpResult();

        // Assert
        httpResult.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)httpResult;
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Extensions["code"].Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public void ToHttpResult_ShouldAlwaysIncludeTraceId()
    {
        // Arrange
        var result = Result<string>.Failure("Thao tác thất bại", ErrorCodes.BadRequest);

        // Act
        var httpResult = result.ToHttpResult();

        // Assert
        httpResult.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)httpResult;
        problem.ProblemDetails.Extensions.Should().ContainKey("traceId");
        problem.ProblemDetails.Extensions["traceId"].Should().NotBeNull();
    }

    [Fact]
    public void ToHttpResult_WhenRawMessageContainsSqlOrInternalDetails_ShouldSanitizeOutput()
    {
        // Arrange - simulating an internal error message with SQL / file path details leaking into Result
        var leakyError = "Npgsql.PostgresException: 23505: duplicate key value violates unique constraint 'users_email_key' at /app/data.cs:42";
        var result = Result<string>.Failure(leakyError, ErrorCodes.InternalServerError);

        // Act
        var httpResult = result.ToHttpResult();

        // Assert
        httpResult.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)httpResult;
        var detail = problem.ProblemDetails.Detail;

        detail.Should().NotContain("Npgsql");
        detail.Should().NotContain("23505");
        detail.Should().NotContain("unique constraint");
        detail.Should().NotContain("/app/");
    }
}
