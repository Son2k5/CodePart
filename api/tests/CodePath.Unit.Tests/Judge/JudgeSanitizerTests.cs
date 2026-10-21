using CodePath.Application.Judge;
using Xunit;

namespace CodePath.Unit.Tests.Judge;

public class JudgeSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldStripSandboxAndBoxPaths()
    {
        var raw = "Error in /box/script.py: line 12: FileNotFoundError: [Errno 2] No such file or directory: '/sandbox/data.txt'";
        var sanitized = JudgeSanitizer.Sanitize(raw);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("/box/", sanitized);
        Assert.DoesNotContain("/sandbox/", sanitized);
        Assert.Contains("script.py: line 12", sanitized);
        Assert.Contains("data.txt", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldStripTmpPaths()
    {
        var raw = "Fatal error: /tmp/judge0-isolate-1002/solution.cpp:4:5: error: 'x' was not declared in this scope";
        var sanitized = JudgeSanitizer.Sanitize(raw);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("/tmp/", sanitized);
        Assert.Contains("solution.cpp:4:5: error:", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldStripWindowsPaths()
    {
        var raw = @"Compilation failure at C:\Users\Runner\sandbox\Program.cs:line 45";
        var sanitized = JudgeSanitizer.Sanitize(raw);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain(@"C:\", sanitized);
        Assert.Contains("Program.cs:line 45", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldStripContainerAndHostNames()
    {
        var raw = "Process failed on runner-node-42a1 (host: 192.168.1.100): stack overflow";
        var sanitized = JudgeSanitizer.Sanitize(raw);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("runner-node-42a1", sanitized);
        Assert.DoesNotContain("192.168.1.100", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldTruncateExtremelyLongOutput()
    {
        var raw = new string('A', 5000);
        var sanitized = JudgeSanitizer.Sanitize(raw);

        Assert.NotNull(sanitized);
        Assert.True(sanitized.Length < 4100);
        Assert.Contains("... [Đã cắt bớt phần thông báo dài]", sanitized);
    }

    [Fact]
    public void Sanitize_WithNullOrEmpty_ReturnsNull()
    {
        Assert.Null(JudgeSanitizer.Sanitize(null));
        Assert.Null(JudgeSanitizer.Sanitize(string.Empty));
    }
}
