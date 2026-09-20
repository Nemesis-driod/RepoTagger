using System.ComponentModel.DataAnnotations;

namespace RepoTagger.AI
{
    public class AIOptions
    {

        public string Provider { get; init; } = "";

    
        public string Endpoint { get; init; } = "";

      
        public string ApiKey { get; init; } = "";

     
        public string ModelId { get; init; } = "";

        public string? EmbeddingModelId { get; init; }

        public double DuplicateSimilarityThreshold { get; init; } = 0.85;

        public int MaxAiCallsPerDayPerRepository { get; init; } = 100;

        public string? EscalationModelId { get; init; }

        public double ConfidenceThreshold { get; init; } = 0.75;
    }
}
