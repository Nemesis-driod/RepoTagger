

namespace RepoTagger.AI
{
    public class FakeIssueClassifier : IIssueClassifier
    {
        public Task<IssueClassificationResult> ClassifyAsync(IssueClassificationRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(
                new IssueClassificationResult(
                "bug",
                0.95,
                "Fake classifier result"
            ));
        }
    }
}
