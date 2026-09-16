using FluentAssertions;
using Xunit;

namespace CodePath.Architecture.Tests;

/// <summary>
/// Quy uoc kien truc toi thieu. Se bo sung NetArchTest khi can siet module boundary.
/// - Cam Services/*Service.cs trong *.Application (chi dung Handler-cross).
/// - Giao tiep lien module qua ISender.Send(public Query/Command).
/// </summary>
public class ModuleBoundaryTests
{
    [Fact]
    public void ArchitectureRAEADME_ShouldExist()
    {
        File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "ARCHITECTURE.md"))
            .Should().BeTrue();
    }
}
