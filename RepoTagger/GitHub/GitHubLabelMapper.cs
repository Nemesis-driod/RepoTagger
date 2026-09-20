namespace RepoTagger.GitHub
{
    public static  class GitHubLabelMapper
    {
        private const string Prefix = "ai:";
        public static string ToGitHubLabel(string label) => $"{Prefix}{label}";

    }
}
