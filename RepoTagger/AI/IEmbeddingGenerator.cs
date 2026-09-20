namespace RepoTagger.AI
{
    public interface IEmbeddingGenerator
    {
        Task<ReadOnlyMemory<float>> GenerateAsync(string text, CancellationToken ct);

    }
}
