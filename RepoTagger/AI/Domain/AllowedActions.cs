namespace RepoTagger.AI.Domain
{
    public class AllowedActions
    {
        public static readonly IReadOnlySet<string> Labels =
             new HashSet<string> { "bug", "feature-request", "question", "needs-human-review" };

        public static bool IsAllowedLabel(string label) => Labels.Contains(label);

    }
}
