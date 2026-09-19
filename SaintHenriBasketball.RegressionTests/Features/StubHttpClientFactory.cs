using System.Net;

/// An IHttpClientFactory whose clients answer from a canned table of URL fragment -> response, so a
/// check can drive what Google appears to say without touching the network.
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly List<(string Contains, HttpStatusCode Status, string Body)> _routes = new();

    public List<string> Requested { get; } = new();

    public StubHttpClientFactory When(string urlContains, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((urlContains, status, body));
        return this;
    }

    public HttpClient CreateClient(string name = "") => new(new StubHandler(this));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly StubHttpClientFactory _owner;
        public StubHandler(StubHttpClientFactory owner) => _owner = owner;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            _owner.Requested.Add(url);

            foreach (var (contains, status, body) in _owner._routes)
            {
                if (url.Contains(contains, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
            }

            // An unstubbed call is a test gap, not a pass.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        }
    }
}
