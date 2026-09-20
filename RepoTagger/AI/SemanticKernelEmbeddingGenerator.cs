
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Embeddings;

namespace RepoTagger.AI
{
    public sealed class SemanticKernelEmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>> _embeddings;
        public SemanticKernelEmbeddingGenerator(Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>> embeddings)
            => _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));

        public async Task<ReadOnlyMemory<float>> GenerateAsync(string text, CancellationToken ct)
        {
            var result = await _embeddings.GenerateAsync(text, cancellationToken: ct);
            return result.Vector;
        }
    }
}
