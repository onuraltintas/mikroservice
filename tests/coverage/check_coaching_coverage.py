"""Report Coaching coverage from a Cobertura file and reject baseline regressions."""

import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path
from xml.etree import ElementTree


# Conservative integration-test floors, not the product's 80% coverage target.
FLOORS = {
    "Coaching.API": Decimal("0.03"),
    "Coaching.Application": Decimal("0.50"),
    "Coaching.Domain": Decimal("0.70"),
    "Coaching.Infrastructure": Decimal("0.08"),
}


def evaluate(root):
    """Return (passed, Markdown summary); reject incomplete or invalid reports."""
    if root.tag != "coverage":
        raise ValueError("Expected a Cobertura coverage root")

    lines = ["| Coaching assembly | Line coverage | Floor | Result |",
             "| --- | ---: | ---: | --- |"]
    passed = True
    for name, floor in FLOORS.items():
        packages = root.findall(f"./packages/package[@name='{name}']")
        if len(packages) != 1:
            raise ValueError(f"Expected exactly one {name} package, found {len(packages)}")
        try:
            rate = Decimal(packages[0].attrib["line-rate"])
        except (KeyError, InvalidOperation) as exc:
            raise ValueError(f"Invalid line-rate for {name}") from exc
        if not rate.is_finite() or not 0 <= rate <= 1:
            raise ValueError(f"Invalid line-rate for {name}: {rate}")
        result = "PASS" if rate >= floor else "FAIL"
        passed &= rate >= floor
        lines.append(f"| {name} | {rate:.2%} | {floor:.0%} | {result} |")

    lines.append("\nIntegration collector only; Docker E2E is not included. "
                 "Floors prevent regression and do not prove the 80% product target.")
    return passed, "\n".join(lines)


def main():
    if len(sys.argv) != 2:
        print("Usage: check_coaching_coverage.py coverage.cobertura.xml", file=sys.stderr)
        return 2
    try:
        root = ElementTree.parse(Path(sys.argv[1])).getroot()
        passed, summary = evaluate(root)
    except (OSError, ElementTree.ParseError, ValueError) as exc:
        print(f"Coaching coverage check failed: {exc}", file=sys.stderr)
        return 1
    print(summary)
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
