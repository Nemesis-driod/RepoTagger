namespace RepoTagger.AI
{
    public interface IIssueClassifier
    {
        Task<IssueClassificationResult> ClassifyAsync(IssueClassificationRequest request, CancellationToken ct = default);
    }
}
