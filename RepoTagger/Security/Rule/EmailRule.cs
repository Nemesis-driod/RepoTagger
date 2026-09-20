using RepoTagger.Security.Redaction;
using System.Text.RegularExpressions;

namespace RepoTagger.Security.Rule
{
    public class EmailRule : IRedactionRule
    {

        private static readonly Regex Regex =
            new(
                @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
                RegexOptions.Compiled);



        public string Name => "EMAIL";


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
