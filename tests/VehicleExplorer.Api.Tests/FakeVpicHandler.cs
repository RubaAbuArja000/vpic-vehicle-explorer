using System.Net;
using System.Text;

namespace VehicleExplorer.Api.Tests;

/// <summary>Stub HTTP handler that maps a URL fragment to a canned vPIC JSON body and counts calls.</summary>
internal sealed class FakeVpicHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _routes = new();

    public List<string> Requests { get; } = [];

    public FakeVpicHandler Respond(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[urlContains] = (status, json);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // AbsoluteUri keeps percent-encoding as sent on the wire (ToString() unescapes it).
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add(url);

        var match = _routes.FirstOrDefault(r => url.Contains(r.Key, StringComparison.OrdinalIgnoreCase));
        var (status, body) = match.Key is null ? (HttpStatusCode.NotFound, "") : match.Value;

        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    public static string Results(string resultsJsonArray) =>
        $$"""{"Count":0,"Message":"Response returned successfully","SearchCriteria":null,"Results":{{resultsJsonArray}}}""";
}
