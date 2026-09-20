namespace RepoTagger.Security.Redaction;


public class SecretRedactor
{
    private readonly IEnumerable<IRedactionRule> _rules;


    public SecretRedactor(
        IEnumerable<IRedactionRule> rules)
    {
        _rules = rules;
    }


    public RedactionResult Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new RedactionResult
            {
                Text = text ?? string.Empty
            };
        }


        var matches = new List<RedactionMatch>();


        /*
         Important:
         All rules scan the original text.
         Do not replace while detecting.
         Otherwise later rules may match our own
         [REDACTED:TYPE] placeholders.
        */

        foreach (var rule in _rules)
        {
            matches.AddRange(
                rule.Find(text)
            );
        }


        var finalMatches = RemoveOverlappingMatches(matches);


        var output = text;


        foreach (var match in finalMatches.OrderByDescending(x => x.Start))
        {
            output =
                output.Remove(
                    match.Start,
                    match.Length)
                .Insert(
                    match.Start,
                    $"[REDACTED:{match.Type}]");
        }


        return new RedactionResult
        {
            Text = output,

            Findings = finalMatches
                .GroupBy(x => x.Type)
                .Select(x => new RedactionFinding
                {
                    Type = x.Key,
                    Count = x.Count()
                })
                .ToList()
        };
    }



    private static List<RedactionMatch> RemoveOverlappingMatches( List<RedactionMatch> matches)
    {
        var result = new List<RedactionMatch>();


        foreach (var match in matches
            .OrderByDescending(x => x.Priority)
             .ThenByDescending(x => x.Length)
            .ThenBy(x => x.Start))
        {

            bool overlaps = result.Any(existing =>
                match.Start < existing.Start + existing.Length &&
                match.Start + match.Length > existing.Start);


            if (!overlaps)
            {
                result.Add(match);
            }
        }


        return result.OrderBy(x => x.Start).ToList();
    }
}