import unittest
from lgs_2026 import parse_istanbul


def page(quota='30', placement='LGS', year='2026'):
    return f'''<h1>{year} bölüm / program verileri</h1><table><tr>
    <th>Kurum Kodu</th><th>İlçe Adı</th><th>Okul Adı</th><th>Program Adı</th>
    <th>Alan Adı</th><th>Yabancı Dil</th><th>Yerleşme Türü</th>
    <th>{year} Kontenjan</th><th>2025 Kontenjan</th><th>2025 Taban Puan</th></tr>
    <tr><td>123456</td><td>TUZLA</td><td>ÖRNEK LİSESİ</td><td>TEKNİK</td>
    <td>BİLİŞİM</td><td>İNGİLİZCE</td><td>{placement}</td><td>{quota}</td>
    <td>99</td><td>450,12</td></tr></table>'''


class LgsMetadataTests(unittest.TestCase):
    def test_only_current_quota_is_read_never_old_scores(self):
        rows, review = parse_istanbul(page())
        self.assertEqual(30, rows[0]['quota'])
        self.assertIsNone(rows[0]['minimumScore'])
        self.assertIsNone(rows[0]['percentile'])
        self.assertEqual('BİLİŞİM', rows[0]['field'])
        self.assertEqual([], review)

    def test_school_total_and_blank_quota_remain_unknown(self):
        rows, review = parse_istanbul(page('306*'))
        self.assertIsNone(rows[0]['quota'])
        self.assertEqual('306*', review[0]['rawQuota'])
        self.assertIsNone(parse_istanbul(page(''))[0][0]['quota'])

    def test_obp_is_excluded_and_wrong_year_rejected(self):
        self.assertEqual([], parse_istanbul(page(placement='OBP'))[0])
        with self.assertRaises(ValueError):
            parse_istanbul(page(year='2025'))

    def test_offering_key_is_stable_and_language_specific(self):
        a = parse_istanbul(page())[0][0]['externalCode']
        self.assertEqual(a, parse_istanbul(page('40'))[0][0]['externalCode'])
        self.assertNotEqual(a, parse_istanbul(page().replace('İNGİLİZCE', 'ALMANCA'))[0][0]['externalCode'])
