"""Reproduce the official 2026 results before preparing the TYT/AYT import."""
import collections
import hashlib
import json
from pathlib import Path

from prepare_2026 import merge_source_rows, parse_result_sheet

ROOT = Path(__file__).resolve().parents[2]


def rebuild_official_rows():
    folder = ROOT / 'data/target-catalog/2026'
    manifest = json.loads((folder / 'yks-source-manifest.json').read_text(encoding='utf-8'))
    original = json.loads((folder / 'yks-results.json').read_text(encoding='utf-8'))
    for name, expected in manifest['files'].items():
        path = folder / 'raw' / name.replace('yokatlas/', 'yokatlas-pages/')
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError('Official evidence hash mismatch: ' + name)
    combined = hashlib.sha256(json.dumps(manifest['files'], sort_keys=True).encode()).hexdigest()
    if combined != manifest['bundleHash'] or combined != original['documentHash']:
        raise ValueError('Official bundle hash mismatch')
    osym = [r for file, degree in [('osym-results-table3.xlsx', 'Associate'), ('osym-results-table4.xlsx', 'Bachelor')]
            for r in parse_result_sheet(folder / 'raw' / file, degree)]
    pages = [json.loads(p.read_text(encoding='utf-8')) for p in sorted((folder / 'raw/yokatlas-pages').glob('*.json'))]
    yok = [r for page in pages for r in page['content']]
    if len(pages) != pages[-1]['totalPages'] or not pages[-1]['last'] or len(yok) != pages[-1]['totalElements']:
        raise ValueError('Incomplete official YOK pages')
    rows, issues = merge_source_rows(osym, yok)
    rows = json.loads(json.dumps(rows, default=float))
    if rows != original['rows'] or issues != original.get('issues', {}):
        raise ValueError('Prepared rows differ from reproduced official evidence')
    return combined, original, rows, issues


def build_package():
    combined, original, all_rows, all_issues = rebuild_official_rows()
    rows = [r for r in all_rows if r['scoreType'] in ('TYT', 'SAY', 'EA', 'SOZ')]
    issues = {k: v for k, v in all_issues.items() if k in {r['externalCode'] for r in rows}}
    package = dict(examCode='YKS', year=2026, stage='Main', sourceUrl=original['sourceUrl'],
                   documentHash=hashlib.sha256((combined + ':TYT,SAY,EA,SOZ').encode()).hexdigest(), rows=rows, issues=issues)
    old = json.loads((ROOT / 'artifacts/target-catalog-20261019/legacy-public-catalog.json').read_text(encoding='utf-8-sig'))['programs']
    lookup = collections.defaultdict(list)
    for p in old:
        lookup[p['ProgramCode']].append(p)
    exact, new, conflicts, ambiguous = 0, 0, [], []
    for r in rows:
        candidates = lookup[r['externalCode']]
        if len(candidates) > 1:
            ambiguous.append(r['externalCode'])
        elif not candidates:
            new += 1
        elif r['externalCode'] == '409610874':
            # Reviewed against both official sources: a shared code alone cannot
            # link Economics in the legacy catalog to Gastronomy in 2026.
            if candidates[0]['Name'] != 'Ekonomi (%25 İndirimli)' or ' '.join(r['name'].split()) != 'Gastronomi (Ücretli)':
                raise ValueError('Reviewed program identity changed; inspect source evidence again')
            conflicts.append(r['externalCode'])
            issues[r['externalCode']] = 'Program identity requires review: legacy Economics (25% discount) versus official 2026 Gastronomy (paid). Same code and score type are insufficient; existing legacy goals preserved.'
        elif {'SÖZ': 'SOZ', 'DİL': 'DIL'}.get(candidates[0]['ScoreType'], candidates[0]['ScoreType']) != r['scoreType']:
            conflicts.append(r['externalCode'])
        else:
            exact += 1
    audit = dict(scoreCounts=dict(collections.Counter(r['scoreType'] for r in rows)), issues=issues,
                 exactLegacyMatches=exact, newPrograms=new, legacyConflicts=conflicts, legacyAmbiguous=ambiguous,
                 officialEvidenceHash=combined, packageHash=package['documentHash'], year=2026, stage='Main')
    return package, audit


def build_ydt_package():
    combined, original, all_rows, all_issues = rebuild_official_rows()
    rows = [dict(r, scoreType='YDT') for r in all_rows if r['scoreType'] == 'DIL']
    codes = {r['externalCode'] for r in rows}
    if len(rows) != 681 or len(codes) != 681:
        raise ValueError('The 2026 YDT package must contain all 681 unique official DİL program codes')
    issues = {code: message for code, message in all_issues.items() if code in codes}
    package = dict(examCode='YKS', year=2026, stage='Main',
                   sourceUrl='https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler',
                   documentHash=hashlib.sha256((combined + ':YDT').encode()).hexdigest(), rows=rows, issues=issues)
    language_counts = collections.Counter(
        'Arabic' if 'arap' in (row.get('name', '') + ' ' + str(row.get('language', ''))).casefold() else 'Other'
        for row in rows
    )
    audit = dict(scoreCounts={'YDT': len(rows)}, issues=issues, languageProgramCounts=dict(language_counts),
                 officialEvidenceHash=combined, packageHash=package['documentHash'], year=2026, stage='Main',
                 sourceFields='OSYM placement quota/scores plus YOK Atlas program code and identity enrichment')
    return package, audit


if __name__ == '__main__':
    package, audit = build_package()
    ydt_package, ydt_audit = build_ydt_package()
    folder = ROOT / 'data/target-catalog/2026/yks-reviewed-20261019'
    folder.mkdir(exist_ok=True)
    for filename, data in [('tyt-ayt.json', package), ('audit.json', audit),
                           ('ydt-all-languages.json', ydt_package), ('ydt-audit.json', ydt_audit)]:
        (folder / filename).write_bytes((json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())
    print(json.dumps({'tytAyt': audit, 'ydt': ydt_audit}, ensure_ascii=True))
