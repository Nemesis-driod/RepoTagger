using RepoTagger.Security.Redaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepoTagger.Tests
{
    public class FakeGenericRule : IRedactionRule
    {
        public string Name => "GENERIC_SECRET";
        public int Priority => 10;

        public IEnumerable<RedactionMatch> Find(string text)
        {
            yield return new RedactionMatch
            {
                Start = 0,
                Length = text.Length,
                Type = Name,
                Priority = Priority
            };
        }
    }
}
