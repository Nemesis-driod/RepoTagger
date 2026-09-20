using RepoTagger.Security.Rule;
using FluentAssertions;
using Xunit;

namespace RepoTagger.Tests
{
    public class EmailRuleTests
    {

        private readonly EmailRule _rule = new();



        [Fact]
        public void Should_Redact_Email()
        {
            var input =
                "contact me admin@example.com";


            _rule.Find(input)
                .Should()
                .ContainSingle();
        }



        [Theory]
        [InlineData("hello@test")]
        [InlineData("@example.com")]
        [InlineData("user@")]
        public void Should_Ignore_Invalid_Email(string input)
        {
            _rule.Find(input)
                .Should()
                .BeEmpty();
        }
    }
}
