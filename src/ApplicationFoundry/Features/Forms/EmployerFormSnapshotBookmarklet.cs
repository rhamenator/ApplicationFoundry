namespace ApplicationFoundry.Features.Forms;

public static class EmployerFormSnapshotBookmarklet
{
    // This runs only when the applicant invokes the bookmarklet on an employer page.
    // It reads form structure, never input values, and sends no network request.
    public const string Script = """
        (() => {
          const fields = Array.from(document.querySelectorAll('input,textarea,select'))
            .filter(el => !el.disabled && !['hidden', 'submit', 'button', 'reset', 'image'].includes(el.type))
            .map(el => {
              const type = el.tagName.toLowerCase() === 'input'
                ? (el.type || 'text').toLowerCase()
                : el.tagName.toLowerCase();
              const label = (el.labels?.[0]?.textContent || el.getAttribute('aria-label') || '').trim();
              const field = {
                key: el.id || el.name || '',
                label,
                type,
                required: el.required === true
              };
              if (type === 'select') {
                field.options = Array.from(el.options)
                  .filter(option => !option.disabled && option.value !== '')
                  .map(option => option.textContent.trim());
              }
              return field;
            });
          const text = JSON.stringify({ url: location.href, fields });
          Promise.resolve()
            .then(() => navigator.clipboard.writeText(text))
            .then(() => alert('Form structure copied. Paste it into Application Foundry.'))
            .catch(() => prompt('Copy this form structure into Application Foundry:', text));
        })();
        """;

    public static string Href => "javascript:" + Uri.EscapeDataString(Script);
}
