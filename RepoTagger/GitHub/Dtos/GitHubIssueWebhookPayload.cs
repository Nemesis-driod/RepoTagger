using System.Text.Json.Serialization;

namespace RepoTagger.GitHub.Dtos
{
    public class GitHubIssueWebhookPayload
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("issue")]
        public GitHubIssueDto Issue { get; set; } = new();

        [JsonPropertyName("repository")]
        public GitHubRepositoryDto Repository { get; set; } = new();
        
        [JsonPropertyName("label")]
        public GitHubLabelDto? Label { get; set; }
    }

    public class GitHubLabelDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class GitHubIssueDto
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;


        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
    }


    public class GitHubRepositoryDto
    {
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;
    }
}