using System.Net;
using System.Net.Sockets;
using System.Text;
using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.Email;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MimeKit;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class EmailSenderTests
{
    [Theory]
    [InlineData("otp")]
    [InlineData("reset")]
    [InlineData("changed")]
    public async Task Development_ShouldDeliverAuthenticationEmailsThroughSmtp(string kind)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = ReceiveMessageAsync(listener, timeout.Token);
        var sender = CreateSender(Environments.Development, new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromAddress = "sender@example.com",
            SocketOptions = "None",
        });

        await SendAsync(sender, kind, timeout.Token);
        var rawMessage = await received;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawMessage));
        var message = await MimeMessage.LoadAsync(stream, timeout.Token);
        message.To.Mailboxes.Single().Address.Should().Be("student@example.com");
        message.From.Mailboxes.Single().Address.Should().Be("sender@example.com");
        if (kind == "otp") message.TextBody.Should().Contain("123456");
        if (kind == "reset") message.TextBody.Should().Contain("https://localhost:5173/reset-password#token=" + new string('A', 64));
        if (kind == "changed") message.TextBody.Should().Contain("password was changed");
    }

    [Theory]
    [InlineData("otp")]
    [InlineData("reset")]
    [InlineData("changed")]
    public async Task Testing_ShouldNeverConnectToSmtp(string kind)
    {
        await SendAsync(CreateSender("Testing", new SmtpOptions()), kind, CancellationToken.None);
    }

    [Fact]
    public async Task Development_WithoutSmtp_ShouldReportMissingConfiguration()
    {
        var action = () => CreateSender(Environments.Development, new SmtpOptions())
            .SendOtpEmailAsync("student@example.com", "123456");
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SMTP is not configured*");
    }

    private static EmailSender CreateSender(string environmentName, SmtpOptions smtp)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return new EmailSender(NullLogger<EmailSender>.Instance, environment.Object,
            Options.Create(smtp), Options.Create(new AuthFrontendOptions { PublicBaseUrl = "https://localhost:5173" }));
    }

    private static Task SendAsync(EmailSender sender, string kind, CancellationToken ct) => kind switch
    {
        "otp" => sender.SendOtpEmailAsync("student@example.com", "123456", ct),
        "reset" => sender.SendPasswordResetEmailAsync("student@example.com", new string('A', 64), ct),
        _ => sender.SendPasswordChangedEmailAsync("student@example.com", ct),
    };

    private static async Task<string> ReceiveMessageAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 localhost SMTP");
        var message = new StringBuilder();
        var inData = false;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (inData)
            {
                if (line != ".") { message.AppendLine(line); continue; }
                inData = false;
                await writer.WriteLineAsync("250 Accepted");
            }
            else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                inData = true;
                await writer.WriteLineAsync("354 End with dot");
            }
            else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");
                break;
            }
            else await writer.WriteLineAsync("250 OK");
        }
        return message.ToString();
    }
}
