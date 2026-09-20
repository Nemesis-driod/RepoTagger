namespace RepoTagger.Data
{
    public class IssueDecision
    {
        public long Id { get; set; }

        public string Repository { get; set; }

        public int IssueNumber { get; set; }

        public string Label { get; set; }

        public double Confidence { get; set; }

        public string ModelReason { get; set; }

        public DateTime DecidedAtUtc { get; set; }

        public string Model { get; set; }


        public string Provider { get; set; }
     

    }

}
