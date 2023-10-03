using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TourEd.Lib.Abstractions.Interfaces.Services;

namespace TourEd.Lib.Services;

public partial class HtmlParsingService : IHtmlParsingService
{
    private readonly HttpClient _client;
    private readonly ConcurrentDictionary<string, string> _htmlContents = new(StringComparer.InvariantCultureIgnoreCase);

    public HtmlParsingService(HttpClient client)
    {
        _client = client;
    }
    
    public async Task<string?> GetRawDmoStringAsync(Uri uri)
    {
        var body = await GetBodyAsync(_client, uri);
        var match = GetDmoRegex().Match(body);
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            return null;
        }

        return Regex.Unescape(match.Groups[1].Value);
    }

    private async Task<string> GetBodyAsync(HttpClient client, Uri uri)
    {
        if (_htmlContents.TryGetValue(uri.ToString(), out var body))
        {
            return body;
        }
        var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        _htmlContents.TryAdd(uri.ToString(), result);
        return result;
    }

    [GeneratedRegex("var dmos = \"(.*)\"")]
    private static partial Regex GetDmoRegex();
}