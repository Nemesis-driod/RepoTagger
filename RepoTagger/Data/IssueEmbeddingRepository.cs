using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.Extensions.VectorData;
using RepoTagger.AI;

namespace RepoTagger.Data
{
    public sealed class IssueEmbeddingRepository : IIssueEmbeddingRepository ,IDisposable
    {
        private const string CollectionName = "issue_embedding_v1";

        private readonly SqliteCollection<string, IssueEmbedding> _collection;

        public IssueEmbeddingRepository(DataContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _collection = new SqliteCollection<string, IssueEmbedding>(context.ConnectionString, CollectionName);
        }


        public Task EnsureCollectionExistsAsync(CancellationToken ct)
        {
            return _collection.EnsureCollectionExistsAsync(ct);
        }

        public async Task AddAsync(IssueEmbedding embedding, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(embedding);
            ValidateEmbedding(embedding.Vector);
            await _collection.UpsertAsync(embedding, ct);
        }

        public async Task<IReadOnlyList<DuplicateCandidate>> FindSimilarAsync(
            string repository, ReadOnlyMemory<float> embedding, int top, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            if (top <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(top), top, "Top must be greater than zero.");
            }
            ValidateEmbedding(embedding);


            var normalizedRepository = IssueEmbedding.NormalizeRepository(repository);


            var options = new VectorSearchOptions<IssueEmbedding>
            {
                Filter = record => record.Repository == normalizedRepository
            };

            var candidates = new List<DuplicateCandidate>(top);

            await foreach (var result in _collection.SearchAsync(embedding, top, options, ct))
            {
                var distance = result.Score ?? throw new InvalidOperationException(
                    "SQLite vector search returned a result without a score.");

                // Vector property is configured with CosineDistance (0 = identical, up to 2 = opposite).
                // DuplicateCandidate.Similarity should read "higher = more similar", so invert here —
                // this is the one place that needs to know which distance function the store uses.
                var similarity = 1d - distance;

                candidates.Add(new DuplicateCandidate(
                    result.Record.Repository, result.Record.IssueNumber, result.Record.Title, similarity));
            }

            return candidates;
        }

        private static void ValidateEmbedding(ReadOnlyMemory<float> embedding)
        {
            if (embedding.Length != IssueEmbedding.Dimensions)
            {
                throw new ArgumentException(
                    $"Expected a {IssueEmbedding.Dimensions}-dimensional embedding, got {embedding.Length}.", nameof(embedding));
            }

            foreach (var value in embedding.Span)
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentException("Embedding contains NaN or infinity.", nameof(embedding));
                }
            }
        }


        public void Dispose()
        {
            _collection.Dispose();
        }

    }
}