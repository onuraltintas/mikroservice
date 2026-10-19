import unittest
from pathlib import Path
from prepare_2026 import parse_result_sheet, number, merge_source_rows


class OfficialResultTests(unittest.TestCase):
    def test_blank_scores_are_unknown_and_turkish_decimal_is_exact(self):
        self.assertIsNone(number('--'))
        self.assertIsNone(number(None))
        self.assertEqual('443.22731', str(number('443,22731')))

    def test_actual_official_workbook_only_uses_2026_general_quota(self):
        rows = parse_result_sheet(Path('data/target-catalog/2026/raw/osym-results-table4.xlsx'), 'Bachelor')
        self.assertGreater(len(rows), 10000)
        row = next(x for x in rows if x['externalCode'] == '106510077')
        self.assertEqual(80, row['quota'])
        self.assertEqual('443.22731', str(row['minimumScore']))
        self.assertIsNone(row['rank'])

    def test_wrong_year_is_rejected(self):
        with self.assertRaises(ValueError):
            parse_result_sheet(Path('data/target-catalog/2026/raw/osym-results-table4.xlsx'), 'Bachelor', expected_year=2027)

    def test_source_disagreement_is_separate_and_rank_is_never_inferred(self):
        row=parse_result_sheet(Path('data/target-catalog/2026/raw/osym-results-table4.xlsx'),'Bachelor')[0]
        yok=dict(kilavuzKodu=row['externalCode'],yil=2026,puanTuru=row['scoreType'],kontenjan=row['quota'],gkY=row['placed'],minPuan=999,basariSirasi=None)
        merged,issues=merge_source_rows([row],[yok])
        self.assertIn(row['externalCode'],issues)
        self.assertIsNone(merged[0]['rank'])


if __name__ == '__main__':
    unittest.main()
