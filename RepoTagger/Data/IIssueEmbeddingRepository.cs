using RepoTagger.AI;

namespace RepoTagger.Data
{
    public interface IIssueEmbeddingRepository
    {
        Task EnsureCollectionExistsAsync(CancellationToken ct);
        Task AddAsync( IssueEmbedding embedding,  CancellationToken ct);

        Task<IReadOnlyList<DuplicateCandidate>> FindSimilarAsync( string repository,
            ReadOnlyMemory<float> embedding, int top, CancellationToken ct);
    }
}
