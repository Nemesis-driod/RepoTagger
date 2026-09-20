using RepoTagger.Security.Redaction;

namespace RepoTagger.AI
{
    public sealed record IssueAnalysis(
        IssueClassificationResult Classification,
        IReadOnlyList<RedactionFinding> Redactions,
        IReadOnlyList<DuplicateCandidate> Duplicates,
         string RedactedTitle,
         string RedactedBody,
    ClassificationMetadata Metadata);

    public sealed record ClassificationMetadata(
    string Provider,
    string Model,
    TimeSpan Duration,
    DateTime PerformedAtUtc);

}
