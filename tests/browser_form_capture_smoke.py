"""Run the actual bookmarklet script against a synthetic Chromium form fixture."""

import html
import json
import re
import shutil
import subprocess
import tempfile
from pathlib import Path


repo = Path(__file__).resolve().parents[1]
chromium = shutil.which("chromium")
if chromium is None:
    raise SystemExit("Chromium is required for this focused browser smoke test.")

source = (repo / "src/ApplicationFoundry/Features/Forms/EmployerFormSnapshotBookmarklet.cs").read_text()
match = re.search(r'public const string Script = """\n(.*?)\n\s*""";', source, re.S)
assert match is not None

with tempfile.TemporaryDirectory(dir=repo) as directory:
    fixture = Path(directory, "form.html")
    fixture.write_text(
        '<!doctype html><meta charset="utf-8"><form>'
        '<label for="email">Email *</label><input id="email" type="email" required value="SECRET-EMAIL">'
        '<label for="availability">Availability</label><select id="availability" required>'
        '<option value="">Choose</option><option value="soon">Two weeks</option></select>'
        '<input id="hidden" type="hidden" value="SECRET-HIDDEN">'
        '<input type="submit" value="Send">'
        '</form><output id="capture"></output><script>'
        "Object.defineProperty(navigator,'clipboard',{value:{writeText:text=>{"
        "document.querySelector('#capture').textContent=text;return Promise.resolve();}}});"
        "window.alert=()=>{};" + match.group(1) + "</script>"
    )
    result = subprocess.run(
        [chromium, "--headless=new", "--no-sandbox", "--disable-gpu",
         "--disable-background-networking", "--no-first-run",
         "--user-data-dir=" + str(Path(directory, "profile")),
         "--dump-dom", fixture.as_uri()],
        capture_output=True, text=True, timeout=25, check=True,
    )
    captured = re.search(r'<output id="capture">([^<]+)</output>', result.stdout)
    assert captured is not None, "Bookmarklet did not copy a snapshot"
    snapshot = json.loads(html.unescape(captured.group(1)))
    assert [field["key"] for field in snapshot["fields"]] == ["email", "availability"]
    assert snapshot["fields"][0]["label"] == "Email *"
    assert snapshot["fields"][1]["options"] == ["Two weeks"]
    assert "SECRET-" not in json.dumps(snapshot), "Input values leaked into the snapshot"

print("Synthetic Chromium capture passed: fields and choices copied without input values.")
