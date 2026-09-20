using RepoTagger.Token;
using System.Net.Http.Headers;

namespace RepoTagger.GitHub
{
    public class GitHubAppAuth
    {
        private readonly IHttpClientFactory _client;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;

        private string? _cachedToken;

        private DateTime _expiresAtUtc;

        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public GitHubAppAuth(IHttpClientFactory client, IConfiguration configuration, TokenService tokenService)
        {
            _client = client;
            _configuration = configuration;
            _tokenService = tokenService;
        }


        public async Task<string> GetTokenAsync(CancellationToken ct = default)
        {
            if (_cachedToken != null &&
                _expiresAtUtc > DateTime.UtcNow.AddMinutes(5))
            {
                return _cachedToken;
            }


            await _refreshLock.WaitAsync(ct);

            try
            {
                if (_cachedToken != null && _expiresAtUtc > DateTime.UtcNow.AddMinutes(5))
                {
                    return _cachedToken;
                }


                var client = _client.CreateClient("GitHubApp");


                var jwt = _tokenService.GenerateToken();
                //Console.WriteLine(jwt);  //test 1


                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", jwt);


                var installationId = _configuration["GitHub:InstallationId"];


                //Console.WriteLine("Refreshing installation token..."); //test 3

                var response = await client.PostAsync($"/app/installations/{installationId}/access_tokens",
                    null,
                    ct);


                response.EnsureSuccessStatusCode();


                var result = await response.Content.ReadFromJsonAsync<GitHubInstallationTokenResponse>(
                        cancellationToken: ct);
                if (result == null)
                {
                    throw new Exception("GitHub token response was empty");
                }



                _cachedToken = result.Token;
                _expiresAtUtc = result.ExpiresAt;


                return _cachedToken;
            }
            finally
            {
                _refreshLock.Release();
            }
        }
    }


}
