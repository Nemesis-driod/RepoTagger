using Microsoft.Extensions.VectorData;

namespace RepoTagger.Data
{
    public sealed class IssueEmbedding
    {
        public const int Dimensions = 3072;

        [VectorStoreKey]
        public string Id { get; init; } = "";

        [VectorStoreData]
        public string Repository { get; init; } = "";

        [VectorStoreData]
        public int IssueNumber { get; init; }

        [VectorStoreData]
        public string Title { get; init; } = "";

        [VectorStoreVector(Dimensions, DistanceFunction = DistanceFunction.CosineDistance)]
        public ReadOnlyMemory<float> Vector { get; init; }

        public static string NormalizeRepository(string repository)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            return repository.Trim().ToLowerInvariant();
        }

        public static IssueEmbedding Create(string repository, int issueNumber, string title, ReadOnlyMemory<float> vector)
        {
            ArgumentNullException.ThrowIfNull(title);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(issueNumber);

            if (vector.Length != Dimensions)
            {
                throw new ArgumentException($"Expected a {Dimensions}-dimensional embedding, got {vector.Length}.", nameof(vector));
            }

            var normalizedRepository = NormalizeRepository(repository);

            return new IssueEmbedding
            {
                Id = $"{normalizedRepository}#{issueNumber}",
                Repository = normalizedRepository,
                IssueNumber = issueNumber,
                Title = title,
                Vector = vector
            };
        }
    }
}