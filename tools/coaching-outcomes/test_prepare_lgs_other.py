import unittest
import tempfile
from pathlib import Path
from prepare_lgs_other import prepare, allocate_minutes, extract_scope

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / 'artifacts/coaching-lgs-other-20261018/sources'
CATALOG = ROOT / 'artifacts/coaching-lgs-other-20261018/catalog.json'


class LgsImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.packages, cls.additions = prepare(SOURCES, CATALOG)

    def test_lgs_scope_instead_of_whole_grade(self):
        self.assertEqual([61, 28, 39, 35, 54], [len(p['outcomes']) for p in self.packages])
        turkish = {o['code'] for o in self.packages[3]['outcomes']}
        self.assertEqual(turkish, {f'T.8.3.{n}' for n in range(5, 36)} | {f'T.8.4.{n}' for n in (16, 18, 19, 20)})
        english = {o['code'] for o in self.packages[4]['outcomes']}
        self.assertNotIn('E8.10.SP1', english)
        self.assertIn('E8.10.SI2', english)

    def test_all_selected_outcomes_and_catalog_topics_are_mapped(self):
        for p in self.packages:
            codes = {o['code'] for o in p['outcomes']}
            mapped = {c for m in p['mappings'] for c in m['outcomeCodes']}
            self.assertEqual(codes, mapped)
            self.assertEqual(len(p['mappings']), len({m['topicSourceId'] for m in p['mappings']}))
            self.assertTrue(all(m['outcomeCodes'] and m['evidence'] for m in p['mappings']))
            self.assertTrue(all(o['name'].endswith('.') and len(o['name']) < 1500 for o in p['outcomes']))
            self.assertTrue(all(o['sourcePage'] > 0 and not o['sources'] for o in p['outcomes']))
        self.assertEqual([22, 26, 39, 22, 23], [len(p['mappings']) for p in self.packages])

    def test_group_totals_and_duration_evidence(self):
        for p in self.packages:
            self.assertEqual('LGS', p['examCode'])
            self.assertEqual(2026, p['academicYearStart'])
            self.assertEqual(2026, p['academicYearEnd'])
            for g in p['officialTopics']:
                group = [o for o in p['outcomes'] if o['officialTopicCode'] == g['code']]
                self.assertEqual(g['outcomeCount'], len(group))
                self.assertTrue(all(o['code'].startswith(g['code'] + '.') for o in group))
            for o in p['outcomes']:
                self.assertTrue(all(o[k] > 0 and o[k] % 5 == 0 for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes')))
                self.assertIn('bireysel', o['durationEvidence'])
                self.assertLessEqual(sum(o[k] for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes')), 1440)

    def test_exact_budgets_and_determinism(self):
        self.assertEqual(132, sum(g['lessonPeriods'] for g in self.packages[0]['officialTopics']))
        for p, total in zip(self.packages, (5280, 2880, 2880, 3315, 4445)):
            self.assertEqual(total, sum(o['learningMinutes'] + o['practiceMinutes'] + o['checkMinutes'] for o in p['outcomes']))
        self.assertEqual((self.packages, self.additions), prepare(SOURCES, CATALOG))
        self.assertEqual([20, 20, 15], allocate_minutes(55, 3))
        with self.assertRaises(ValueError):
            allocate_minutes(5, 3)

    def test_sources_and_additions(self):
        self.assertEqual(1, len(self.additions))
        self.assertIn('Muhammed', self.additions[0]['name'])
        self.assertEqual(54, len(extract_scope(SOURCES / 'lgs-scope-2022.pdf')['english']))

    def test_modified_scope_source_is_rejected(self):
        with tempfile.TemporaryDirectory(dir=ROOT / 'artifacts/coaching-lgs-other-20261018') as temporary:
            changed = Path(temporary) / 'scope.pdf'
            changed.write_bytes(b'unreviewed source')
            with self.assertRaises(ValueError):
                extract_scope(changed)


if __name__ == '__main__':
    unittest.main()
