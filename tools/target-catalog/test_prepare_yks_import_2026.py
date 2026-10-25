import unittest

from prepare_yks_import_2026 import build_package, build_ydt_package, rebuild_official_rows


class YksImportTests(unittest.TestCase):
    def test_tyt_and_ayt_scope_reproduces_official_evidence(self):
        package, audit = build_package()
        self.assertEqual((package['examCode'], package['year'], package['stage']), ('YKS', 2026, 'Main'))
        self.assertEqual(audit['scoreCounts'], {'TYT': 9254, 'SAY': 5744, 'EA': 3900, 'SOZ': 1914})
        self.assertEqual(len(package['rows']), 20812)
        self.assertEqual(len({r['externalCode'] for r in package['rows']}), 20812)
        self.assertEqual(set(audit['issues']), {'409610874'})
        self.assertEqual(audit['exactLegacyMatches'], 19229)
        self.assertEqual(audit['newPrograms'], 1582)
        self.assertEqual(audit['legacyConflicts'], ['409610874'])
        self.assertEqual(audit['legacyAmbiguous'], [])

    def test_ydt_package_contains_all_681_dil_programs_including_arabic(self):
        package, audit = build_ydt_package()
        _, _, all_rows, all_issues = rebuild_official_rows()
        source_rows = [r for r in all_rows if r['scoreType'] == 'DIL']
        expected_codes = {r['externalCode'] for r in source_rows}
        actual_codes = {r['externalCode'] for r in package['rows']}
        self.assertEqual((package['examCode'], package['year'], package['stage']), ('YKS', 2026, 'Main'))
        self.assertEqual(package['sourceUrl'], 'https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler')
        self.assertEqual(len(package['rows']), 681)
        self.assertEqual(actual_codes, expected_codes)
        self.assertTrue(all(r['scoreType'] == 'YDT' for r in package['rows']))
        self.assertEqual(package['issues'], {code: issue for code, issue in all_issues.items() if code in expected_codes})
        self.assertEqual(audit['scoreCounts'], {'YDT': 681})
        self.assertEqual(sum(audit['languageProgramCounts'].values()), 681)
        self.assertGreater(audit['languageProgramCounts'].get('Arabic', 0), 0)
        tyt_ayt, _ = build_package()
        self.assertFalse(actual_codes & {r['externalCode'] for r in tyt_ayt['rows']})


if __name__ == '__main__':
    unittest.main()
