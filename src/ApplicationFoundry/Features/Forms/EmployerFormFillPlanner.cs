using ApplicationFoundry.Data;
using System.Globalization;

namespace ApplicationFoundry.Features.Forms;

public sealed record EmployerFormField(string Key, string Label, string Type, bool Required, IReadOnlyList<string>? Options = null);
public sealed record EmployerFormSnapshot(Uri Url, IReadOnlyList<EmployerFormField> Fields, Uri? ConfirmedExternalUrl = null);
public sealed record ProposedFieldValue(string Key, string Label, string Value);
public sealed record FormFillProposal(bool IsComplete, IReadOnlyList<ProposedFieldValue> Values, IReadOnlyList<string> Reasons);

public static class EmployerFormFillPlanner
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "email", "tel", "url", "date", "textarea", "select"
    };

    public static FormFillProposal Prepare(JobOpportunity job, EmployerFormSnapshot form)
    {
        var reasons = new List<string>();
        var values = new List<ProposedFieldValue>();
        if (job.FormPlan is null)
            reasons.Add("Prepare and review answers before filling an employer form.");
        if (job.Status is OpportunityStatus.Submitted or OpportunityStatus.Interviewing or OpportunityStatus.Closed)
            reasons.Add("This opportunity is no longer in a pre-submission state.");
        var safeFormUrl = form.Url.Scheme == Uri.UriSchemeHttps && form.Url.UserInfo.Length == 0;
        var sameOrigin = Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var postingUrl) &&
            postingUrl.Scheme == Uri.UriSchemeHttps &&
            string.Equals(postingUrl.IdnHost, form.Url.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            postingUrl.Port == form.Url.Port;
        var confirmedExternal = form.ConfirmedExternalUrl is { UserInfo: { Length: 0 } } confirmed &&
            confirmed.Scheme == Uri.UriSchemeHttps &&
            string.Equals(confirmed.AbsoluteUri, form.Url.AbsoluteUri, StringComparison.Ordinal);
        if (!safeFormUrl || (!sameOrigin && !confirmedExternal))
            reasons.Add("The form must use the same HTTPS origin as the saved posting, or the applicant must confirm this exact external HTTPS form URL.");
        if (form.Fields.Count is < 1 or > 100)
            reasons.Add("The form must contain between 1 and 100 fields.");

        var answers = new Dictionary<string, ApplicationFormAnswer>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in job.FormPlan?.Answers ?? [])
        {
            var label = Normalize(answer.FieldLabel);
            if (label.Length == 0 || !answers.TryAdd(label, answer))
                reasons.Add($"Prepared answer '{answer.FieldLabel}' has an empty or repeated label.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in form.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 200 || !keys.Add(field.Key))
            {
                reasons.Add("The form contains an empty, repeated, or oversized field key.");
                continue;
            }
            var label = Normalize(field.Label);
            if (label.Length == 0 || field.Label.Length > 200)
            {
                reasons.Add($"Field '{field.Key}' has an invalid label.");
                continue;
            }
            if (!labels.Add(label))
            {
                reasons.Add($"Field label '{field.Label}' is repeated on the form.");
                continue;
            }
            if (!SupportedTypes.Contains(field.Type))
            {
                if (field.Required)
                    reasons.Add($"Required field '{field.Label}' has an unsupported input type.");
                continue;
            }
            if (!answers.TryGetValue(label, out var answer))
            {
                if (field.Required)
                    reasons.Add($"Required field '{field.Label}' needs a reviewed answer.");
                continue;
            }
            if (field.Required && string.IsNullOrWhiteSpace(answer.Answer))
            {
                reasons.Add($"Required field '{field.Label}' has an empty reviewed answer.");
                continue;
            }
            if (field.Type.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                (!Uri.TryCreate(answer.Answer, UriKind.Absolute, out var answerUrl) ||
                 answerUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(answerUrl.UserInfo)))
            {
                reasons.Add($"Reviewed answer for '{field.Label}' must be an HTTPS URL without credentials.");
                continue;
            }
            if (field.Type.Equals("date", StringComparison.OrdinalIgnoreCase) &&
                !DateOnly.TryParseExact(answer.Answer, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
            {
                reasons.Add($"Reviewed answer for '{field.Label}' must be a valid YYYY-MM-DD date.");
                continue;
            }
            if (field.Type.Equals("select", StringComparison.OrdinalIgnoreCase) &&
                (field.Options is null || field.Options.Count(option =>
                    option.Equals(answer.Answer, StringComparison.OrdinalIgnoreCase)) != 1))
            {
                reasons.Add($"Reviewed answer for '{field.Label}' is not one unambiguous available choice.");
                continue;
            }
            values.Add(new ProposedFieldValue(field.Key, field.Label, answer.Answer));
        }
        if (values.Count == 0)
            reasons.Add("No form fields matched reviewed answers.");
        return reasons.Count == 0
            ? new FormFillProposal(true, values, [])
            : new FormFillProposal(false, [], reasons);
    }

    private static string Normalize(string label) =>
        string.Join(' ', label.Trim().TrimEnd(' ', '\t', '*', ':').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
