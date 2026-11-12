using ApplicationFoundry.Data;

namespace ApplicationFoundry.Features.Forms;

public sealed record EmployerFormField(string Key, string Label, string Type, bool Required, IReadOnlyList<string>? Options = null);
public sealed record EmployerFormSnapshot(Uri Url, IReadOnlyList<EmployerFormField> Fields);
public sealed record ProposedFieldValue(string Key, string Label, string Value);
public sealed record FormFillProposal(bool IsComplete, IReadOnlyList<ProposedFieldValue> Values, IReadOnlyList<string> Reasons);

public static class EmployerFormFillPlanner
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "email", "tel", "textarea", "select"
    };

    public static FormFillProposal Prepare(JobOpportunity job, EmployerFormSnapshot form)
    {
        var reasons = new List<string>();
        var values = new List<ProposedFieldValue>();
        if (job.FormPlan is null)
            reasons.Add("Prepare and review answers before filling an employer form.");
        if (job.Status is OpportunityStatus.Submitted or OpportunityStatus.Interviewing or OpportunityStatus.Closed)
            reasons.Add("This opportunity is no longer in a pre-submission state.");
        if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var postingUrl) ||
            postingUrl.Scheme != Uri.UriSchemeHttps || form.Url.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(postingUrl.IdnHost, form.Url.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            postingUrl.Port != form.Url.Port)
            reasons.Add("The form must use the same HTTPS origin as the saved posting.");
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
            if (field.Type.Equals("select", StringComparison.OrdinalIgnoreCase) &&
                (field.Options is null || !field.Options.Contains(answer.Answer, StringComparer.OrdinalIgnoreCase)))
            {
                reasons.Add($"Reviewed answer for '{field.Label}' is not an available choice.");
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
