using RepoTagger.Security.Rule;
using FluentAssertions;
using Xunit;

namespace RepoTagger.Tests
{
    public class AwsAccessKeyRuleTests
    {

        private readonly AwsAccessKeyRule _rule = new();



        [Fact]
        public void Should_Find_Aws_Key()
        {
            var input =
                "aws key AKIA1234567890ABCDEF";


            var result =
                _rule.Find(input);


            result.Should()
                .ContainSingle();
        }



        [Fact]
        public void Should_Ignore_Invalid_Aws_Key()
        {
            var input =
                "AKIA123";


            _rule.Find(input)
                .Should()
                .BeEmpty();
        }



        [Fact]
        public void Should_Not_Match_Lowercase()
        {
            var input =
                "akia1234567890ABCDEF";


            _rule.Find(input)
                .Should()
                .BeEmpty();
        }
    }
}
