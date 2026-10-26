import hashlib
import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from lgs_review_r3 import ROOT, build_packages, normalize


class ReviewR3Tests(unittest.TestCase):
    def test_turkish_case_and_central_district_normalize(self):
        self.assertEqual(normalize("  Şile  "), normalize("ŞİLE"))
        self.assertEqual(normalize("Tepebaşı (Merkez)"), normalize("TEPEBAŞI"))

    def test_quota_year_is_2026_and_old_scores_are_excluded(self):
        packages = build_packages()
        self.assertEqual([p["rows"][0]["quota"] for p in packages], [120, 90, 60])
        self.assertEqual([p["rows"][0]["city"] for p in packages], ["ESKİŞEHİR", "ANKARA", "KONYA"])
        for p in packages:
            self.assertEqual((p["year"], p["stage"]), (2026, "Catalog"))
            r = p["rows"][0]
            self.assertIsNone(r["minimumScore"])
            self.assertIsNone(r["percentile"])
            self.assertIsNone(r["placed"])

    def test_official_evidence_bytes_match_manifest(self):
        folder = ROOT / "data/target-catalog/2026/raw/lgs-review-r3-20261019"
        for s in json.loads((folder / "sources.json").read_text(encoding="utf-8")):
            self.assertEqual(hashlib.sha256((folder / s["file"]).read_bytes()).hexdigest(), s["sha256"])
            self.assertTrue(".meb.k12.tr/" in s["url"] or s["url"].startswith("https://mtegm.meb.gov.tr/"))

    def test_changed_evidence_and_wrong_year_are_rejected(self):
        relative = Path("data/target-catalog/2026/raw/lgs-review-r3-20261019")
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as temp:
            root = Path(temp)
            folder = root / relative
            shutil.copytree(ROOT / relative, folder)
            page = folder / "eskisehir.html"
            page.write_bytes(page.read_bytes().replace(b"2026", b"2025"))
            with patch("lgs_review_r3.ROOT", root):
                with self.assertRaisesRegex(ValueError, "bytes changed"):
                    build_packages()
                manifest = json.loads((folder / "sources.json").read_text(encoding="utf-8"))
                next(s for s in manifest if s["file"] == "eskisehir.html")["sha256"] = hashlib.sha256(page.read_bytes()).hexdigest()
                (folder / "sources.json").write_text(json.dumps(manifest), encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "2026-2027 quota required"):
                    build_packages()


if __name__ == "__main__":
    unittest.main()
