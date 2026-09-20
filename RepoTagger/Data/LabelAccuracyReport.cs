namespace RepoTagger.Data
{
    public sealed record LabelAccuracyReport
    {
        public string Label { get; init; } = "";
        public long TotalDecisions { get; init; }
        public long OverriddenCount { get; init; }
        public double AverageConfidence { get; init; }
        public double? AverageHoursToOverride { get; init; }
    }
}
