using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Oberon.Extended;

internal static class ExtendedVersion
{
    internal const string Number = "1.0.0-preview.7";
    internal const string Project = "https://github.com/katakishi0/OberonRemote";
    internal const string Releases = Project + "/releases";
    internal static async Task<(string Status, string? Url)> CheckForUpdatesAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OberonExtended", Number));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // The active preview is intentionally marked as a prerelease on GitHub,
        // so /releases/latest would not return it.
        using var response = await http.GetAsync(
            "https://api.github.com/repos/katakishi0/OberonRemote/releases?per_page=10");
        response.EnsureSuccessStatusCode();
        using var data = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        if (data.RootElement.ValueKind != JsonValueKind.Array ||
            data.RootElement.GetArrayLength() == 0)
            return ("Nenhuma versão publicada ainda.", null);

        var released = data.RootElement.EnumerateArray().FirstOrDefault(
            element => !element.GetProperty("draft").GetBoolean());
        if (released.ValueKind != JsonValueKind.Object)
            return ("Nenhuma versão publicada ainda.", null);
        string tag = released.GetProperty("tag_name").GetString() ?? "";
        string url = released.GetProperty("html_url").GetString() ?? Releases;
        string version = tag.TrimStart('v', 'V');
        string current = Number;
        if (string.Equals(version,current,StringComparison.OrdinalIgnoreCase))
            return ("Você está usando a versão publicada: " + tag, url);
        // Compare stable version numbers first; previews of equal numeric versions
        // are reported as a published alternative rather than a false update.
        string plainVersion = version.Split('-')[0];
        string plainCurrent = current.Split('-')[0];
        if (Version.TryParse(plainVersion,out var remote) &&
            Version.TryParse(plainCurrent,out var local) && remote > local)
            return ("Nova versão disponível: " + tag, url);
        return ("Versão publicada: " + tag, url);
    }
    internal static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
