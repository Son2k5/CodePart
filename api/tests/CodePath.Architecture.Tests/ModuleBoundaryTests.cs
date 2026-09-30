using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace CodePath.Architecture.Tests;

public class ModuleBoundaryTests
{
    [Fact]
    public void Domain_ShouldNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CodePath.Application",
                "CodePath.Infrastructure",
                "CodePath.Api",
                "CodePath.Shared.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(CodePath.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CodePath.Infrastructure",
                "CodePath.Api",
                "CodePath.Shared.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOnApplicationLayers()
    {
        var result = Types.InAssembly(typeof(Result<>).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CodePath.Domain",
                "CodePath.Application",
                "CodePath.Infrastructure",
                "CodePath.Api",
                "CodePath.Shared.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationHandlers_ShouldBeSealed()
    {
        var result = Types.InAssembly(typeof(CodePath.Application.DependencyInjection).Assembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
