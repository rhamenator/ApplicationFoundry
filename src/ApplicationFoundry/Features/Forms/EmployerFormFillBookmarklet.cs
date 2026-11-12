namespace ApplicationFoundry.Features.Forms;

public static class EmployerFormFillBookmarklet
{
    // The applicant supplies a short-lived packet after explicit authorization in the app.
    // This script fills fields only. It never clicks a button or submits a form.
    public const string Script = """
        (async () => {
          const fail = reason => { alert('Application Foundry did not fill this form: ' + reason); };
          let raw;
          try { raw = await navigator.clipboard.readText(); }
          catch { raw = prompt('Paste the approved Application Foundry fill packet:'); }
          if (!raw) return fail('No fill packet was provided.');
          let packet;
          try { packet = JSON.parse(raw); }
          catch { return fail('The fill packet is not valid JSON.'); }
          if (!packet || !Number.isInteger(packet.jobId) ||
              typeof packet.authorizationFingerprint !== 'string' ||
              packet.authorizationFingerprint.length !== 64 || packet.url !== location.href ||
              !Array.isArray(packet.fields) || !Array.isArray(packet.values) ||
              packet.values.length < 1 || packet.values.length > 100)
            return fail('The packet does not match this page.');
          const expires = Date.parse(packet.expiresAt);
          if (!Number.isFinite(expires) || expires < Date.now() || expires > Date.now() + 600000)
            return fail('The authorization has expired or has an invalid time.');
          const elements = Array.from(document.querySelectorAll('input,textarea,select'))
            .filter(el => !el.disabled && !['hidden', 'submit', 'button', 'reset', 'image'].includes(el.type));
          const fields = elements.map(el => {
            const type = el.tagName.toLowerCase() === 'input'
              ? (el.type || 'text').toLowerCase() : el.tagName.toLowerCase();
            const field = {
              key: el.id || el.name || '',
              label: (el.labels?.[0]?.textContent || el.getAttribute('aria-label') || '').trim(),
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
          if (JSON.stringify(fields) !== JSON.stringify(packet.fields))
            return fail('The live form changed since it was reviewed.');
          const allowed = new Set(['text', 'email', 'tel', 'textarea', 'select']);
          const seen = new Set();
          const actions = [];
          for (const value of packet.values) {
            if (!value || typeof value.key !== 'string' || typeof value.label !== 'string' ||
                typeof value.value !== 'string' || value.value.length > 4000 || seen.has(value.key))
              return fail('The packet contains an invalid or repeated value.');
            seen.add(value.key);
            const index = fields.findIndex(field => field.key === value.key && field.label === value.label);
            if (index < 0 || !allowed.has(fields[index].type) || elements[index].readOnly)
              return fail('A proposed field is unavailable or read-only.');
            let nextValue = value.value;
            if (fields[index].type === 'select') {
              const matches = Array.from(elements[index].options).filter(option =>
                !option.disabled && option.value !== '' &&
                option.textContent.trim().toLowerCase() === value.value.toLowerCase());
              if (matches.length !== 1) return fail('A proposed choice is unavailable or ambiguous.');
              nextValue = matches[0].value;
            }
            actions.push([elements[index], nextValue, value, fields[index].type]);
          }
          if (fields.some(field => field.required && !seen.has(field.key)))
            return fail('A required field has no reviewed value.');
          for (const [element, value] of actions) {
            element.value = value;
            element.dispatchEvent(new Event('input', { bubbles: true }));
            element.dispatchEvent(new Event('change', { bubbles: true }));
          }
          const observed = [];
          for (const [element, assigned, proposed, type] of actions) {
            if (element.value !== assigned)
              return fail('The page changed a field after filling. Inspect it manually.');
            observed.push({
              key: proposed.key,
              label: proposed.label,
              value: type === 'select' ? element.selectedOptions[0].textContent.trim() : element.value
            });
          }
          const receipt = JSON.stringify({
            jobId: packet.jobId,
            url: packet.url,
            authorizationFingerprint: packet.authorizationFingerprint,
            values: observed
          });
          try {
            await navigator.clipboard.writeText(receipt);
            alert('Application Foundry filled ' + actions.length + ' field(s) and copied an entry receipt. Inspect the form before submitting it yourself, then return to the app to record the values.');
          } catch {
            prompt('Copy this entry receipt into Application Foundry after inspecting the form:', receipt);
          }
        })();
        """;

    public static string Href => "javascript:" + Uri.EscapeDataString(Script);
}
