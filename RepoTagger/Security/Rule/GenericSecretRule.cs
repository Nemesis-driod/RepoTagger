    using RepoTagger.Security.Redaction;
    using System.Text.RegularExpressions;

    namespace RepoTagger.Security.Rule
    {
        public class GenericSecretRule : IRedactionRule
        {
        private static readonly Regex Regex = new(
     """(?i)(token|secret|apikey|api_key|password|private_key)[\w]*['"]?\s*[:=]\s*['"]?[A-Za-z0-9+/_\-=]{16,}['"]?""",
     RegexOptions.Compiled);

        public string Name => "GENERIC_SECRET";
            public int Priority => RedactionPriorities.Generic;

        public IEnumerable<RedactionMatch> Find(string text)
            {
                foreach (Match match in Regex.Matches(text))
                {
                    yield return new RedactionMatch
                    {
                        Start = match.Index,
                        Length = match.Length,
                        Type = Name,
                        Priority = Priority
                    };
                }
            }
        }
    }
