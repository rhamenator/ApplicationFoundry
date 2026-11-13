"""Exercise the browser fill script on synthetic forms without submitting them."""

import html
import json
import re
import shutil
import subprocess
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path


repo = Path(__file__).resolve().parents[1]
chromium = shutil.which("chromium")
if chromium is None:
    raise SystemExit("Chromium is required for this focused browser smoke test.")
source = (repo / "src/ApplicationFoundry/Features/Forms/EmployerFormFillBookmarklet.cs").read_text()
match = re.search(r'public const string Script = """\n(.*?)\n\s*""";', source, re.S)
assert match is not None


def run_fixture(directory: Path, changed: bool, invalid_url: bool = False, invalid_date: bool = False) -> dict:
    variant = "invalid-date" if invalid_date else "invalid-url" if invalid_url else "changed" if changed else "matching"
    fixture = (directory / (variant + ".html")).resolve()
    label = "Email changed" if changed else "Email"
    packet = {
        "jobId": 42,
        "url": fixture.as_uri(),
        "authorizationFingerprint": "A" * 64,
        "fields": [
            {"key": "email", "label": "Email", "type": "email", "required": True},
            {"key": "availability", "label": "Availability", "type": "select",
             "required": True, "options": ["Two weeks"]},
            {"key": "portfolio", "label": "Portfolio", "type": "url", "required": True},
            {"key": "start", "label": "Start date", "type": "date", "required": True},
        ],
        "values": [
            {"key": "email", "label": "Email", "value": "avery@example.test"},
            {"key": "availability", "label": "Availability", "value": "Two weeks"},
            {"key": "portfolio", "label": "Portfolio", "value": "http://portfolio.example.test/avery" if invalid_url else "https://portfolio.example.test/avery"},
            {"key": "start", "label": "Start date", "value": "2026-02-30" if invalid_date else "2026-12-01"},
        ],
        "expiresAt": (datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat(),
    }
    raw = json.dumps(packet)
    fixture.write_text(
        '<!doctype html><meta charset="utf-8"><form id="application">'
        f'<label for="email">{label}</label><input id="email" type="email" required>'
        '<label for="availability">Availability</label><select id="availability" required>'
        '<option value="">Choose</option><option value="soon">Two weeks</option></select>'
        '<label for="portfolio">Portfolio</label><input id="portfolio" type="url" required>'
        '<label for="start">Start date</label><input id="start" type="date" required>'
        '<button type="submit">Submit</button></form><output id="capture"></output><script>'
        'window.submissions=0;document.querySelector("form").addEventListener("submit",'
        'event=>{event.preventDefault();window.submissions++});'
        'window.receipt=null;Object.defineProperty(navigator,"clipboard",{value:{'
        'readText:()=>Promise.resolve(' + json.dumps(raw) + '),'
        'writeText:text=>{window.receipt=text;return Promise.resolve();}}});'
        'window.alert=message=>{document.querySelector("#capture").textContent=JSON.stringify('
        '{message,email:document.querySelector("#email").value,'
        'availability:document.querySelector("#availability").value,'
        'portfolio:document.querySelector("#portfolio").value,'
        'start:document.querySelector("#start").value,'
        'submissions:window.submissions,receipt:window.receipt})};'
        + match.group(1) + '</script>'
    )
    result = subprocess.run(
        [chromium, "--headless=new", "--no-sandbox", "--disable-gpu",
         "--disable-background-networking", "--no-first-run",
         "--user-data-dir=" + str(directory / ("profile-" + variant)),
         "--dump-dom", fixture.as_uri()],
        capture_output=True, text=True, timeout=25, check=True,
    )
    captured = re.search(r'<output id="capture">([^<]+)</output>', result.stdout)
    assert captured is not None, "Fill script did not report a result"
    return json.loads(html.unescape(captured.group(1)))


with tempfile.TemporaryDirectory(dir=repo) as directory:
    folder = Path(directory)
    matching = run_fixture(folder, False)
    assert matching["email"] == "avery@example.test", matching
    assert matching["availability"] == "soon", matching
    assert matching["portfolio"] == "https://portfolio.example.test/avery", matching
    assert matching["start"] == "2026-12-01", matching
    assert matching["submissions"] == 0, matching
    receipt = json.loads(matching["receipt"])
    assert receipt["jobId"] == 42, receipt
    assert receipt["values"] == [
        {"key": "email", "label": "Email", "value": "avery@example.test"},
        {"key": "availability", "label": "Availability", "value": "Two weeks"},
        {"key": "portfolio", "label": "Portfolio", "value": "https://portfolio.example.test/avery"},
        {"key": "start", "label": "Start date", "value": "2026-12-01"},
    ], receipt
    changed = run_fixture(folder, True)
    assert changed["email"] == "", changed
    assert changed["availability"] == "", changed
    assert changed["portfolio"] == "", changed
    assert changed["start"] == "", changed
    assert changed["submissions"] == 0, changed
    assert changed["receipt"] is None, changed
    assert "changed" in changed["message"].lower(), changed
    invalid = run_fixture(folder, False, invalid_url=True)
    assert invalid["email"] == "", invalid
    assert invalid["portfolio"] == "", invalid
    assert invalid["receipt"] is None, invalid
    assert invalid["submissions"] == 0, invalid
    assert "https" in invalid["message"].lower(), invalid
    invalid_date = run_fixture(folder, False, invalid_date=True)
    assert invalid_date["email"] == "", invalid_date
    assert invalid_date["start"] == "", invalid_date
    assert invalid_date["receipt"] is None, invalid_date
    assert invalid_date["submissions"] == 0, invalid_date
    assert "date" in invalid_date["message"].lower(), invalid_date

print("Synthetic Chromium fill passed: exact values receipted, changed form, unsafe URL and date blocked, no submission.")
