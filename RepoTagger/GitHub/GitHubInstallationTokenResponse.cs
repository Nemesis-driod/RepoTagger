using System.Text.Json.Serialization;

namespace RepoTagger.GitHub
{
    public class GitHubInstallationTokenResponse
    {
        public string Token { get; set; } = string.Empty;


        [JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }
}
