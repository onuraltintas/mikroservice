import unittest
from xml.etree import ElementTree

from check_coaching_coverage import evaluate


def report(rates):
    packages = "".join(
        f'<package name="{name}" line-rate="{rate}" />'
        for name, rate in rates.items()
    )
    return ElementTree.fromstring(f"<coverage><packages>{packages}</packages></coverage>")


BASELINE = {
    "Coaching.API": "0.0323",
    "Coaching.Application": "0.5185",
    "Coaching.Domain": "0.7302",
    "Coaching.Infrastructure": "0.0887",
}


class CoachingCoverageTests(unittest.TestCase):
    def test_current_baseline_passes(self):
        passed, summary = evaluate(report(BASELINE))
        self.assertTrue(passed)
        self.assertIn("Coaching.Application", summary)
        self.assertIn("51.85%", summary)

    def test_regression_fails(self):
        rates = {**BASELINE, "Coaching.Application": "0.49"}
        passed, summary = evaluate(report(rates))
        self.assertFalse(passed)
        self.assertIn("FAIL", summary)

    def test_missing_package_fails_closed(self):
        rates = {name: rate for name, rate in BASELINE.items() if name != "Coaching.API"}
        with self.assertRaisesRegex(ValueError, "Coaching.API"):
            evaluate(report(rates))

    def test_invalid_rate_fails_closed(self):
        rates = {**BASELINE, "Coaching.Domain": "n/a"}
        with self.assertRaisesRegex(ValueError, "Coaching.Domain"):
            evaluate(report(rates))

    def test_duplicate_package_fails_closed(self):
        root = report(BASELINE)
        ElementTree.SubElement(root.find("packages"), "package", name="Coaching.API", **{"line-rate": "1"})
        with self.assertRaisesRegex(ValueError, "Coaching.API"):
            evaluate(root)


if __name__ == "__main__":
    unittest.main()
