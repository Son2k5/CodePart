using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Notifications.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Integration.Tests.Notifications;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationApiSpecifications(CodePathApiFixture fixture)
{
    private async Task<(User User, string Token)> NewStudentAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("TestPassword1!");
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var user = User.CreateStudent("Student", suffix + "@hanu.edu.vn", hash, suffix, now);
        user.VerifyEmail(now);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user, await AuthTestClient.IssueSessionAsync(scope.ServiceProvider, user));
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token, object? data = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (data != null) request.Content = JsonContent.Create(data);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    [Fact]
    public async Task Inbox_RequiresAuthentication()
    {
        var response = await Send(fixture.Client, HttpMethod.Get, "/api/notifications", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Inbox_ListsMarksRead_AndRejectsCrossUserAccess()
    {
        var (owner, ownerToken) = await NewStudentAsync();
        var (_, otherToken) = await NewStudentAsync();

        Guid notificationId;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notification = Notification.Create(owner.Id, NotificationType.SubmissionGraded,
                "Bài làm đã được chấm", "Bạn đạt 9/10", null, null, DateTime.UtcNow);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            notificationId = notification.Id;
        }

        var unread = await Json(await Send(fixture.Client, HttpMethod.Get, "/api/notifications/unread-count", ownerToken));
        unread.GetProperty("count").GetInt32().Should().Be(1);

        var page = await Json(await Send(fixture.Client, HttpMethod.Get, "/api/notifications", ownerToken));
        page.GetProperty("items").GetArrayLength().Should().Be(1);

        // Another user must not see or mutate someone else's notification (IDOR).
        (await Send(fixture.Client, HttpMethod.Patch, $"/api/notifications/{notificationId}/read", otherToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await Json(await Send(fixture.Client, HttpMethod.Patch, $"/api/notifications/{notificationId}/read", ownerToken));

        unread = await Json(await Send(fixture.Client, HttpMethod.Get, "/api/notifications/unread-count", ownerToken));
        unread.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Preferences_RoundTrip()
    {
        var (_, token) = await NewStudentAsync();

        var before = await Json(await Send(fixture.Client, HttpMethod.Get, "/api/notifications/preferences", token));
        before.GetArrayLength().Should().Be(0);

        await Json(await Send(fixture.Client, HttpMethod.Put, "/api/notifications/preferences", token,
            new[] { new { type = "AiHintReady", inAppEnabled = false, emailEnabled = true } }));

        var after = await Json(await Send(fixture.Client, HttpMethod.Get, "/api/notifications/preferences", token));
        after.GetArrayLength().Should().Be(1);
        after[0].GetProperty("inAppEnabled").GetBoolean().Should().BeFalse();
    }
}
