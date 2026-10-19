"""2026 metadata only. Historical score columns are deliberately never read."""
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import urlopen
from html.parser import HTMLParser

SOURCE = 'https://istanbul.meb.gov.tr/meslek/bolum_analiz.php'
ROOT = Path('data/target-catalog/2026')


class Tables(HTMLParser):
    def __init__(self, html):
        super().__init__(convert_charrefs=True)
        self.tables, self.text = [], []
        self.table = self.row = self.cell = None
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        if tag == 'table':
            self.table = []
        elif tag == 'tr' and self.table is not None:
            self.row = []
        elif tag in ('td', 'th') and self.row is not None:
            self.cell = []

    def handle_data(self, data):
        self.text.append(data)
        if self.cell is not None:
            self.cell.append(data)

    def handle_endtag(self, tag):
        if tag in ('td', 'th') and self.cell is not None:
            self.row.append(' '.join(''.join(self.cell).split()))
            self.cell = None
        elif tag == 'tr' and self.row is not None:
            self.table.append(self.row)
            self.row = None
        elif tag == 'table' and self.table is not None:
            self.tables.append(self.table)
            self.table = None


def parse_istanbul(html):
    table = next((t for t in Tables(html).tables if t and '2026 Kontenjan' in t[0]), None)
    if table is None:
        raise ValueError('Explicit 2026 quota column required')
    headers = table[0]
    required = ['Kurum Kodu', 'İlçe Adı', 'Okul Adı', 'Program Adı', 'Alan Adı',
                'Yabancı Dil', 'Yerleşme Türü', '2026 Kontenjan']
    if any(h not in headers for h in required):
        raise ValueError('Official table schema changed')
    rows, review = [], []
    for cells in table[1:]:
        if not cells:
            continue
        d = dict(zip(headers, cells, strict=True))
        if d['Yerleşme Türü'] != 'LGS':
            continue
        identity = [d[x] for x in required[:6]]
        if any(not x for x in identity[:3]) or not re.fullmatch(r'\d+', identity[0]):
            raise ValueError('Missing official institution identity')
        # Derived offering key, not an invented official offering code. Mapping is in the manifest.
        key = 'ist:' + identity[0] + ':' + hashlib.sha256('\x1f'.join(identity).encode()).hexdigest()[:24]
        raw = d['2026 Kontenjan']
        quota = int(raw) if re.fullmatch(r'\d+', raw) else None
        if quota is None:
            review.append(dict(externalCode=key, rawQuota=raw,
                               reason='SchoolTotalNotAllocated' if '*' in raw else 'QuotaUnspecified'))
        rows.append(dict(externalCode=key, institution=d['Okul Adı'],
                         name=' / '.join(x for x in [d['Program Adı'], d['Alan Adı'], d['Yabancı Dil']] if x),
                         scoreType='LGS', city='İSTANBUL', district=d['İlçe Adı'],
                         institutionType=None, degree=None, language=d['Yabancı Dil'] or None,
                         scholarship=None, educationType=d['Program Adı'] or None, field=d['Alan Adı'] or None,
                         quota=quota, placed=None, minimumScore=None, maximumScore=None, rank=None, percentile=None))
    return rows, review


def consolidate(rows):
    groups = {}
    for row in rows:
        groups.setdefault(row['externalCode'], []).append(row)
    unique, review = [], []
    for code, group in groups.items():
        row = dict(group[0])
        if any({k:v for k,v in r.items() if k != 'quota'} != {k:v for k,v in row.items() if k != 'quota'} for r in group):
            raise ValueError('Offering identity collision')
        quotas = {r['quota'] for r in group}
        if len(quotas) > 1:
            row['quota'] = None
            review.append(dict(externalCode=code, reason='ConflictingOfficialQuota',
                               values=sorted(quotas, key=lambda v: -1 if v is None else v)))
        unique.append(row)
    return unique, review


def institution_issues(rows):
    names, codes = {}, {}
    for row in rows:
        code = row['externalCode'].split(':')[1]
        name = (row['institution'], row['district'])
        names.setdefault(code, set()).add(name)
        codes.setdefault(name, set()).add(code)
    return {r['externalCode']: 'Official institution code/name is inconsistent; manual identity review required'
            for r in rows if len(names[r['externalCode'].split(':')[1]]) > 1
            or len(codes[(r['institution'], r['district'])]) > 1}


def main():
    raw_dir = ROOT / 'raw/lgs-research'
    raw_dir.mkdir(parents=True, exist_ok=True)
    manifest, rows, review = [], [], []
    page, pages, expected = 1, 1, None
    while page <= pages:
        url = f'{SOURCE}?f_yerlesme=LGS&page={page}'
        path = raw_dir / f'istanbul-lgs-{page}.html'
        if not path.exists():
            with urlopen(url, timeout=60) as response:
                path.write_bytes(response.read())
        data = path.read_bytes()
        html = data.decode('utf-8-sig')
        text = ' '.join(' '.join(Tables(html).text).split())
        pagination = re.search(r'Toplam\s+([\d.]+)\s+kayıt\s*\(sayfa\s+(\d+)\s*/\s*(\d+)', text)
        if pagination is None or int(pagination[2]) != page:
            raise ValueError('Pagination evidence missing')
        total = int(pagination[1].replace('.', ''))
        if expected is not None and total != expected:
            raise ValueError('Source changed during retrieval')
        expected, pages = total, int(pagination[3])
        parsed, issues = parse_istanbul(html)
        rows.extend(parsed)
        review.extend(issues)
        manifest.append(dict(url=url, path=str(path), sha256=hashlib.sha256(data).hexdigest(),
                             retrievedAt=datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat(), rows=len(parsed)))
        page += 1
    if len(rows) != expected:
        raise ValueError('Missing source rows; refusing package')
    rows, duplicate_issues = consolidate(rows)
    review.extend(duplicate_issues)
    # A source code collision by school name is reviewed, never merged automatically.
    conflicts = institution_issues(rows)
    digest = hashlib.sha256(''.join(x['sha256'] for x in manifest).encode()).hexdigest()
    package = dict(examCode='LGS', year=2026, stage='Catalog', sourceUrl=SOURCE,
                   documentHash=digest, rows=rows, issues=conflicts)
    def save(name, data):
        (ROOT / name).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    save('lgs-istanbul-metadata.json', package)
    save('lgs-istanbul-review.json', review)
    summary = dict(year=2026, scope='İstanbul MEM mesleki programlar; Türkiye geneli değildir',
                   sources=manifest, offeringKey='ist:<official institution code>:<sha256 identity prefix>; derived, not official offering code',
                   rows=len(rows), schools=len({r['externalCode'].split(':')[1] for r in rows}),
                   sourceRows=expected, duplicateRows=expected-len(rows),
                   numericQuotas=sum(r['quota'] is not None for r in rows), unknownQuotas=sum(r['quota'] is None for r in rows),
                   excluded='All historical scores and quotas; all OBP rows', scoreRows=0, conflicts=len(conflicts))
    save('lgs-istanbul-manifest.json', summary)
    print(json.dumps({k:v for k,v in summary.items() if k != 'sources'}, ensure_ascii=False))


if __name__ == '__main__':
    main()
