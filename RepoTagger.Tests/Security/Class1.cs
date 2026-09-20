using FluentAssertions;
using RepoTagger.Security.Redaction;
using RepoTagger.Security.Rule;
using Xunit;

namespace RepoTagger.Tests.Security;

public class GenericSecretRuleTests
{
    private readonly GenericSecretRule _rule = new();

    [Theory]
    [InlineData("aws_secret_access_key: removed_due_to_github_not_allowing_change_it_later_when_you_test")]
    [InlineData("aws_secret_access_key=removed_due_to_github_not_allowing_change_it_later_when_you_test")]
    [InlineData("\"stripe_api_key_live\": \"removed_due_to_github_not_allowing_change_it_later_when_you_test\"")]
    [InlineData("db_password_hash: removed_due_to_github_not_allowing_change_it_later_when_you_test")]
    public void Find_DetectsCompoundIdentifierNamesWithBase64Values(string text)
    {
        _rule.Find(text).Should().NotBeEmpty();
    }

    [Fact]
    public void Find_DoesNotFlagOrdinaryProseContainingTriggerWords()
    {
        var text = "My secret: I like pizza. There's no password here, just a conversation about tokens of appreciation.";
        _rule.Find(text).Should().BeEmpty();
    }
}