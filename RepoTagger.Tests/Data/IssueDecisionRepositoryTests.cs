using Dapper;
using FluentAssertions;
using RepoTagger.Data;
using Xunit;

namespace RepoTagger.Tests.Data;

public class IssueDecisionRepositoryTests : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private IssueDecisionRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        _repository = new IssueDecisionRepository(_db.Context);
    }

    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task ExistsAsync_FalseBeforeThenTrueAfterAdd()
    {
        (await _repository.ExistsAsync("owner/repo", 1, CancellationToken.None)).Should().BeFalse();

        await _repository.AddAsync(CreateDecision("owner/repo", 1, "bug", 0.9), CancellationToken.None);

        (await _repository.ExistsAsync("owner/repo", 1, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_UpsertsRatherThanDuplicating()
    {
        await _repository.AddAsync(CreateDecision("owner/repo", 1, "bug", 0.60), CancellationToken.None);
        await _repository.AddAsync(CreateDecision("owner/repo", 1, "feature-request", 0.95), CancellationToken.None);

        // No GetAsync exists on the repository — querying the DB directly from
        // the test, via the already-public DataContext, needs no source change.
        using var connection = _db.Context.CreateConnection();
        var rows = (await connection.QueryAsync<IssueDecision>(
            "SELECT * FROM IssueDecisions WHERE Repository = @Repository AND IssueNumber = @IssueNumber",
            new { Repository = "owner/repo", IssueNumber = 1 })).ToList();

        rows.Should().ContainSingle();
        rows[0].Label.Should().Be("feature-request");
        rows[0].Confidence.Should().Be(0.95);
    }

    [Fact]
    public async Task DecidedAtUtc_DapperRoundTrip_DoesNotPreserveUtcKind()
    {
        // This documents a known SQLite + Dapper limitation, not a bug in this
        // project's code — SQLite has no native datetime type, so DateTimeKind
        // doesn't survive the round trip. Worth having as a visible, regression-
        // checked fact rather than an assumption nobody verified.
        await _repository.AddAsync(CreateDecision("owner/repo", 1, "bug", 0.9), CancellationToken.None);

        using var connection = _db.Context.CreateConnection();
        var stored = await connection.QuerySingleAsync<IssueDecision>(
            "SELECT * FROM IssueDecisions WHERE Repository = @Repository AND IssueNumber = @IssueNumber",
            new { Repository = "owner/repo", IssueNumber = 1 });

        stored.DecidedAtUtc.Kind.Should().Be(DateTimeKind.Unspecified); // the actual, current behavior
    }

    private static IssueDecision CreateDecision(string repository, int issueNumber, string label, double confidence) =>
        new()
        {
            Repository = repository,
            IssueNumber = issueNumber,
            Label = label,
            Confidence = confidence,
            ModelReason = "test",
            DecidedAtUtc = DateTime.UtcNow,
            Provider = "test-provider",
            Model = "test-model",
        };
}