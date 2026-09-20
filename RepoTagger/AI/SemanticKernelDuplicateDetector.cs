
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using RepoTagger.Data;

namespace RepoTagger.AI
{
    public sealed class SemanticKernelDuplicateDetector : IDuplicateDetector
    {
        private const int CandidatesToFetch = 5;
        private const int CandidatesToReturn = 3;
        private const int MaxEmbeddingInputCharacters = 8000;

        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;    
        private readonly IIssueEmbeddingRepository _issueEmbeddingRepository;
        private readonly ILogger<SemanticKernelDuplicateDetector> _logger;
        private readonly AiUsageRepository _aiUsageRepository;
        private readonly double _similarityThreshold;
        private readonly AIOptions _aiOptions;


        public SemanticKernelDuplicateDetector(
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IIssueEmbeddingRepository repository,  ILogger<SemanticKernelDuplicateDetector> logger,
            IOptions<AIOptions> options, AiUsageRepository aiUsageRepository)
        {
            _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
            _issueEmbeddingRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _aiUsageRepository = aiUsageRepository ?? throw new ArgumentNullException(nameof(aiUsageRepository));


            ArgumentNullException.ThrowIfNull(options);
             _aiOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
            if (!double.IsFinite(_aiOptions.DuplicateSimilarityThreshold) ||
           _aiOptions.DuplicateSimilarityThreshold < -1d || _aiOptions.DuplicateSimilarityThreshold > 1d)
            {
                throw new InvalidOperationException(
                    "AI:DuplicateSimilarityThreshold must be a finite value between -1 and 1.");
            }

            _similarityThreshold = _aiOptions.DuplicateSimilarityThreshold;

        }

        public async Task<IReadOnlyList<DuplicateCandidate>> FindAsync(string repository, int issueNumber, string redactedTitle, string redactedBody, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentNullException.ThrowIfNull(redactedTitle);
            ArgumentNullException.ThrowIfNull(redactedBody);
            var text = CombineText(redactedTitle, redactedBody);

            var embeddingResult = await GenerateEmbeddingAsync(repository, issueNumber, text, ct);
            var candidates = await _issueEmbeddingRepository.FindSimilarAsync(repository, embeddingResult, CandidatesToFetch, ct);


            foreach (var candidate in candidates)
            {
                _logger.LogInformation(
                    "Duplicate candidate {Repository}#{IssueNumber}: similarity={Similarity}",
                    candidate.Repository,
                    candidate.IssueNumber,
                    candidate.Similarity);
            }


            return candidates
                .Where(c => c.IssueNumber != issueNumber)
                .Where(c => c.Similarity >= _similarityThreshold)
                .Take(CandidatesToReturn)
                .ToList();
               

        }

        

        private static string CombineText(string title, string body)
        {
            var combined = $"{title}\n\n{body}";
            return combined.Length > MaxEmbeddingInputCharacters
                ? combined[..MaxEmbeddingInputCharacters]
                : combined;
        }

        public async Task StoreAsync(string repository, int issueNumber, string title, string body, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repository);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(body);
            var text = CombineText(title, body);

            var vector = await GenerateEmbeddingAsync(repository, issueNumber, text, ct);
            var embedding = IssueEmbedding.Create(repository, issueNumber, title, vector);
            await _issueEmbeddingRepository.AddAsync(embedding, ct);

            
        }

        private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string repository, int issueNumber, string text, CancellationToken ct)
        {
            var result = await _embeddingGenerator.GenerateAsync([text], cancellationToken: ct);
            var embedding = result.FirstOrDefault() ?? throw new InvalidOperationException("Embedding generator returned no results.");

            try
            {
                await _aiUsageRepository.RecordAsync(
                    repository, issueNumber, "Embedding", _aiOptions.Provider, _aiOptions.EmbeddingModelId ?? _aiOptions.ModelId,
                    result.Usage?.InputTokenCount, result.Usage?.OutputTokenCount, result.Usage?.ReasoningTokenCount, result.Usage?.TotalTokenCount, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record embedding usage for {Repository}#{IssueNumber} — continuing without it.", repository, issueNumber);
            }

            return embedding.Vector;
        }
    }
}
