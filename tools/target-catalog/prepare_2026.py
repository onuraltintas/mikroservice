"""Read official 2026 result workbooks; never infer rank or import historical columns."""
import hashlib
import json
import re
from decimal import Decimal
from pathlib import Path
import openpyxl


def number(value):
    if value is None or str(value).strip() in ('', '--', '-', '---'):
        return None
    text = str(value).strip()
    if ',' in text:
        text = text.replace('.', '').replace(',', '.')
    return Decimal(text)


def parse_result_sheet(path, degree, expected_year=2026):
    workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
    try:
        rows = iter(workbook.active.values)
        title = str(next(rows)[0])
        if expected_year < 2026 or not re.search(rf'\b{expected_year}\b', title):
            raise ValueError('Official result year does not match requested year')
        group = next(rows)
        headers = next(rows)
        if group[6] != 'Genel Kontenjan' or headers[0] != 'Program Kodu' or headers[8] != 'En Küçük Puan':
            raise ValueError('Unexpected official columns; manual source review required')
        result = []
        for values in rows:
            code = str(values[0] or '').strip()
            if not code:
                continue
            if not re.fullmatch(r'\d{9}', code):
                raise ValueError(f'Invalid program code: {code}')
            name = str(values[4]).strip()
            institution = str(values[2]).strip()
            score = {'SÖZ': 'SOZ', 'DİL': 'DIL'}.get(values[5], values[5])
            # Only explicit source labels are extracted. Missing language/burs remains unknown.
            language = next((x for x in ['İngilizce', 'Almanca', 'Fransızca', 'Arapça', 'Rusça'] if f'({x})' in name), None)
            scholarship = next((x for x in ['Burslu', 'Ücretli', '%50 İndirimli', '%25 İndirimli'] if f'({x})' in name), None)
            education = next((x for x in ['Açıköğretim', 'Uzaktan Öğretim', 'İÖ'] if f'({x})' in name), None)
            result.append(dict(externalCode=code, institution=institution, name=name, scoreType=score,
                city=None, district=None, institutionType=values[1], degree=degree, language=language,
                scholarship=scholarship, educationType=education, field=str(values[3]).strip(),
                quota=int(number(values[6])) if number(values[6]) is not None else None,
                placed=int(number(values[7])) if number(values[7]) is not None else None,
                minimumScore=number(values[8]), maximumScore=number(values[9]), rank=None, percentile=None))
        if len({x['externalCode'] for x in result}) != len(result):
            raise ValueError('Duplicate official program codes')
        return result
    finally:
        workbook.close()


def write_json(path, data):
    # Decimal values are source precision (5 decimals), safe as JSON numbers at this magnitude.
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2, default=float)+'\n', encoding='utf-8')


def merge_source_rows(osym_rows, yok_rows):
    by_code={str(x['kilavuzKodu']):x for x in yok_rows}
    if len(by_code)!=len(yok_rows):
        raise ValueError('Duplicate YOK codes')
    issues={}
    result=[]
    for row in osym_rows:
        row=dict(row)
        yok=by_code.get(row['externalCode'])
        if yok is None or yok.get('yil')!=2026:
            issues[row['externalCode']]='Missing exact 2026 YOK program match'
        else:
            mismatches=[]
            for local,remote in [('quota','kontenjan'),('placed','gkY'),('minimumScore','minPuan')]:
                value=number(yok.get(remote))
                if local=='minimumScore' and value is not None and value<=0:value=None
                # Absence on the secondary source is not a contradiction to an official published result.
                if value is not None and number(row[local])!=value:mismatches.append(local)
            if yok.get('puanTuru') and {'SÖZ':'SOZ','DİL':'DIL'}.get(yok.get('puanTuru'),yok.get('puanTuru'))!=row['scoreType']:mismatches.append('scoreType')
            if mismatches:issues[row['externalCode']]='Official sources disagree: '+', '.join(mismatches)
            row.update(city=yok.get('ilAdi'),district=yok.get('ilceAdi'),language=yok.get('ogrenimDiliAdi'),
                scholarship=yok.get('bursOraniAdi'),educationType=yok.get('ogrenimTuruAdi'))
            rank=number(yok.get('basariSirasi'))
            row['rank']=int(rank) if rank is not None and rank>0 else None
        result.append(row)
    missing=set(by_code)-{r['externalCode'] for r in osym_rows}
    if missing:raise ValueError(f'{len(missing)} YOK programs missing from OSYM result files')
    return result,issues


def main():
    root = Path('data/target-catalog/2026')
    files = [(root/'raw/osym-results-table3.xlsx', 'Associate'), (root/'raw/osym-results-table4.xlsx', 'Bachelor')]
    rows = [r for path, degree in files for r in parse_result_sheet(path, degree)]
    hashes = {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path, _ in files}
    pages=sorted((root/'raw/yokatlas-pages').glob('*.json'))
    issues={}
    source_url='https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler'
    if pages:
        page_data=[json.loads(p.read_text('utf-8')) for p in pages]
        if len(pages)!=page_data[-1]['totalPages'] or not page_data[-1]['last']:raise ValueError('Incomplete YOK fetch')
        yok_rows=[r for p in page_data for r in p['content']]
        if len(yok_rows)!=page_data[-1]['totalElements']:raise ValueError('YOK count mismatch')
        rows,issues=merge_source_rows(rows,yok_rows)
        hashes.update({f'yokatlas/{p.name}':hashlib.sha256(p.read_bytes()).hexdigest() for p in pages})
        source_url='https://yokatlas.yok.gov.tr/api/tercih-kilavuz/search'
    combined_hash = hashlib.sha256(json.dumps(hashes, sort_keys=True).encode()).hexdigest()
    write_json(root/'yks-results.json', dict(examCode='YKS', year=2026, stage='Main',
        sourceUrl=source_url,documentHash=combined_hash, rows=rows,issues=issues))
    write_json(root/'yks-source-manifest.json', dict(year=2026, scope='GeneralQuota', files=hashes,
        bundleHash=combined_hash, rows=len(rows),rankStatus='YOK 2026 exact program code' if pages else 'Not present in official result XLSX',
        issues=issues,verifiedSourceFields='OSYM: quota,placed,min,max,code,score type; YOK: rank,city,district,language,scholarship,education',
        excludedYears='All years before 2026', legacyRecords='Preserved without assigning a guessed year'))
    print(json.dumps(dict(rows=len(rows), degreeCounts={d:sum(r['degree']==d for r in rows) for d in ['Associate','Bachelor']},
        missingScores=sum(r['minimumScore'] is None for r in rows)), ensure_ascii=True))


if __name__ == '__main__':
    main()
