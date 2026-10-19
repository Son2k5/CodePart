using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Learning;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CodePath.Unit.Tests.Learning;

public class CurriculumTests
{
    [Fact]
    public void Template_has_owner_and_no_enrollment_settings()
    {
        var owner = Guid.NewGuid(); var now = DateTime.UtcNow;
        var template = Class.CreateTemplate("C++ curriculum", "Description", owner, now);
        Assert.True(template.IsTemplate); Assert.Equal(owner.ToString(), template.CreatedBy);
        Assert.Null(template.JoinCode); Assert.Null(template.MaxStudents); Assert.Null(template.StartDate); Assert.Null(template.EndDate);
        template.SetStatus(ClassStatus.Active, now);
        Assert.Throws<DomainRuleViolationException>(() => template.EnsureCanJoin(false, 0, false, now));
        Assert.Throws<DomainRuleViolationException>(() => template.SetJoinCode("ABCD1234", null, now));
        Assert.Throws<DomainRuleViolationException>(() => template.Configure("C++ curriculum", "", null, ClassAccessType.Public, 10, null, null, null, null, now));
    }

    [Theory]
    [InlineData("", "description")]
    [InlineData("ab", "description")]
    [InlineData("Curriculum", null)]
    public void Template_rejects_invalid_metadata(string name, string? description) =>
        Assert.Throws<DomainRuleViolationException>(() => Class.CreateTemplate(name, description!, Guid.NewGuid(), DateTime.UtcNow));

    [Theory]
    [InlineData("https://evil.example/video", 60)]
    [InlineData("https://www.youtube.com.evil.example/watch?v=dQw4w9WgXcQ", 60)]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ", 60)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", 0)]
    public async Task Video_validation_rejects_untrusted_hosts_and_bad_duration(string url, double duration)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var validator = new ContentValidator(db, new ConfigurationBuilder().Build());
        ContentBlock[] blocks = [new(Guid.NewGuid(), "Video", JsonSerializer.SerializeToElement(new { provider = "YouTube", url, durationSec = duration }))];
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => validator.Validate(blocks, new Actor(Guid.NewGuid(), false, true), new Topic(), default));
    }

    [Fact]
    public async Task Lecture_normalizes_video_and_preserves_code_while_removing_script()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var validator = new ContentValidator(db, new ConfigurationBuilder().Build());
        ContentBlock[] blocks = [
            new(Guid.NewGuid(), "Markdown", JsonSerializer.SerializeToElement(new { markdown = "<script>alert(1)</script>\n```cpp\nstd::stack<int> s;\n```" })),
            new(Guid.NewGuid(), "Video", JsonSerializer.SerializeToElement(new { provider = "YouTube", url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", durationSec = 60 }))];
        var json = await validator.Validate(blocks, new Actor(Guid.NewGuid(), false, true), new Topic(), default);
        using var parsed = JsonDocument.Parse(json);
        var markdown = parsed.RootElement[0].GetProperty("data").GetProperty("markdown").GetString()!;
        Assert.DoesNotContain("<script>", markdown); Assert.Contains("std::stack<int>", markdown);
        Assert.Equal("https://www.youtube.com/embed/dQw4w9WgXcQ", parsed.RootElement[1].GetProperty("data").GetProperty("url").GetString());
    }
}
