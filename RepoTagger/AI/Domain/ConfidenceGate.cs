using Microsoft.Extensions.Options;

namespace RepoTagger.AI.Domain
{

    public enum TaggerOutcome { AutoApply, NeedsHumanReview }

    public class ConfidenceGate
    {
        private readonly double _threshold;

        public ConfidenceGate(IOptions<AIOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _threshold = options.Value.ConfidenceThreshold;
        }

        public TaggerOutcome Evaluate(double confidence)
        {
            if (confidence < 0 || confidence > 1)
                throw new ArgumentOutOfRangeException(nameof(confidence));

            return confidence >= _threshold ? TaggerOutcome.AutoApply : TaggerOutcome.NeedsHumanReview;
        }
    }
}
