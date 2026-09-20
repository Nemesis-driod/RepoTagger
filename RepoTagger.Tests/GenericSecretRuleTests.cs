using RepoTagger.Security.Rule;
using FluentAssertions;
using Xunit;

namespace RepoTagger.Tests
{
    public class GenericSecretRuleTests
    {

        private readonly GenericSecretRule _rule = new();

        [Fact]
        public void Should_Find_Config_Secret()
        {
            var input =
                "api_key=abcdefghijklmnop12345";


            _rule.Find(input)
                .Should()
                .ContainSingle();
        }


        [Fact]
        public void Should_Not_Match_Normal_Text()
        {
            var input =
                "the password field is required";


            _rule.Find(input)
                .Should()
                .BeEmpty();
        }


        [Fact]
        public void Should_Find_Colon_Format()
        {
            var input =
                "token: abcdefghijklmnop12345";


            _rule.Find(input)
                .Should()
                .ContainSingle();
        }
    }
}
