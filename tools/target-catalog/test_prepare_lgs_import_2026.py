import unittest
from prepare_lgs_import_2026 import build_packages


class PreparedLgsImportTests(unittest.TestCase):
    def test_only_verified_results_and_reviewed_aliases_are_in_import_packages(self):
        packages = build_packages()
        self.assertEqual(8, len(packages))
        self.assertEqual(432, sum(len(p['rows']) for p in packages))
        catalogs = [p for p in packages if p['stage'] == 'Catalog']
        self.assertEqual(430, sum(len(p['rows']) for p in catalogs))
        self.assertTrue(all(r['minimumScore'] is None and r['percentile'] is None for p in catalogs for r in p['rows']))
        scored = [(p['stage'], r['minimumScore'], r['percentile']) for p in packages for r in p['rows'] if r['minimumScore'] is not None]
        self.assertEqual([('Main', 441.2318, 4.08), ('Transfer1', 442.6725, 3.88)], scored)
        ist = packages[0]
        self.assertEqual(39, len(ist['schoolAliases']))
        self.assertEqual(2, len(ist['issues']))
        self.assertTrue(all('751600' in k for k in ist['issues']))
