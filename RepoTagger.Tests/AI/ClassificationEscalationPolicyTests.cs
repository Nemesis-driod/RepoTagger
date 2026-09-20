using FluentAssertions;
using RepoTagger.AI;
using RepoTagger.AI.Domain;
using Xunit;

namespace RepoTagger.Tests.AI;

public class ClassificationEscalationPolicyTests
{
    [Theory]
    [InlineData(TaggerOutcome.NeedsHumanReview, true, "gemini-3.1-pro-preview", true)]   // gate rejected confidence, label allowed, escalation configured
    [InlineData(TaggerOutcome.AutoApply, true, "gemini-3.1-pro-preview", false)]           // confidence was fine — nothing to escalate
    [InlineData(TaggerOutcome.NeedsHumanReview, false, "gemini-3.1-pro-preview", false)]  // model chose needs-human-review directly — a stronger model won't fix missing info
    [InlineData(TaggerOutcome.NeedsHumanReview, true, null, false)]                        // escalation not configured
    [InlineData(TaggerOutcome.NeedsHumanReview, true, "", false)]                          // escalation model blank
    public void ShouldEscalate_ReturnsExpected(TaggerOutcome gateOutcome, bool labelAllowed, string? escalationModelId, bool expected)
    {
        ClassificationEscalationPolicy.ShouldEscalate(gateOutcome, labelAllowed, escalationModelId).Should().Be(expected);
    }
}