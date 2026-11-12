"""Embed current browser helpers into an isolated synthetic application form."""

from pathlib import Path
import re
import shutil
import sys


repo = Path(__file__).resolve().parents[2]
fixture = Path(__file__).resolve().parent
if len(sys.argv) != 2:
    raise SystemExit("Usage: build_fixture.py OUTPUT_DIRECTORY")
output = Path(sys.argv[1]).resolve()
output.mkdir(parents=True, exist_ok=True)
for name in ("serve.ps1", "job.html"):
    shutil.copy2(fixture / name, output / name)


def script(name: str) -> str:
    source = (repo / f"src/ApplicationFoundry/Features/Forms/{name}.cs").read_text()
    match = re.search(r'public const string Script = """\n(.*?)\n\s*""";', source, re.S)
    if match is None:
        raise ValueError(f"No bookmarklet script in {name}")
    return match.group(1)


page = f"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>Synthetic application form</title></head>
<body>
  <h1>Synthetic application form</h1>
  <p>Local test fixture. No employer receives this data.</p>
  <form id="application" onsubmit="event.preventDefault(); document.querySelector('#status').textContent='Submission blocked by fixture';">
    <label for="email">Email</label><input id="email" type="email" required>
    <label for="availability">Availability</label>
    <select id="availability" required>
      <option value="">Choose</option><option value="soon">Two weeks</option>
    </select>
    <input type="hidden" value="SYNTHETIC-HIDDEN">
    <input type="submit" value="Submit (blocked by fixture)">
  </form>
  <p><button type="button" onclick="captureStructure()">Copy form structure</button></p>
  <p><button type="button" onclick="fillApproved()">Fill approved packet</button></p>
  <output id="status"></output>
  <script>
    window.alert = message => document.querySelector('#status').textContent = message;
    function captureStructure() {{ {script('EmployerFormSnapshotBookmarklet')} }}
    function fillApproved() {{ {script('EmployerFormFillBookmarklet')} }}
  </script>
</body>
</html>
"""
(output / "apply.html").write_text(page)
print(f"Built {output / 'apply.html'} from current browser helpers")
