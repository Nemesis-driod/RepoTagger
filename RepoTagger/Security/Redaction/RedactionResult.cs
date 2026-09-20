namespace RepoTagger.Security.Redaction
{
    public class RedactionResult
    {
        public string Text { get; set; } = string.Empty;

        public List<RedactionFinding> Findings { get; set; } = new();
    }
}
