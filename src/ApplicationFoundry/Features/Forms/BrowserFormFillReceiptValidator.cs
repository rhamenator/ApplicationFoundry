using System.Text.Json;

namespace ApplicationFoundry.Features.Forms;

public static class BrowserFormFillReceiptValidator
{
    public const int MaxJsonCharacters = 512000;

    public static bool TryMatch(string? json, BrowserFormFillPacket packet,
        out IReadOnlyList<ProposedFieldValue> entered, out string error)
    {
        entered = [];
        error = "The browser entry receipt is invalid or does not match the approved fill packet.";
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonCharacters)
            return false;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 5 });
            var root = document.RootElement;
            if (!HasExactProperties(root, "jobId", "url", "authorizationFingerprint", "values") ||
                !root.TryGetProperty("jobId", out var jobId) || jobId.ValueKind != JsonValueKind.Number ||
                !jobId.TryGetInt32(out var id) || id != packet.JobId ||
                !TryString(root, "url", 2048, out var url) || url != packet.Url ||
                !TryString(root, "authorizationFingerprint", 64, out var fingerprint) ||
                fingerprint != packet.AuthorizationFingerprint ||
                !root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array ||
                values.GetArrayLength() != packet.Values.Count)
                return false;

            var result = new List<ProposedFieldValue>();
            for (var index = 0; index < packet.Values.Count; index++)
            {
                var item = values[index];
                var expected = packet.Values[index];
                if (!HasExactProperties(item, "key", "label", "value") ||
                    !TryString(item, "key", 200, out var key) || key != expected.Key ||
                    !TryString(item, "label", 200, out var label) || label != expected.Label ||
                    !TryString(item, "value", 4000, out var value))
                    return false;
                var field = packet.Fields.FirstOrDefault(field => field.Key == key);
                if (field is null)
                    return false;
                var type = field.Type;
                if (!string.Equals(value, expected.Value, type.Equals("select", StringComparison.OrdinalIgnoreCase)
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    return false;
                result.Add(new ProposedFieldValue(key, label, value));
            }

            entered = result;
            error = "";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasExactProperties(JsonElement element, params string[] names) =>
        element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Count() == names.Length &&
        element.EnumerateObject().All(property => names.Contains(property.Name, StringComparer.Ordinal));

    private static bool TryString(JsonElement element, string name, int maxLength, out string value)
    {
        value = "";
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String ||
            property.GetString() is not { Length: > 0 } text || text.Length > maxLength)
            return false;
        value = text;
        return true;
    }
}
