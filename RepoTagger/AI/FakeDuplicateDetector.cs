namespace RepoTagger.AI
{
    public class FakeDuplicateDetector : IDuplicateDetector
    {
        public Task<IReadOnlyList<DuplicateCandidate>> FindAsync(
       string repository,  int issuenumber  ,string title,   string body, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<DuplicateCandidate>>(
                Array.Empty<DuplicateCandidate>());
        }

       

        public Task StoreAsync(  string repository,int issueNumber, string title,  string body, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
