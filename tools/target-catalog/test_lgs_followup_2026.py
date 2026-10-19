import unittest
from pathlib import Path
from lgs_followup_2026 import parse_konak_catalog, parse_izmir_results

RAW = Path('data/target-catalog/2026/raw/lgs-followup-20261019')


class FollowupEvidenceTests(unittest.TestCase):
    def test_catalog_uses_only_2026_total_quota_and_excludes_local_obp(self):
        rows = parse_konak_catalog(RAW / 'konak-quota.pdf')
        self.assertEqual(len(rows), 22)
        english = next(r for r in rows if r['institution'] == 'Atatürk Lisesi' and r['language'] == 'İngilizce')
        self.assertEqual(english['quota'], 150)
        self.assertTrue(all(r['minimumScore'] is None and r['percentile'] is None for r in rows))

    def test_first_placement_is_not_a_vacancy_quota(self):
        rows = parse_izmir_results(RAW / 'izmir-main.pdf', 'Main')
        self.assertEqual(len(rows), 135)
        izmir_fen = next(r for r in rows if r['institution'] == 'İzmir Fen Lisesi')
        self.assertEqual(izmir_fen['minimumScore'], 492.1823)
        self.assertIsNone(izmir_fen['quota'])
        self.assertTrue(all(r['percentile'] is None for r in rows))

    def test_second_transfer_never_falls_back_to_main_score(self):
        rows = parse_izmir_results(RAW / 'izmir-transfer2.pdf', 'Transfer2')
        self.assertEqual(len(rows), 108)
        self.assertFalse(any(r['institution'] == 'Aliağa Heydar Aliyev Mesleki ve Teknik Anadolu Lisesi' for r in rows))
        self.assertEqual(next(r for r in rows if r['institution'] == 'İzmir Fen Lisesi')['minimumScore'], 491.8566)
        self.assertTrue(all(r['quota'] is None and r['percentile'] is None for r in rows))

    def test_wrong_document_stage_is_rejected(self):
        with self.assertRaises(ValueError):
            parse_izmir_results(RAW / 'izmir-main.pdf', 'Transfer2')


if __name__ == '__main__':
    unittest.main()
