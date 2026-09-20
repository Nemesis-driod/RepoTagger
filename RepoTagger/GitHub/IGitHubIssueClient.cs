namespace RepoTagger.GitHub
{
    public interface IGitHubIssueClient
    {

        Task AddCommentAsync(string repo, int issueNumber, string comment, CancellationToken ct);

        Task AddLabelAsync(string repo, int issueNumber, string label, CancellationToken ct);


        Task<string> GetIssueStateAsync(string repo, int issueNumber, CancellationToken ct);
    }
}
