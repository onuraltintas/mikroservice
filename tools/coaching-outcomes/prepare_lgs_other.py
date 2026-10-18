"""Build reproducible LGS-only import packages. This command never writes a database."""
import hashlib
import json
from pathlib import Path
import re
import sys
import unicodedata

import pymupdf

SCOPE = 'https://ttkb.meb.gov.tr/meb_iys_dosyalar/2022_03/29162211_2022_LGS.pdf'
YEAR_SCOPE = 'https://tegm.meb.gov.tr/www/2026-2027-egitim-ogretim-yili-taslak-cerceve-planlar-yayinlandi/icerik/1316'
SCHEDULE = 'https://dhgm.meb.gov.tr/meb_iys_dosyalar/2025_05/14103336_mayistebligler.pdf'
SPECS = [
    ('325', 'fen', 'Fen Bilimleri', 48, 56, r'(F\.8\.\d\.\d+\.\d+)', 61,
     '3a8aa21327083bf15c33b9c404f299ff784b11d26024522a8c24b61387c8c0c1'),
    ('318', 'din', 'Din Kültürü ve Ahlak Bilgisi', 42, 47, r'(8\.[1-5]\.\d+)', 28,
     'baace330fd3b7c5b5dde6061bbd0af3b885382d6eb5e7049095a30db8c075608'),
    ('355', 'tarih', 'T.C. İnkılap Tarihi ve Atatürkçülük', 12, 16, r'(İTA\.8\.\d\.\d+)', 39,
     '38a24b11191f7ebca2fc85831d787e7187eca70e82f0bd36e7de5c81524357d5'),
    ('663', 'turkce', 'Türkçe', 48, 52, r'(T\.8\.[1-4]\.\d+)', 76,
     '10e55d403a708d35abc192710977a47df933210a0c54a15e86ea7b9d09401633'),
    ('327', 'ingilizce', 'İngilizce', 84, 94, r'(E8\.\d+\.(?:L|SI|SP|R|W)\d+)', 70,
     'e976ae163c83bb1b3bcf79d7d77ce8cb8308dbdc97a630b941951b9b57653279'),
]


def shifted(text, amount):
    # The verified scope PDF uses two broken font encodings. Decode only for code
    # selection; readable names and explanations come from the original programs.
    return ''.join(chr(ord(c) + amount) for c in text)


def extract_scope(path):
    if hashlib.sha256(path.read_bytes()).hexdigest() != 'b97618bbc7598eb98a1e3c6f822baa92defb8e3bb01156f931b5fbc75e832774':
        raise ValueError('Unreviewed LGS scope PDF')
    with pymupdf.open(path) as document:
        if len(document) != 75:
            raise ValueError('Unexpected LGS scope document')
        english = ''.join(p.get_text() for p in document[34:44])
        codes = set(re.findall(r'E8\.\d+\.(?:L|SI|SP|R|W)\d+', english + shifted(english, 29)))
        turkish = ''.join(p.get_text() for p in document[3:6])
        turkish_codes = set(re.findall(r'T\.8\.[34]\.\d+', shifted(turkish, 29)))
        if len(codes) != 54 or turkish_codes != {f'T.8.3.{n}' for n in range(5, 36)} | {f'T.8.4.{n}' for n in (16, 18, 19, 20)}:
            raise ValueError('Scope extraction differs from reviewed LGS pages')
        return {'english': codes, 'turkish': turkish_codes}


def extract_outcomes(path, start, end, pattern, expected):
    result = {}
    with pymupdf.open(path) as document:
        for index in range(start, end):
            lines = document[index].get_text().splitlines()
            for row, line in enumerate(lines):
                match = re.match(r'^\s*' + pattern + r'\.\s*(.+)', line)
                if not match:
                    continue
                code, title = match.groups()
                if code in result or 'kazanımı ile' in title:
                    continue
                cursor = row + 1
                while not title.rstrip().endswith('.') and cursor < len(lines):
                    tail = lines[cursor].strip()
                    title = title[:-1] + tail if title.endswith('-') else title + ' ' + tail
                    cursor += 1
                title = re.sub(r'\s+', ' ', title).strip()
                result[code] = (title, index + 1)
    if len(result) != expected:
        raise ValueError(f'Expected {expected} outcomes in {path}, got {len(result)}')
    return result


