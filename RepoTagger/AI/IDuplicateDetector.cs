namespace RepoTagger.AI
{
    public interface IDuplicateDetector
    {

      
            Task<IReadOnlyList<DuplicateCandidate>> FindAsync(string repository, int issueNumber, string redactedTitle, string redactedBody, CancellationToken ct);

            Task StoreAsync(string repository, int issueNumber, string title, string body, CancellationToken ct);

    }
}
