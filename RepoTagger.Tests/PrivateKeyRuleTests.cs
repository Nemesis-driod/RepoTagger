using RepoTagger.Security.Rule;
using FluentAssertions;
using Xunit;

namespace RepoTagger.Tests
{
    public class PrivateKeyRuleTests
    {

        private readonly PrivateKeyRule _rule = new();



        [Fact]
        public void Should_Redact_Private_Key_Block()
        {
            var input =
            """
        config:

        -----BEGIN PRIVATE KEY-----
        abcdefghijklmnop
        -----END PRIVATE KEY-----

        end
        """;


            var result =
                _rule.Find(input).ToList();


            result.Should()
                .ContainSingle();


            result[0].Type
                .Should()
                .Be("PRIVATE_KEY");
        }



        [Fact]
        public void Should_Not_Match_Normal_Text()
        {
            var input =
                "private key is stored safely";


            var result =
                _rule.Find(input);


            result.Should()
                .BeEmpty();
        }



        [Fact]
        public void Should_Handle_RSA_Key()
        {
            var input =
            """
        -----BEGIN RSA PRIVATE KEY-----
        data
        -----END RSA PRIVATE KEY-----
        """;


            _rule.Find(input)
                .Should()
                .ContainSingle();
        }
    }
}