def allocate_minutes(total, count):
    if count < 1 or total % 5 or total < count * 15:
        raise ValueError('Insufficient or non-five-minute budget')
    quotient, remainder = divmod(total // 5, count)
    return [(quotient + (index < remainder)) * 5 for index in range(count)]


def normal(text):
    return re.sub(r'[^a-z0-9]', '', unicodedata.normalize('NFKD', text.replace('ı', 'i').casefold()).encode('ascii', 'ignore').decode())


def prepare(sources, catalog_path):
    catalog = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
    scope = extract_scope(sources / 'lgs-scope-2022.pdf')
    packages, additions = [], []
    for file, slug, subject, start, end, regex, expected, digest in SPECS:
        pdf = sources / (file + '.pdf')
        if hashlib.sha256(pdf.read_bytes()).hexdigest() != digest:
            raise ValueError('Unreviewed source PDF: ' + file)
        found = extract_outcomes(pdf, start, end, regex, expected)
        if slug in ('turkce', 'ingilizce'):
            selected = scope['turkish' if slug == 'turkce' else 'english']
            found = {k: v for k, v in found.items() if k in selected}
        rows = [x for x in catalog if x['lesson'] == subject]
        parents = sorted([x for x in rows if not x['leaf']], key=lambda x: x['sort_order'])
        leaves = [x for x in rows if x['leaf']]
        groups, mappings, outcomes = [], {}, []

        def bind(row, codes):
            if not codes or any(c not in found for c in codes):
                raise ValueError('Unresolved reviewed mapping: ' + row['topic'])
            mappings.setdefault(row['source_id'], set()).update(codes)

        def group(code, name, periods, codes, budget=None):
            groups.append(dict(code=code, name=name, outcomeCount=len(codes), lessonPeriods=periods))
            minutes = allocate_minutes(budget if budget is not None else periods * 40, len(codes))
            for outcome_code, total in zip(codes, minutes):
                learn = max(5, round(total * .4 / 5) * 5)
                check = max(5, round(total * .1 / 5) * 5)
                title, page = found[outcome_code]
                evidence = (f'LGS kapsamı: {SCOPE}. Resmî {code} referans bütçesi {periods} ders saati. '
                    'Model: ders saati 40 dakika; seçili kazanımlara eşit dağılım, toplamı koruyan 5 dakika adımları; '
                    'yaklaşık %40 öğrenme, %50 soru/alıştırma, %10 kontrol. MEB bireysel öğrenme dakikası vermemiştir; '
                    'düşük güvenli başlangıç tahminidir, video/mola içermez.')
                if slug in ('turkce', 'ingilizce'):
                    whole = 76 if slug == 'turkce' else 70
                    evidence += (f' Konu saati değil yıllık genel ders bütçesi: {SCHEDULE} PDF 6; 36 hafta model varsayımı. '
                        f'LGS seçili {len(codes)}/{whole} oranı uygulanarak {budget} dk ayrıldı. Bu oran resmî LGS süre kararı değildir.')
                notes = (f'Yalnız LGS kapsam seçimi; {SCOPE}. Özgün kazanım metni ve kapsam açıklamaları: '
                         f'kaynak öğretim programı PDF {page}. Geçerlilik: {YEAR_SCOPE}.')
                if slug == 'ingilizce':
                    notes += (' LGS uygulaması yazılı metin/diyalog ve çoktan seçmeli soru çalışmasıdır. '
                        'Resmî beceri etiketi korunmuştur; ayrıca sözlü sınav, poster, proje veya yazı üretme ödevi oluşturulmaz.')
                outcomes.append(dict(code=outcome_code, name=title, notes=notes, officialTopicCode=code,
                    sourcePage=page, displayOrder=len(outcomes)+1, learningMinutes=learn,
                    practiceMinutes=total-learn-check, checkMinutes=check, durationEvidence=evidence, sources=[]))

        if slug == 'fen':
            text = '\n'.join(p.get_text() for p in pymupdf.open(pdf)[48:56])
            headers = re.findall(r'(?m)^(F\.8\.\d\.\d+)\.\s*(.+)\nÖnerilen Süre:\s*(\d+)', text)
            for code, name, periods in headers:
                codes = [c for c in found if c.startswith(code + '.')]
                group(code, name.strip(), int(periods), codes)
                matches = [x for x in leaves if normal(x['topic']) == normal(name)]
                if len(matches) != 1:
                    raise ValueError('Science topic name mismatch: ' + name)
                bind(matches[0], codes)
        elif slug in ('din', 'tarih'):
            periods = [14, 14, 14, 16, 14] if slug == 'din' else [8, 18, 14, 12, 6, 6, 8]
            for unit, parent in enumerate(parents, 1):
                code = f'8.{unit}' if slug == 'din' else f'İTA.8.{unit}'
                codes = sorted([c for c in found if c.startswith(code + '.')], key=lambda c: int(c.rsplit('.', 1)[1]))
                group(code, parent['topic'], periods[unit-1], codes)
                children = sorted([x for x in leaves if x['parent_id'] == parent['topic_id']], key=lambda x: x['sort_order'])
                if slug == 'din' and unit == 1:
                    for row, indices in zip(children, [(1,), (2,), (3, 4), (5,), (6,)]):
                        bind(row, [f'{code}.{n}' for n in indices])
                elif slug == 'din' and unit == 4:
                    for row, n in zip(children, [1, 2, 3, 4, 5, 6, 9]):
                        bind(row, [f'{code}.{n}'])
                    addition = dict(sourceId='lgs-2026:din:ornek-davranislar', name='Hz. Muhammed’in Örnek Davranışları ve Hikmetli Sözleri',
                        lessonId=parent['lesson_id'], unitId=parent['unit_id'], parentId=parent['topic_id'], displayOrder=8)
                    additions.append(addition)
                    bind({'source_id': addition['sourceId'], 'topic': addition['name']}, [f'{code}.7', f'{code}.8'])
                else:
                    if len(children) != len(codes):
                        raise ValueError('Unit mapping count mismatch')
                    for row, outcome in zip(children, codes):
                        bind(row, [outcome])
        elif slug == 'turkce':
            group('T.8', 'LGS Türkçe seçili kazanımlar; yıllık genel ders bütçesi referansı', 180, list(found), 3315)
            for row in leaves:
                name = row['topic']
                if name == 'Sözcükte Anlam': codes = ['T.8.3.5', 'T.8.3.6']
                elif name == 'Söz Sanatları': codes = ['T.8.3.7']
                elif name == 'Anlatım Bozuklukları': codes = ['T.8.3.8', 'T.8.4.16']
                elif name == 'Fiilimsiler': codes = ['T.8.3.9']
                elif name.startswith('Cümlede Anlam'): codes = ['T.8.3.10', 'T.8.3.25']
                elif name.startswith('Paragrafta Anlam'): codes = [f'T.8.3.{n}' for n in [11,12,13,14,15,16,17,18,19,20,21,22,23,24,28,29,30,31,33,34,35]]
                elif name == 'Metin Türleri': codes = ['T.8.3.26']
                elif name.startswith(('Yazım Kuralları', 'Noktalama İşaretleri')): codes = ['T.8.4.16']
                elif name == 'Cümlenin Ögeleri': codes = ['T.8.4.18']
                elif name == 'Cümle Türleri': codes = ['T.8.4.19']
                elif name == 'Fiilde Çatı': codes = ['T.8.4.20']
                elif name == 'Sözel Mantık': codes = ['T.8.3.14', 'T.8.3.25', 'T.8.3.35']
                elif name == 'Görsel, Grafik ve Tablo Yorumlama': codes = ['T.8.3.27', 'T.8.3.32', 'T.8.3.33']
                else: raise ValueError('Unknown Turkish topic ' + name)
                bind(row, codes)
        else:
            group('E8', 'LGS İngilizce seçili kazanımlar; yıllık genel ders bütçesi referansı', 144, list(found), 4445)
            for unit, parent in enumerate(parents, 1):
                codes = [c for c in found if c.startswith(f'E8.{unit}.')]
                children = sorted([x for x in leaves if x['parent_id'] == parent['topic_id']], key=lambda x: x['sort_order'])
                # Each unit's reading outcomes cover its written vocabulary/dialogue
                # contexts. Specific functional outcomes are narrowed where explicit.
                for row in children:
                    selected = list(codes)
                    if unit == 1:
                        selected = [c for c in codes if c.rsplit('.',1)[1] not in (('R1',) if row['sort_order'] == 1 else ('R2','W1'))]
                    if unit == 3:
                        selected = [c for c in codes if c.endswith('R2') or (row['sort_order'] == 1 and not c.endswith('SI1')) or (row['sort_order'] == 3 and c.endswith('SI1'))]
                    if unit == 4 and row['sort_order'] == 1: selected = [c for c in codes if not c.endswith('SP1')]
                    if unit == 6 and row['sort_order'] == 1: selected = [c for c in codes if not c.endswith(('SP1','W1'))]
                    if unit == 7:
                        selected = [c for c in codes if not c.endswith('SP3') or row['sort_order'] == 4]
                    bind(row, selected)
        mapped = {c for codes in mappings.values() for c in codes}
        if mapped != set(found) or len(mappings) != len(leaves) + (slug == 'din'):
            raise ValueError('Incomplete mapping coverage for ' + slug)
        packages.append(dict(code=f'meb-lgs-{slug}-2026-v1', examCode='LGS', gradeNumber=8, subjectName=subject,
            academicYearStart=2026, academicYearEnd=2026, sourceUrl=(sources/(file+'.url')).read_text(encoding='utf-8-sig').strip(),
            documentSha256=digest, scopeEvidenceUrl=SCOPE, officialTopics=groups, outcomes=outcomes,
            mappings=[dict(topicSourceId=key, outcomeCodes=sorted(value), evidence=(
                'Resmî LGS kapsam seçimi ve konu/ünite işlevi karşılaştırıldı. ' + SCOPE +
                ' Numaralı konu bölümleri ortak resmî kapsamı paylaşır; video bölüm ayrımı iddia edilmez. '
                'Aynı kazanım yıllık planda kimliği üzerinden yalnız bir kez sayılır.')) for key,value in mappings.items()]))
    return packages, additions


if __name__ == '__main__':
    packages, additions = prepare(Path(sys.argv[1]), Path(sys.argv[2]))
    output = Path(sys.argv[3]); output.mkdir(parents=True, exist_ok=True)
    for package in packages:
        (output/(package['code']+'.json')).write_text(json.dumps(package, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (output/'topic-additions.json').write_text(json.dumps(additions, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({p['subjectName']:len(p['outcomes']) for p in packages}, ensure_ascii=False))
