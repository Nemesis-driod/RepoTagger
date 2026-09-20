namespace RepoTagger.Security.Redaction
{
        public interface IRedactionRule
        {
            string Name { get; }

            int Priority { get; }

            IEnumerable<RedactionMatch> Find(string text);
        }
}
