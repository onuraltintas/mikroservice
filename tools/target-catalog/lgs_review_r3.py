"""Read-only audit of 2026 review rows against the original public catalog."""
import collections
import hashlib
import html
import json
import re
from pathlib import Path

from lgs_followup_2026 import offering

ROOT = Path(__file__).resolve().parents[2]


def normalize(value):
    return " ".join((value or "").replace("i", "İ").replace("ı", "I").upper().replace(" (MERKEZ)", "").split())


def build_packages():
    folder = ROOT / "data/target-catalog/2026/raw/lgs-review-r3-20261019"
    sources = {s["file"]: s for s in json.loads((folder / "sources.json").read_text(encoding="utf-8"))}
    facts = [
        ("eskisehir.html", "Borsa İstanbul Fen Lisesi", "ESKİŞEHİR", "TEPEBAŞI", 120),
        ("ankara.html", "Hatice-Cemil Ercan Fen Lisesi", "ANKARA", "BEYPAZARI", 90),
        ("konya.html", "Haydar-Fadim Kocaer Fen Lisesi", "KONYA", "ÇUMRA", 60),
    ]
    packages = []
    for filename, name, city, district, quota in facts:
        source = sources[filename]
        raw = (folder / filename).read_bytes()
        if hashlib.sha256(raw).hexdigest() != source["sha256"]:
            raise ValueError("Reviewed official evidence bytes changed")
        text = " ".join(html.unescape(re.sub("<[^>]+>", " ", raw.decode("utf-8"))).split())
        if not re.search(r"2026\s*-\s*2027.{0,60}?\b" + str(quota) + r"\b", text):
            raise ValueError("Explicit 2026-2027 quota required")
        row = offering(name, city, district, institutionType="Fen Lisesi", language="İngilizce", quota=quota)
        packages.append(dict(examCode="LGS", year=2026, stage="Catalog", sourceUrl=source["url"], documentHash=source["sha256"], rows=[row]))
    return packages


def audit():
    rows = json.loads((ROOT / "artifacts/lgs-review-r3-20261019/public-review.json").read_text(encoding="utf-8-sig"))
    schools = json.loads((ROOT / "artifacts/target-catalog-20261019/legacy-public-catalog.json").read_text(encoding="utf-8-sig"))["schools"]
    aliases = json.loads((ROOT / "data/target-catalog/2026/lgs-identity-resolutions-20261019.json").read_text(encoding="utf-8-sig"))["sourceAliasResolutions"]
    result = []
    for item in rows:
        r = item["record"]
        names = {normalize(r["Institution"])}
        evidence = []
        for alias in aliases:
            if r["ExternalCode"] in alias["affectedExternalCodes"]:
                names.add(normalize(alias["canonicalName"]))
                names.update(normalize(name) for name in alias["observedAliases"])
                evidence.append(alias["evidenceUrl"])
        candidates = [s["Id"] for s in schools if normalize(s["Name"]) in names and normalize(s["City"]) == normalize(r["City"]) and (not r["District"] or normalize(s["District"]) == normalize(r["District"]))]
        result.append({"externalCode": r["ExternalCode"], "status": r["MatchStatus"], "institution": r["Institution"], "candidateIds": candidates, "identityEvidence": evidence})
    output = ROOT / "data/target-catalog/2026/lgs-review-r3-audit.json"
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(collections.Counter((r["status"], len(r["candidateIds"])) for r in result))
    print(json.dumps([r for r in result if len(r["candidateIds"]) == 1], ensure_ascii=True, indent=2))


if __name__ == "__main__":
    audit()
