namespace RepoTagger.Data
{
    public class Metrics
    {
        public sealed record OperationsReport
        {
            public long PendingJobs { get; init; }
            public long FailedJobs { get; init; }
            public long StuckJobs { get; init; }
            public long TotalWebhookDeliveries { get; init; }
            public double JobRetryRate { get; init; }
            public double? AverageProcessingTimeSeconds { get; init; }
        }
        public sealed record IssueBreakdown
        {
            public string Repository { get; init; } = "";
            public string Label { get; init; } = "";
            public string Provider { get; init; } = "";
            public string Model { get; init; } = "";
            public long Count { get; init; }
            public double AverageConfidence { get; init; }
            public long LowConfidenceCount { get; init; }
        }
    }
}
