namespace RepoTagger.Security.Redaction
{
    public class RedactionMatch
    {
        public int Start { get; set; }

        public int Length { get; set; }

        public string Type { get; set; } = string.Empty;


        public int Priority { get; set; }

    }
}
