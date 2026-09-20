

namespace RepoTagger.AI
{
    public sealed record IssueClassificationResult(string Label, double Confidence, string Reason);
}
