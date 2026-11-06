using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Events;
using CodePath.Application.Notifications.Handlers;
using CodePath.Domain.Notifications.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Notifications;

public sealed class NotificationEmailHandlerTests
{
    private readonly Mock<INotificationDbContext> _db = new();
    private readonly Mock<INotificationEmailRenderer> _renderer = new();
    private readonly Mock<IEmailSender> _sender = new();

    private EmailDispatchHandler CreateHandler() => new(
        _db.Object,
        _renderer.Object,
        _sender.Object,
        NullLogger<EmailDispatchHandler>.Instance);

    [Fact]
    public async Task Handle_ResolvesAddress_RendersAndSends()
    {
        var recipient = Guid.NewGuid();
        const string email = "student@hanu.edu.vn";
        _db.Setup(d => d.GetUserEmailAsync(recipient, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        _renderer.Setup(r => r.Render(It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("<html>rendered</html>");

        var message = new EmailDispatchNotification(recipient, NotificationType.SubmissionGraded, "Graded", "You scored 9");

        await CreateHandler().Handle(message, CancellationToken.None);

        _renderer.Verify(r => r.Render(NotificationType.SubmissionGraded, "Graded", "You scored 9"), Times.Once);
        _sender.Verify(s => s.SendNotificationEmailAsync(
            email, "Graded", "<html>rendered</html>", "You scored 9", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoAddress_SkipsSend()
    {
        var recipient = Guid.NewGuid();
        _db.Setup(d => d.GetUserEmailAsync(recipient, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var message = new EmailDispatchNotification(recipient, NotificationType.NewMessage, "Hi", "Body");

        await CreateHandler().Handle(message, CancellationToken.None);

        _sender.Verify(s => s.SendNotificationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSenderThrows_PropagatesForOutboxRetry()
    {
        var recipient = Guid.NewGuid();
        _db.Setup(d => d.GetUserEmailAsync(recipient, It.IsAny<CancellationToken>())).ReturnsAsync("a@hanu.edu.vn");
        _renderer.Setup(r => r.Render(It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>())).Returns("<html/>");
        _sender.Setup(s => s.SendNotificationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var message = new EmailDispatchNotification(recipient, NotificationType.NewMessage, "Hi", "Body");

        var act = () => CreateHandler().Handle(message, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
