
using RepoTagger.GitHub.Dtos;

namespace RepoTagger.GitHub
{
    public class GitHubIssueClient : IGitHubIssueClient
    {

        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<GitHubIssueClient> _logger;

        public GitHubIssueClient(IHttpClientFactory clientFactory , ILogger<GitHubIssueClient> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async  Task AddCommentAsync(string repo, int issueNumber, string comment, CancellationToken ct)
        {
            var client = _clientFactory.CreateClient("GitHub");

            var payload = new
            {
                body = comment
            };

           var response = await client.PostAsJsonAsync(   $"repos/{repo}/issues/{issueNumber}/comments", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);

                _logger.LogError("Failed adding comment to {Repo} issue {Issue}. Status={Status}. Error={Error}",
                    repo, issueNumber, response.StatusCode, error);

                throw new HttpRequestException(  $"GitHub comment failed: {error}");

            }
            _logger.LogInformation(  "Comment added to {Repo} issue {Issue}",  repo,  issueNumber);

        }

        public async Task AddLabelAsync(string repo, int issueNumber, string label, CancellationToken ct)
        {
            var client = _clientFactory.CreateClient("GitHub");


            var payload = new
            {
                labels = new[]
                {
                    label
                }
            };

            var response = await client.PostAsJsonAsync( $"repos/{repo}/issues/{issueNumber}/labels", payload,   ct);


            if (!response.IsSuccessStatusCode)
            {
                var error =   await response.Content.ReadAsStringAsync(ct);


                _logger.LogError(
                    "Failed adding label {Label} to {Repo} issue {Issue}. Status={Status}. Error={Error}",
                    label, repo,   issueNumber, response.StatusCode,error);


                throw new HttpRequestException(  $"GitHub label failed: {error}");
            }


            _logger.LogInformation(   "Label {Label} added to {Repo} issue {Issue}",  label,repo,  issueNumber);
        }


        public async Task<string> GetIssueStateAsync(string repo , int issueNumber , CancellationToken ct)
        {
            var client = _clientFactory.CreateClient("GitHub");
            var response = await client.GetAsync($"repos/{repo}/issues/{issueNumber}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed fetching issue state for {Repo} issue {Issue}. Status={Status}. Error={Error}",
                    repo, issueNumber, response.StatusCode, error);
                throw new HttpRequestException($"GitHub get issue failed: {error}");
            }
            var issue = await response.Content.ReadFromJsonAsync<GitHubIssueDto>(cancellationToken: ct);
            return issue?.State ?? throw new InvalidOperationException("GitHub issue response missing state.");
        }
    
    }
}
