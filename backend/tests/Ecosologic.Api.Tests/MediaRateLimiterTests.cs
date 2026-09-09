using System.Net;
using System.Security.Claims;
using Ecosologic.Api.Security;
using Microsoft.AspNetCore.Http;

namespace Ecosologic.Api.Tests;

public sealed class MediaRateLimiterTests
{
    [Fact]
    public void Upload_rate_limit_allows_twenty_requests_per_minute()
    {
        var options = MediaRateLimiter.UploadOptions();

        Assert.Equal(20, options.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Window);
        Assert.Equal(0, options.QueueLimit);
    }

    [Fact]
    public void PartitionKey_uses_authenticated_user_over_ip()
    {
        var context = NewContext("203.0.113.10", "admin@example.com");

        Assert.Equal("admin@example.com", MediaRateLimiter.PartitionKey(context));
    }

    [Fact]
    public void PartitionKey_distinguishes_different_users_sharing_the_same_ip()
    {
        var userA = NewContext("203.0.113.10", "a@example.com");
        var userB = NewContext("203.0.113.10", "b@example.com");

        Assert.NotEqual(MediaRateLimiter.PartitionKey(userA), MediaRateLimiter.PartitionKey(userB));
    }

    [Fact]
    public void PartitionKey_falls_back_to_ip_when_unauthenticated()
    {
        var context = NewContext("203.0.113.10", null);

        Assert.Equal("203.0.113.10", MediaRateLimiter.PartitionKey(context));
    }

    private static DefaultHttpContext NewContext(string ip, string? userName)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        if (userName is not null)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], "Test");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }
}
