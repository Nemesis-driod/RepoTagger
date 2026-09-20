using FluentAssertions;
using Microsoft.Extensions.Options;
using RepoTagger.AI;
using RepoTagger.AI.Domain;
using Xunit;

namespace RepoTagger.Tests;

public class RepositoryRateLimiterTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CheckAsync_AllowsRepositoryBelowLimit()
    {
        var limiter = CreateLimiter(3);

        await RecordAsync("owner/repo", 1);
        await RecordAsync("owner/repo", 2);

        var result = await limiter.CheckAsync(
            "owner/repo",
            CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.RetryAfterUtc.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_BlocksRepositoryAtLimit()
    {
        var limiter = CreateLimiter(3);

        for (var i = 1; i <= 3; i++)
        {
            await RecordAsync("owner/repo", i);
        }

        var result = await limiter.CheckAsync(
            "owner/repo",
            CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.RetryAfterUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckAsync_DoesNotCountCallsFromAnotherRepository()
    {
        var limiter = CreateLimiter(3);

        for (var i = 1; i <= 3; i++)
        {
            await RecordAsync("owner/repo-a", i);
        }

        var result = await limiter.CheckAsync(
            "owner/repo-b",
            CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.RetryAfterUtc.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_DoesNotCountCallsOlderThan24Hours()
    {
        var limiter = CreateLimiter(3);

        var occurredAt = DateTime.UtcNow.AddHours(-25);

        for (var i = 1; i <= 3; i++)
        {
            await RecordAsync(
                "owner/repo",
                i,
                occurredAt);
        }

        var result = await limiter.CheckAsync(
            "owner/repo",
            CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.RetryAfterUtc.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ReturnsRetryAfterBasedOnOldestCall()
    {
        var limiter = CreateLimiter(3);

        var oldest = DateTime.UtcNow.AddHours(-5);

        for (var i = 1; i <= 3; i++)
        {
            await RecordAsync(
                "owner/repo",
                i,
                oldest.AddMinutes(i));
        }

        var result = await limiter.CheckAsync(
            "owner/repo",
            CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.RetryAfterUtc.Should().NotBeNull();

        result.RetryAfterUtc!.Value.Should().BeCloseTo(
       oldest.AddMinutes(1).AddDays(1).AddMinutes(1),
       TimeSpan.FromSeconds(2));
    }

    private Task RecordAsync(
        string repository,
        int issueNumber,
        DateTime? occurredAtUtc = null)
    {
        return _db.AiUsageRepository.RecordAsync(
            repository,
            issueNumber,
            "Embedding",
            "Gemini",
            "test-model",
            100,
            50,
            0,
            150,
            CancellationToken.None,
            occurredAtUtc ?? DateTime.UtcNow);
    }

    private RepositoryRateLimiter CreateLimiter(int limit)
    {
        var options = Options.Create(new AIOptions
        {
            MaxAiCallsPerDayPerRepository = limit
        });

        return new RepositoryRateLimiter(
            _db.AiUsageRepository,
            options);
    }
}
