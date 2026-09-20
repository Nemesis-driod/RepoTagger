using RepoTagger.Security.Redaction;
using System.Text.RegularExpressions;

namespace RepoTagger.Security.Rule
{
    public class PrivateKeyRule : IRedactionRule
    {
        private static readonly Regex Regex =
            new(
                @"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----",
                RegexOptions.Compiled);


        public string Name => "PRIVATE_KEY";


        public int Priority => RedactionPriorities.KnownSecret;

            
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