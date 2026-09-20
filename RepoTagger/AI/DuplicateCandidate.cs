namespace RepoTagger.AI
{
    public sealed record DuplicateCandidate(   string Repository, int IssueNumber,string Title,double Similarity);
}
