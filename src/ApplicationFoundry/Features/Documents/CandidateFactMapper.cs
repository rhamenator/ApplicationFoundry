using System.Text.RegularExpressions;

namespace ApplicationFoundry.Features.Documents;

public sealed record CandidateFactSuggestions(string Email, string Phone, string Summary, List<string> Claims);

public static partial class CandidateFactMapper
{
    public static CandidateFactSuggestions Suggest(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(line => line.Trim()).ToArray();
        var email = EmailPattern().Match(text).Value;
        var phone = lines.Select(line => PhonePattern().Match(line).Value)
            .FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? "";
        var summary = "";
        for (var index = 0; index < lines.Length; index++)
        {
            if (!SummaryHeadingPattern().IsMatch(lines[index])) continue;
            summary = string.Join(' ', lines.Skip(index + 1).TakeWhile(line =>
                !string.IsNullOrEmpty(line) && !SectionHeadingPattern().IsMatch(line)).Take(4));
            break;
        }

        var claims = lines.Where(line => line.StartsWith('•') || line.StartsWith("- ") || line.StartsWith("* "))
            .Select(line => line.TrimStart('•', '-', '*', ' '))
            .Where(line => line.Length is >= 12 and <= 240)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
        return new CandidateFactSuggestions(email, phone, summary[..Math.Min(summary.Length, 4000)], claims);
    }

    [GeneratedRegex(@"(?<![\w.+-])[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}(?![\w.-])")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\d)(?:\+1[ .-]?)?\(?\d{3}\)?[ .-]?\d{3}[ .-]?\d{4}(?!\d)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^(professional )?(summary|profile|overview):?$", RegexOptions.IgnoreCase)]
    private static partial Regex SummaryHeadingPattern();

    [GeneratedRegex(@"^[A-Z][A-Za-z /&]{2,40}:?$")]
    private static partial Regex SectionHeadingPattern();
}
