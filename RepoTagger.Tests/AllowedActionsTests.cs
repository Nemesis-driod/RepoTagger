

using RepoTagger.AI.Domain;

using Xunit;

namespace RepoTagger.Tests
{
    public class AllowedActionsTests
    {

        [Theory]
        [InlineData("bug")]
        [InlineData("feature-request")]
        [InlineData("question")]
        [InlineData("needs-human-review")]
        public void Know_label_should_apply(string label)
        {
            var result = AllowedActions.IsAllowedLabel(label);

            Assert.True(result);
        }


        [Theory]
        [InlineData("security")]
        [InlineData("critical")]
        [InlineData("delete-repo")]
        [InlineData("")]
        public void Unknown_label_should_not_apply(string label)
        {
            var result = AllowedActions.IsAllowedLabel(label);

            Assert.False(result);
        }



    }
}
