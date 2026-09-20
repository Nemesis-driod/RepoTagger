using RepoTagger.AI.Domain;

namespace RepoTagger.AI
{
    public static class ClassificationEscalationPolicy
    {
        public static bool ShouldEscalate(TaggerOutcome gateOutcome, bool labelAllowed, string? escalationModelId)
        {
            return gateOutcome == TaggerOutcome.NeedsHumanReview
                && labelAllowed
                && !string.IsNullOrWhiteSpace(escalationModelId);
        }
    }
}