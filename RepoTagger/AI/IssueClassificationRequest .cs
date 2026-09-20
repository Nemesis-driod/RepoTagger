namespace RepoTagger.AI
{
    public sealed record IssueClassificationRequest(string Repository, int IssueNumber, string Title, string Body);
}
