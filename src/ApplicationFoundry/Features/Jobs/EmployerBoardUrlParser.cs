using System.Text.RegularExpressions;

namespace ApplicationFoundry.Features.Jobs;

public sealed record EmployerBoard(string Source, string Token, string Region);

public static partial class EmployerBoardUrlParser
{
    public static bool TryParse(string? input, out EmployerBoard? board)
    {
        board = null;
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort
            || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Fragment))
            return false;

        var source = url.Host.ToLowerInvariant() switch
        {
            "boards.greenhouse.io" => "greenhouse",
            "jobs.lever.co" or "jobs.eu.lever.co" => "lever",
            _ => null
        };
        if (source is null) return false;

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !TokenPattern().IsMatch(parts[0])) return false;
        board = new EmployerBoard(source, parts[0], url.Host.Equals("jobs.eu.lever.co", StringComparison.OrdinalIgnoreCase) ? "eu" : "global");
        return true;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
