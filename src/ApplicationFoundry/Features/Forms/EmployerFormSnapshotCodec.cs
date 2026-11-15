using System.Text.Json;

namespace ApplicationFoundry.Features.Forms;

public static class EmployerFormSnapshotCodec
{
    public const int MaxJsonCharacters = 65536;

    public static bool TryParse(string? json, out EmployerFormSnapshot? snapshot, out string error)
    {
        snapshot = null;
        error = "The form snapshot is invalid.";
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonCharacters)
            return false;

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 6 });
            var root = document.RootElement;
            if (!HasOnly(root, "url", "fields") ||
                !TryString(root, "url", 2048, out var urlText) ||
                !Uri.TryCreate(urlText, UriKind.Absolute, out var url) ||
                url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length > 0 ||
                !root.TryGetProperty("fields", out var elements) ||
                elements.ValueKind != JsonValueKind.Array || elements.GetArrayLength() is < 1 or > 100)
                return false;

            var fields = new List<EmployerFormField>();
            foreach (var element in elements.EnumerateArray())
            {
                if (!HasOnly(element, "key", "label", "type", "required", "options") ||
                    !TryString(element, "key", 200, out var key) ||
                    !TryString(element, "label", 200, out var label) ||
                    !TryString(element, "type", 30, out var type) ||
                    !element.TryGetProperty("required", out var requiredElement) ||
                    requiredElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return false;

                List<string>? options = null;
                if (element.TryGetProperty("options", out var optionElements))
                {
                    if (optionElements.ValueKind != JsonValueKind.Array || optionElements.GetArrayLength() > 100)
                        return false;
                    options = [];
                    foreach (var option in optionElements.EnumerateArray())
                    {
                        if (option.ValueKind != JsonValueKind.String ||
                            option.GetString() is not { Length: > 0 and <= 200 } optionText)
                            return false;
                        options.Add(optionText);
                    }
                }
                fields.Add(new EmployerFormField(key, label, type, requiredElement.GetBoolean(), options));
            }

            snapshot = new EmployerFormSnapshot(url, fields);
            error = "";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryString(JsonElement element, string name, int maxLength, out string value)
    {
        value = "";
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String ||
            property.GetString() is not { Length: > 0 } text || text.Length > maxLength ||
            string.IsNullOrWhiteSpace(text))
            return false;
        value = text;
        return true;
    }

    private static bool HasOnly(JsonElement element, params string[] names) =>
        element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().All(property => names.Contains(property.Name, StringComparer.Ordinal)) &&
        names.Where(name => element.TryGetProperty(name, out _)).Count() == element.EnumerateObject().Count();
}
