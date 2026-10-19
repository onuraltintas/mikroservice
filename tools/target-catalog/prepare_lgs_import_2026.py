"""Build source-separated operator packages from the reviewed 2026 evidence."""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path('data/target-catalog/2026')


def read(name):
    return json.loads((ROOT / name).read_text(encoding='utf-8'))


def row(code, institution, city, district, **values):
    result = dict(externalCode=code, institution=institution, name=institution, scoreType='LGS', city=city,
                  district=district, institutionType=None, degree=None, language=None,
                  scholarship=None, educationType=None, field=None, quota=None, placed=None,
                  minimumScore=None, maximumScore=None, rank=None, percentile=None)
    result.update(values)
    return result


def package(url, rows, evidence, stage='Catalog', **review):
    content = dict(examCode='LGS', year=2026, stage=stage, sourceUrl=url, rows=rows, **review)
    content['documentHash'] = hashlib.sha256(json.dumps([content, evidence], ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    return content


def build_packages():
    ist = copy.deepcopy(read('lgs-istanbul-metadata.json'))
    resolutions = read('lgs-identity-resolutions-20261019.json')
    aliases = {}
    for decision in resolutions['sourceAliasResolutions']:
        for code in decision['affectedExternalCodes']:
            aliases[code] = decision['observedAliases']
            next(r for r in ist['rows'] if r['externalCode'] == code)['institution'] = decision['canonicalName']
    issues = {code: 'Official source institution code 751600 belongs to another school; 768129 identity and offering require review.'
              for decision in resolutions['sourceCodeCorrections'] for code in decision['affectedExternalCodes']}
    result = [package(ist['sourceUrl'], ist['rows'], [ist['documentHash'], resolutions], issues=issues, schoolAliases=aliases)]
    for filename, raw in [('lgs-samsun-research.json', 'samsun-2026.pdf'),
                          ('lgs-kayseri-research.json', 'kayseri-al-sbl-aihl-2026.pdf'),
                          ('lgs-kayseri-mtal-research.json', 'kayseri-mtal-2026.pdf')]:
        source = read(filename)
        if hashlib.sha256((ROOT / 'raw/lgs-research' / raw).read_bytes()).hexdigest() != source['sha256']:
            raise ValueError('Reviewed source bytes changed')
        rows = []
        districts = {'Merkez Mesleki ve Teknik Anadolu Lisesi': 'MELİKGAZİ', 'Merkez MTAL': 'MELİKGAZİ',
                     'Talas Atatürk Mesleki ve Teknik Anadolu Lisesi': 'TALAS',
                     'Hürriyet Mesleki ve Teknik Anadolu Lisesi': 'MELİKGAZİ', 'Hürriyet MTAL': 'MELİKGAZİ',
                     'Muammer Kocatürk Mesleki ve Teknik Anadolu Lisesi': 'DEVELİ'}
        for index, fact in enumerate(source['rows'], 1):
            code = ('meb-offering:' + fact['officialOfferingCode']) if fact.get('officialOfferingCode') else filename.removesuffix('.json') + ':' + str(fact.get('sourceRow', index))
            name = fact['institution']
            if name == 'Merkez MTAL':
                name = 'Merkez Mesleki ve Teknik Anadolu Lisesi'
            if name == 'Hürriyet MTAL':
                name = 'Hürriyet Mesleki ve Teknik Anadolu Lisesi'
            rows.append(row(code, name, fact['city'], fact.get('district') or districts.get(fact['institution']),
                            name=' / '.join(filter(None, [fact.get('institutionType'), fact.get('field'), fact.get('duration'), fact.get('language')])) or name,
                            institutionType=fact.get('institutionType'), educationType=fact.get('duration'),
                            language=fact.get('language'), field=fact.get('field'), quota=fact['quota2026']))
        result.append(package(source['sourceUrl'], rows, source['sha256']))
    local = read('lgs-local-metadata-followup.json')['rows']
    for url in dict.fromkeys(r['sourceUrl'] for r in local):
        facts = [r for r in local if r['sourceUrl'] == url]
        rows = [row('local:' + (r['officialInstitutionCode'] or 'ozay-gonlum') + ':' + str(i), r['institution'], r['city'], r['district'],
                    name=r['institution'] + ' / ' + (r['program'] or 'Fen Lisesi'),
                    language=r['language'], educationType=r['program'], quota=r['quota2026']) for i, r in enumerate(facts, 1)]
        result.append(package(url, rows, facts))
    source = read('lgs-menemen-results-research.json')
    if source['year'] != 2026 or source['status'] != 'YearAndPlacementStageVerified_NotImported':
        raise ValueError('Reviewed result year and stage required')
    for fact in source['rows']:
        if fact['stage'] not in ['Main', 'Transfer1']:
            raise ValueError('Unreviewed placement stage')
        result.append(package(source['sourceUrl'], [row('meb-institution:762133', source['institution'], source['city'], source['district'],
                              minimumScore=float(fact['minimumScore']), percentile=float(fact['percentile']))], source['sha256'], stage=fact['stage']))
    return result


if __name__ == '__main__':
    out = ROOT / 'lgs-import-reviewed'
    out.mkdir(exist_ok=True)
    packages = build_packages()
    for index, item in enumerate(packages, 1):
        (out / f'{index:02d}-{item["stage"].lower()}.json').write_text(json.dumps(item, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(packages=len(packages), rows=sum(len(p['rows']) for p in packages))))
