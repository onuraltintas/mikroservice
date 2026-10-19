"""Read reviewed official LGS tables without mixing quota years or result stages."""
import hashlib
import json
from pathlib import Path
import pdfplumber

ROOT = Path('data/target-catalog/2026')
RAW = ROOT / 'raw/lgs-followup-20261019'


def clean(value):
    return ' '.join((value or '').split())


def offering(institution, city, district, **values):
    data = dict(institution=institution, name=institution, scoreType='LGS', city=city, district=district,
                institutionType=None, degree=None, language=None, scholarship=None, educationType=None,
                field=None, quota=None, placed=None, minimumScore=None, maximumScore=None, rank=None, percentile=None)
    data.update(values)
    data['name'] = ' / '.join(filter(None, [institution, data['field'], data['language'], data['educationType']]))
    data['externalCode'] = 'reviewed-lgs2026:' + hashlib.sha256(json.dumps(data, sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:32]
    return data


def parse_konak_catalog(path):
    with pdfplumber.open(path) as pdf:
        table = pdf.pages[0].extract_tables()[0]
    if '2026' not in table[0][11] or '2025' not in table[0][8]:
        raise ValueError('Reviewed quota and old-score headers required')
    return [offering(clean(r[3]), clean(r[1]), clean(r[2]), institutionType=clean(r[4]),
                     field=clean(r[5]), language=clean(r[6]), educationType=clean(r[19]), quota=int(r[11]))
            for r in table[1:] if clean(r[7]) == 'SINAVLI']


def parse_izmir_results(path, stage):
    if stage not in ('Main', 'Transfer2'):
        raise ValueError('Unreviewed placement stage')
    rows = []
    with pdfplumber.open(path) as pdf:
        header = ' '.join(clean(c) for c in pdf.pages[0].extract_tables()[0][0])
        required = '05 Ağustos 2026' if stage == 'Main' else '14 AĞUSTOS 2026'
        if required not in header or (stage == 'Transfer2' and '2. Nakil' not in header):
            raise ValueError('Explicit source year and stage required')
        for page in pdf.pages:
            for table in page.extract_tables():
                for cells in table:
                    label = clean(cells[0])
                    if ' / ' not in label:
                        continue
                    score = clean(cells[8])
                    # A blank transfer score must remain absent even when the main score is present.
                    if not score:
                        continue
                    amount = float(score.replace(',', '.'))
                    if not 0 <= amount <= 500:
                        raise ValueError('Unexpected LGS score')
                    district, institution = label.split(' / ', 1)
                    rows.append(offering(institution, 'İZMİR', district, institutionType=clean(cells[1]),
                                         field=clean(cells[2]) or None, educationType=clean(cells[3]),
                                         language=clean(cells[6]), minimumScore=amount))
    return rows


def build_packages():
    sources = {r['file']: r for r in json.loads((RAW / 'sources.json').read_text(encoding='utf-8'))}
    output = []
    for filename, stage, parser in [('konak-quota.pdf', 'Catalog', parse_konak_catalog),
                                    ('izmir-main.pdf', 'Main', lambda p: parse_izmir_results(p, 'Main')),
                                    ('izmir-transfer2.pdf', 'Transfer2', lambda p: parse_izmir_results(p, 'Transfer2'))]:
        source = sources[filename]
        if hashlib.sha256((RAW / filename).read_bytes()).hexdigest() != source['sha256']:
            raise ValueError('Reviewed evidence changed')
        rows = parser(RAW / filename)
        if stage == 'Main':
            # Already imported from the school's direct official announcement; this document corroborates it.
            rows = [r for r in rows if r['institution'] != 'Şehit Ahmet Özsoy Fen Lisesi']
        output.append(dict(examCode='LGS', year=2026, stage=stage, sourceUrl=source['url'], documentHash=source['sha256'], rows=rows))
    local = [('mardin.html', 'Mardin Fen Lisesi', 'MARDİN', 'ARTUKLU', [('Fen Lisesi', None, 150)]),
             ('zonguldak.html', 'Nuran ve Celal Gülşen Mesleki ve Teknik Anadolu Lisesi', 'ZONGULDAK', 'ÇAYCUMA',
              [('Anadolu Teknik Programı', 'ELEKTRİK-ELEKTRONİK TEKNOLOJİSİ', 30), ('Anadolu Teknik Programı', 'ENDÜSTRİYEL OTOMASYON TEKNOLOJİLERİ', 30)]),
             ('nevsehir.html', 'Ahmet Yiğitoğlu Turizm Mesleki ve Teknik Anadolu Lisesi', 'NEVŞEHİR', 'ÜRGÜP',
              [('Mesleki ve Teknik Anadolu Lisesi', 'YİYECEK İÇECEK HİZMETLERİ', 30), ('Mesleki ve Teknik Anadolu Lisesi', 'KONAKLAMA VE SEYAHAT HİZMETLERİ', 30)])]
    for filename, name, city, district, facts in local:
        s = sources[filename]
        if hashlib.sha256((RAW / filename).read_bytes()).hexdigest() != s['sha256']:
            raise ValueError('Reviewed local source changed')
        rows = [offering(name, city, district, institutionType=t, field=f, quota=q,
                         language='İngilizce' if filename == 'mardin.html' else None) for t, f, q in facts]
        output.append(dict(examCode='LGS', year=2026, stage='Catalog', sourceUrl=s['url'], documentHash=s['sha256'], rows=rows))
    return output


if __name__ == '__main__':
    folder = ROOT / 'lgs-followup-reviewed-20261019'
    folder.mkdir(exist_ok=True)
    packages = build_packages()
    for i, p in enumerate(packages, 1):
        (folder / f'{i:02d}-{p["stage"].lower()}.json').write_text(json.dumps(p, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(packages=len(packages), rows=[len(p['rows']) for p in packages])))
