using RepoTagger.Security.Redaction;
using System.Text.RegularExpressions;

namespace RepoTagger.Security.Rule;


public class GitHubTokenRule : IRedactionRule
{
    private static readonly Regex Regex =
     new(
         @"\b(ghp_|gho_|ghu_|ghs_|ghr_|github_pat_)[A-Za-z0-9_]{20,}\b",
         RegexOptions.Compiled);


    public string Name => "GITHUB_TOKEN";


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