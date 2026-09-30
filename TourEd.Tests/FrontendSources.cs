using System.Text.RegularExpressions;

namespace TourEd.Tests;

/// <summary>Reads the bundled frontend modules the way the service worker precaches them.</summary>
internal static class FrontendSources
{
    /// <summary>All frontend modules the service worker precaches, starting with the entry module.</summary>
    public static async Task<IReadOnlyList<string>> GetPrecachedModulesAsync(HttpClient client)
    {
        var worker = await client.GetStringAsync("/service-worker.js");
        return Regex.Matches(worker, @"""(?<file>js/[\w-]+\.js)""")
            .Select(match => match.Groups["file"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The source of every precached frontend module, for contract checks across modules.</summary>
    public static async Task<string> GetScriptAsync(HttpClient client)
    {
        var modules = await GetPrecachedModulesAsync(client);
        var sources = await Task.WhenAll(modules.Select(module => client.GetStringAsync("/" + module)));
        return string.Join("\n", sources);
    }
}
