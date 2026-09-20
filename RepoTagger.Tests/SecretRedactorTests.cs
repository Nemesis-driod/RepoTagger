//using FluentAssertions;
//using RepoTagger.AI;
//using RepoTagger.Security.Redaction;
//using RepoTagger.Security.Rule;
//using Xunit;

//namespace RepoTagger.Tests;


//public class SecretRedactorTests
//{

//    private readonly SecretRedactor _redactor;


//    public SecretRedactorTests()
//    {
//        var rules = new List<IRedactionRule>
//        {
//            new GitHubTokenRule()
//        };


//        _redactor = new SecretRedactor(rules);
//    }



//    [Fact]
//    public void Should_Redact_Github_Token()
//    {
//        // Arrange
//        var input =
//            "My github token is ghp_123456789012345678901234567890123456";


//        // Act
//        var result = _redactor.Redact(input);


//        // Assert
//        result.Text.Should()
//            .Contain("[REDACTED:GITHUB_TOKEN]");


//        result.Findings.Should()
//            .ContainSingle();


//        result.Findings[0].Type
//            .Should()
//            .Be("GITHUB_TOKEN");
//    }

//    [Fact]
//    public void Should_Not_Redact_Normal_Text()
//    {
//        var input =
//            "The token bucket algorithm is used for rate limiting";


//        var result = _redactor.Redact(input);


//        result.Text.Should()
//            .Be(input);


//        result.Findings.Should()
//            .BeEmpty();
//    }

//    [Fact]
//    public void Should_Redact_Multiple_Tokens()
//    {
//        var input =
//            """
//        First:
//        ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa

//        Second:
//        ghp_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
//        """;


//        var result = _redactor.Redact(input);


//        result.Text
//            .Should()
//            .NotContain("ghp_");


//        result.Findings
//            .Single(x => x.Type == "GITHUB_TOKEN")
//            .Count
//            .Should()
//            .Be(2);
//    }

//    [Theory]
//    [InlineData("ghp_")]
//    [InlineData("ghp_hello")]
//    [InlineData("not_a_github_token")]
//    public void Should_Not_Redact_Invalid_Github_Token(string input)
//    {
//        var result = _redactor.Redact(input);


//        result.Text.Should()
//            .Be(input);
//    }

//    [Fact]
//    public void Should_Handle_Null_Input()
//    {
//        var result = _redactor.Redact(null);


//        result.Text.Should()
//            .BeEmpty();


//        result.Findings.Should()
//            .BeEmpty();
//    }

//    [Fact]
//    public void Should_Redact_Github_Refresh_Token()
//    {
//        var input =
//            "refresh token: ghr_abcdefghijklmnopqrstuvwxyz123456";


//        var result = _redactor.Redact(input);


//        result.Text.Should()
//            .Contain("[REDACTED:GITHUB_TOKEN]");
//    }


//    [Fact]
//    public void Higher_Priority_Rule_Should_Win()
//    {
//        var rules = new List<IRedactionRule>
//    {
//        new GitHubTokenRule(),
//        new FakeGenericRule()
//    };


//        var redactor = new SecretRedactor(rules);


//        var input =
//            "ghp_abcdefghijklmnopqrstuvwxyz123456";


//        var result = redactor.Redact(input);


//        result.Findings
//            .Should()
//            .Contain(x => x.Type == "GITHUB_TOKEN");


//        result.Findings
//            .Should()
//            .NotContain(x => x.Type == "GENERIC_SECRET");
//    }

//    [Fact]
//    public void Should_Redact_Mixed_Realistic_Issue_Body()
//    {
//        var redactor = new SecretRedactor(new List<IRedactionRule>
//    {
//        new GitHubTokenRule(), new AwsAccessKeyRule(),
//        new EmailRule(), new GenericSecretRule(), new PrivateKeyRule()
//    });
//        var input = """
//        Contact me at dev@example.com if this fails.
//        My token=ghp_abcdefghijklmnopqrstuvwxyz123456 stopped working.
//        AWS key: AKIA1234567890ABCDEF
//        """;
//        var result = redactor.Redact(input);
//        result.Text.Should().NotContain("dev@example.com");
//        result.Text.Should().NotContain("AKIA1234567890ABCDEF");
//        result.Text.Should().NotContain("ghp_");
//        result.Findings.Select(f => f.Type).Should()
//            .Contain(new[] { "EMAIL", "GITHUB_TOKEN", "AWS_ACCESS_KEY" });
//    }



//    [Fact]
//    public async Task FakeClassifier_ReturnsExpectedResult()
//    {
//        // Arrange
//        var classifier = new FakeIssueClassifier();
//        var request = new IssueClassificationRequest(
//            "Application crashes",
//            "When I click save, application throws exception"
//        );

//        // Act
//        var result = await classifier.ClassifyAsync(request);

//        // Assert
//        Assert.Equal("bug", result.Label);
//        Assert.Equal(0.95, result.Confidence);
//        Assert.Equal("Fake classifier result", result.Reason);
//    }


//    [Fact]
//    public async Task FakeClassifier_ReturnsExpectedResults()
//    {
//        // Arrange
//        var classifier = new FakeIssueClassifier();

//        var request = new IssueClassificationRequest(
//     "Application crashes",
//     "When I click save, application throws exception"
// );

//        // Act
//        var result = await classifier.ClassifyAsync(request);

//        // Assert
//        Assert.Equal("bug", result.Label);
//        Assert.Equal(0.95, result.Confidence);
//        Assert.Equal("Fake classifier result", result.Reason);
//    }
//}