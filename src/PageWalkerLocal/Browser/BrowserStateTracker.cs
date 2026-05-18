using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Browser;

public sealed class BrowserStateTracker
{
    private string? _initialDomain;

    public BrowserSnapshot Observe(PerceptionState state)
    {
        var url = ExtractUrl(state);
        var domain = TryGetDomain(url);
        _initialDomain ??= domain;
        var pageKey = !string.IsNullOrWhiteSpace(url)
            ? $"url:{url}"
            : $"{state.WindowTitle}|{state.TextHash}|{state.ScreenshotHash}";

        return new BrowserSnapshot(url, domain, _initialDomain, pageKey);
    }

    public string BuildPageKey(PerceptionState state) => Observe(state).PageKey;

    public static bool IsCurrentDomainAllowed(BrowserSnapshot snapshot, AppConfig config)
    {
        if (config.AllowExternalNavigation || snapshot.CurrentDomain is null)
        {
            return true;
        }

        if (config.AllowedDomains.Count > 0)
        {
            return config.AllowedDomains.Any(domain => DomainMatches(snapshot.CurrentDomain, domain));
        }

        return snapshot.InitialDomain is null || DomainMatches(snapshot.CurrentDomain, snapshot.InitialDomain);
    }

    private static string? ExtractUrl(PerceptionState state)
    {
        var address = state.OfKind(CandidateKind.AddressBar)
            .OrderByDescending(candidate => candidate.Confidence)
            .Select(candidate => candidate.Text)
            .FirstOrDefault(text => Uri.TryCreate(NormalizeUrlToken(text), UriKind.Absolute, out _));

        return address is null ? null : NormalizeUrlToken(address);
    }

    private static string? TryGetDomain(string? url)
    {
        if (url is null)
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? NormalizeHost(uri.Host) : null;
    }

    private static string NormalizeUrlToken(string text)
    {
        var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            ?? text.Trim();

        token = token.TrimEnd('.', ',', ';', ')', ']', '"', '\'');
        return token.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + token : token;
    }

    private static bool DomainMatches(string host, string configured)
    {
        configured = NormalizeHost(configured);
        return string.Equals(host, configured, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + configured, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHost(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(host, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : host;
        }

        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}

public sealed record BrowserSnapshot(string? CurrentUrl, string? CurrentDomain, string? InitialDomain, string PageKey);
