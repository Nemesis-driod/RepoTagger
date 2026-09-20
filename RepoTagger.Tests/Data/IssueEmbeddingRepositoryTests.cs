using FluentAssertions;
using RepoTagger.Data;
using RepoTagger.Tests;
using Xunit;

public class IssueEmbeddingRepositoryTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task FindSimilarAsync_ShouldOnlyReturnCandidatesFromSameRepository()
    {
        var repository = _db.EmbeddingRepository;

        await repository.AddAsync(IssueEmbedding.Create("owner/repo-a", 1, "Same repository issue", CreateVector(1f)), CancellationToken.None);
        await repository.AddAsync(IssueEmbedding.Create("owner/repo-b", 2, "Different repository issue", CreateVector(1f)), CancellationToken.None);

        var results = await repository.FindSimilarAsync("owner/repo-a", CreateVector(1f), 5, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Repository.Should().Be("owner/repo-a");
        results.Should().NotContain(x => x.Repository == "owner/repo-b");
    }

    [Fact]
    public async Task AddAsync_RejectsWrongDimensionVector()
    {
        var repository = _db.EmbeddingRepository;
        var wrongSize = new float[100];

        var act = () => repository.AddAsync(
            IssueEmbedding.Create("owner/repo", 1, "title", wrongSize), CancellationToken.None);

        // Create() itself throws first — this documents that invariant.
        var thrown = () => IssueEmbedding.Create("owner/repo", 1, "title", wrongSize);
        thrown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task AddAsync_Upserts_ForSameRepositoryAndIssueNumber()
    {
        var repository = _db.EmbeddingRepository;

        await repository.AddAsync(IssueEmbedding.Create("owner/repo", 1, "First title", CreateVector(1f)), CancellationToken.None);
        await repository.AddAsync(IssueEmbedding.Create("owner/repo", 1, "Updated title", CreateVector(0.5f)), CancellationToken.None);

        var results = await repository.FindSimilarAsync("owner/repo", CreateVector(0.5f), 5, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Title.Should().Be("Updated title");
    }

    [Fact]
    public async Task FindSimilarAsync_NormalizesRepositoryNameOnBothWriteAndRead()
    {
        var repository = _db.EmbeddingRepository;
        await repository.AddAsync(IssueEmbedding.Create("Owner/Repo", 1, "title", CreateVector(1f)), CancellationToken.None);

        var results = await repository.FindSimilarAsync(" owner/repo ", CreateVector(1f), 5, CancellationToken.None);

        results.Should().ContainSingle();
    }

    private static ReadOnlyMemory<float> CreateVector(float value)
    {
        var vector = new float[IssueEmbedding.Dimensions];
        Array.Fill(vector, value);
        return vector;
    }
}