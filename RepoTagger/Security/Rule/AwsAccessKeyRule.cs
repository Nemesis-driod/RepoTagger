using RepoTagger.Security.Redaction;
using System.Text.RegularExpressions;

namespace RepoTagger.Security.Rule
{
    public class AwsAccessKeyRule : IRedactionRule
    {

        private static readonly Regex Regex =
            new(
                @"\bAKIA[0-9A-Z]{16}\b",
                RegexOptions.Compiled);


        public string Name => "AWS_ACCESS_KEY";


        public int Priority => RedactionPriorities.Critical;



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