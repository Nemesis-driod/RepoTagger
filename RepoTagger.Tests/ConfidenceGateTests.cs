

using Microsoft.Extensions.Options;
using RepoTagger.AI;
using RepoTagger.AI.Domain;

using Xunit;

namespace RepoTagger.Tests
{
    public class ConfidenceGateTests
    {
        [Fact]
        public void Confidence_Equal_to_threshold_should_apply()
        {
            var gate = new ConfidenceGate(Options.Create(new AIOptions { ConfidenceThreshold = 0.75 }));

            var result = gate.Evaluate(0.85);

            Assert.Equal(TaggerOutcome.AutoApply, result);


        }


        [Fact]
        public void Confidence_Above_to_threshold_should_apply()
        {
            var gate = new ConfidenceGate(Options.Create(new AIOptions { ConfidenceThreshold = 0.75 }));

            var result = gate.Evaluate(0.91);

            Assert.Equal(TaggerOutcome.AutoApply, result);
        }


        [Fact]
        public void Condifence_Below_to_Threshold_should_require_review()
        {
            var gate = new ConfidenceGate(Options.Create(new AIOptions { ConfidenceThreshold = 0.75 }));
            var result = gate.Evaluate(0.7);

            Assert.Equal(TaggerOutcome.NeedsHumanReview, result);
        }

        [Fact]
        public void Confidence_threshold_change()
        {
            var gate = new ConfidenceGate(Options.Create(new AIOptions { ConfidenceThreshold = 0.75 }));

            var result = gate.Evaluate(0.70);

            Assert.Equal(TaggerOutcome.NeedsHumanReview, result);

        }

        [Fact]
        public void Custom_threshold_is_actually_used()
        {
            var gate = new ConfidenceGate(Options.Create(new AIOptions { ConfidenceThreshold = 0.75 }));
            var result = gate.Evaluate(0.80); // fails default 0.85, passes custom 0.75
            Assert.Equal(TaggerOutcome.AutoApply, result);
        }

        
        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public void Invalid_Confidence_Should_Throw(double confidence)
        {
            var gate = new ConfidenceGate(Options.Create(new AIOptions { ConfidenceThreshold = 0.75 }));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => gate.Evaluate(confidence));
        }
    }
}
