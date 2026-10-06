using System.Net;
using System.Net.Http.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodePath.Integration.Tests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginConcurrencySpecifications
{
    private readonly CodePathApiFixture _fixture;

    public LoginConcurrencySpecifications(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PasswordVerification_ShouldRespectGlobalAndPerIpLimits()
    {
        var tracker = new TrackingPasswordHasher(TimeSpan.FromMilliseconds(80));
        using var factory = CreateFactory(tracker, maxGlobal: 2, maxPerIp: 1, queueLimit: 50);
        using var client = CreateClient(factory);

        await SendConcurrentLogins(client, 12, _ => "198.51.100.30");
        tracker.MaximumConcurrency.Should().Be(1);

        tracker.ResetMaximum();
        await SendConcurrentLogins(client, 12, index => $"198.51.100.{100 + index}");
        tracker.MaximumConcurrency.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task FullConcurrencyQueue_ShouldReturn429AndRetryAfter()
    {
        var tracker = new TrackingPasswordHasher(TimeSpan.FromMilliseconds(500));
        using var factory = CreateFactory(tracker, maxGlobal: 1, maxPerIp: 1, queueLimit: 0);
        using var client = CreateClient(factory);

        var first = SendLogin(client, 1, "198.51.100.40");
        tracker.WaitUntilEntered(TimeSpan.FromSeconds(5)).Should().BeTrue();
        using var rejected = await SendLogin(client, 2, "198.51.100.41");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
        using var completed = await first;
        completed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private CodePathWebApplicationFactory CreateFactory(
        TrackingPasswordHasher tracker,
        int maxGlobal,
        int maxPerIp,
        int queueLimit) =>
        _fixture.CreateFactory(
            new Dictionary<string, string?>
            {
                ["Auth:LoginConcurrency:MaxConcurrentLogins"] = maxGlobal.ToString(),
                ["Auth:LoginConcurrency:MaxConcurrentPerIp"] = maxPerIp.ToString(),
                ["Auth:LoginConcurrency:QueueLimit"] = queueLimit.ToString(),
                ["Auth:LoginConcurrency:QueueTimeoutSeconds"] = "5"
            },
            services =>
            {
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher>(tracker);
            });

    private static HttpClient CreateClient(CodePathWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static async Task SendConcurrentLogins(
        HttpClient client,
        int count,
        Func<int, string> ipFactory)
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(1, count).Select(index => SendLogin(client, index, ipFactory(index))));
        foreach (var response in responses)
        {
            using (response)
            {
                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
        }
    }

    private static Task<HttpResponseMessage> SendLogin(HttpClient client, int index, string ipAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = $"concurrency-{index}-{Guid.NewGuid():N}@example.edu",
                password = "WrongPassword1!"
            })
        };
        request.Headers.Add("X-Forwarded-For", ipAddress);
        return client.SendAsync(request);
    }

    private sealed class TrackingPasswordHasher : IPasswordHasher
    {
        private readonly TimeSpan _duration;
        private readonly ManualResetEventSlim _entered = new();
        private int _active;
        private int _maximum;

        public TrackingPasswordHasher(TimeSpan duration) => _duration = duration;

        public int MaximumConcurrency => Volatile.Read(ref _maximum);

        public string HashPassword(string password) => "test-hash";
        public bool NeedsRehash(string passwordHash) => false;

        public bool VerifyPassword(string password, string passwordHash)
        {
            TrackWork();
            return false;
        }

        public void SimulateVerification() => TrackWork();

        public void ResetMaximum() => Interlocked.Exchange(ref _maximum, 0);
        public bool WaitUntilEntered(TimeSpan timeout) => _entered.Wait(timeout);

        private void TrackWork()
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            _entered.Set();
            try
            {
                Thread.Sleep(_duration);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdateMaximum(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maximum);
                if (value <= current || Interlocked.CompareExchange(ref _maximum, value, current) == current)
                {
                    return;
                }
            }
        }
    }
}
