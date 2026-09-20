using System.Net.Http.Headers;

namespace RepoTagger.GitHub
{
    public class GitHubAuthorizationHandler : DelegatingHandler
    {
        private readonly GitHubAppAuth _auth;

        public GitHubAuthorizationHandler(
            GitHubAppAuth auth)
        {
            _auth = auth;
        }


        protected override async Task<HttpResponseMessage> SendAsync( HttpRequestMessage request,CancellationToken ct)
        {

            var token = await _auth.GetTokenAsync(ct);

            //        await Task.WhenAll(
            //_auth.GetTokenAsync(),
            //_auth.GetTokenAsync(),
            //_auth.GetTokenAsync(),
            //_auth.GetTokenAsync(),
            //_auth.GetTokenAsync());

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);

            return await base.SendAsync(request, ct);
        }
    }
}
