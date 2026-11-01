"""Build the KM2 catalog pack from the read-only production catalog snapshot (never committed).

    python3 -I content-packs/coaching-catalog-km2/v1/build.py --snapshot <coaching-catalog-20261101 dir> [--out <dir>]

Writes data/*.csv (the reviewed proposals) and renders templates/*.sql.in into self-contained SQL files with the data
embedded, so the operator can pipe each file into psql inside the database container. Same snapshot -> same bytes.
"""
import argparse
import csv
import re
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = 'approved-catalog'
LESSONS = {  # study_catalog_lessons (Source, SourceId) -> role
    'tyt_tarih': '1db94d28-83dd-4de8-b590-08dc055fab0e',
    'ayt_tarih': 'c4ee03b3-8384-4963-b599-08dc055fab0e',
    'tyt_matematik': '9ca488e4-6265-4885-b58f-08dc055fab0e',
    'tdp_matematik': '6283030c-281d-4ff3-1536-08dce12e5803',
    'lgs_matematik': '00c641eb-c82d-4712-3b41-08dd3944e858',
}
TAR_EDITION = 'yks-preparation-2026-tar'
MEBI_URL = 'https://ogmmateryal.eba.gov.tr/mebi'
OSYM_AYT_2026 = 'https://dokuman.osym.gov.tr/pdfdokuman/2026/YKS/TSK/yks_ayt_2026_kitapcik_kt12.pdf'
LINK_EVIDENCE = ('KM2 önerisi (ekip onayı bekliyor, 01.11.2026): bu AYT Tarih konusu, aynı MEBİ başlıklı Tarih (TYT) konusu '
                 '"{tyt}" ile aynı {edition} sınav hazırlık kazanımını paylaşır. ÖSYM 2026 AYT soru kitapçığında Tarih-1 '
                 '(TDE-SB1, 10 soru) ve Tarih-2 (SB2, 11 soru) tüm dönemlere dağılır; konu ayrımı yapılmaz. Kaynaklar: '
                 + MEBI_URL + ' ; ' + OSYM_AYT_2026)

# TDP "Matematik (Destek)" leaf -> comparable TYT Matematik leaves (topic SourceIds). Our own analysis (KM2, 01.11.2026):
# the same mathematical content at TYT level. An empty list falls back to the median of all TYT Matematik leaves.
TDP_COMPARABLES = {
    'topic:a2bdbbd0-25ca-46ae-0546-08dce38e4979': ['topic:ed2aa349-dcd9-4a56-c844-08dc224a8549', 'topic:8f8127ee-0908-4079-5faf-08dc0b6b6139'],
    'topic:8ac577e3-be34-40be-274a-08dce39258cc': ['topic:21657ed2-c3f6-4315-b6bf-08dc0b6b444a', 'topic:45cebedf-d996-4238-8b89-08dc0b6b07fd'],
    'topic:17c42310-940a-4f7c-274c-08dce39258cc': ['topic:bfdbf326-d5c3-4560-b6c2-08dc0b6b444a', 'topic:5dd5568e-8073-4014-31d9-08dc0b6b4f46',
                                                   'topic:f64b9d70-06c6-41b8-af8c-08dc0b6afc03', 'topic:4f6ec219-204d-40e4-8b8a-08dc0b6b07fd',
                                                   'topic:500c543c-c211-40ef-5694-08dc0b6b5532'],
    'topic:b4029917-0e6a-4ec8-274b-08dce39258cc': ['topic:4c1b662b-5fbb-405b-568f-08dc0b6b5532', 'topic:f461e084-d8cf-4c35-2282-08dc0b6af052',
                                                   'topic:264b62f2-e64c-4e8e-e9dd-08dc0b6ad494', 'topic:c80a52cd-4665-45e3-af7d-08dc0b6afc03',
                                                   'topic:03c6794c-67d6-453c-568e-08dc0b6b5532'],
    'topic:6cc652c9-9aed-4115-274d-08dce39258cc': ['topic:a7bace37-2e4d-4831-e9d6-08dc0b6ad494', 'topic:42a96ee6-de6e-49bc-e9d7-08dc0b6ad494',
                                                   'topic:cc6d4eaa-fb41-430a-e9d9-08dc0b6ad494'],
    'topic:ffe5ee44-87e2-4aca-274e-08dce39258cc': ['topic:51969f2b-06b4-40cb-568b-08dc0b6b5532', 'topic:ed2aa349-dcd9-4a56-c844-08dc224a8549'],
    'topic:082d442c-39f3-4ac7-274f-08dce39258cc': ['topic:562f645f-7262-4de5-5692-08dc0b6b5532', 'topic:48d41471-990b-476e-af82-08dc0b6afc03'],
    'topic:303289bf-7827-4138-2750-08dce39258cc': ['topic:15d9e824-647a-4b03-5691-08dc0b6b5532'],
    'topic:139f6398-d06a-4a28-2751-08dce39258cc': ['topic:4a9b0c1b-d4ed-4799-af81-08dc0b6afc03'],
    'topic:7ba52936-6505-43e9-2752-08dce39258cc': ['topic:8cfafab3-b44b-44de-b6bc-08dc0b6b444a'],
    'topic:6a5ee913-82ed-47fc-2753-08dce39258cc': ['topic:8cfafab3-b44b-44de-b6bc-08dc0b6b444a', 'topic:3777fc8e-bec0-49bb-e9df-08dc0b6ad494',
                                                   'topic:c7ead496-24b3-45db-5fb3-08dc0b6b6139'],
    'topic:bd0a9d11-93ef-40fc-2754-08dce39258cc': ['topic:4c93cb58-36f9-458c-af93-08dc0b6afc03', 'topic:d4daa420-c8e9-43d1-2289-08dc0b6af052',
                                                   'topic:9d366c1b-60f7-430a-8b8f-08dc0b6b07fd'],
    'topic:8e62586f-0f5e-41bf-2755-08dce39258cc': ['topic:0658e545-4322-40e8-b6bd-08dc0b6b444a', 'topic:3fc5e036-f251-4420-e9de-08dc0b6ad494'],
    'topic:523f6f42-5d12-4316-2756-08dce39258cc': ['topic:bf118af2-a14d-4462-b6c5-08dc0b6b444a', 'topic:e0f295ba-69b8-477d-b6c6-08dc0b6b444a'],
    'topic:416b7fd3-6525-4a24-2757-08dce39258cc': ['topic:c21a1316-4b7c-4737-8b91-08dc0b6b07fd', 'topic:b3fde499-107e-414c-af9f-08dc0b6afc03',
                                                   'topic:0681068f-b6c4-48fb-5fb7-08dc0b6b6139', 'topic:bc154e6a-18f4-4ac1-31f3-08dc0b6b4f46'],
    'topic:9c2c87e9-ef7b-4939-2758-08dce39258cc': [],
    'topic:0613181d-8523-4ea6-2759-08dce39258cc': ['topic:3df3692c-ef06-4e4d-56a0-08dc0b6b5532', 'topic:7618f3a2-3d6b-4a35-b6c8-08dc0b6b444a',
                                                   'topic:6c6e88ef-2bac-440c-afa0-08dc0b6afc03', 'topic:89b099e0-b6bf-4409-99ed-08dc48bc468e'],
    'topic:e69c15a2-9c20-438c-275a-08dce39258cc': ['topic:8722458a-4b63-4b70-1594-08dc0c34b5fa', 'topic:5c631438-b2ce-4d01-1595-08dc0c34b5fa'],
    'topic:04c8bbeb-b1de-44d2-275b-08dce39258cc': [],
    'topic:942328dc-2e5a-4f03-275c-08dce39258cc': [],
    'topic:4e4a871e-58f5-40ec-275d-08dce39258cc': [],
    'topic:22c139e4-0a05-41d4-275e-08dce39258cc': [],
    'topic:fdc4948b-9262-47dd-275f-08dce39258cc': [],
    'topic:802cb490-0b67-4e65-2760-08dce39258cc': ['topic:8d38aba1-1884-42fb-b6c7-08dc0b6b444a'],
    'topic:86678d89-c314-42ec-2761-08dce39258cc': ['topic:da357cd8-c265-4516-8b93-08dc0b6b07fd', 'topic:62ab8080-0654-4c68-afa1-08dc0b6afc03',
                                                   'topic:0e2b0ba9-1e56-41e1-228c-08dc0b6af052', 'topic:47aec3c4-1e9a-42d0-228d-08dc0b6af052',
                                                   'topic:800cbf06-32a0-4836-e9ea-08dc0b6ad494', 'topic:ba55ec77-d08b-46f1-31f8-08dc0b6b4f46'],
    'topic:a5657513-6b65-434f-2762-08dce39258cc': ['topic:1e8d6134-2e07-45bc-228b-08dc0b6af052', 'topic:99b09934-a44e-4e57-569d-08dc0b6b5532'],
    'topic:e24af3c3-891b-4df1-2764-08dce39258cc': ['topic:882d753d-9925-4933-5695-08dc0b6b5532', 'topic:82820b04-9d79-437b-2287-08dc0b6af052'],
    'topic:02f16954-3c61-42de-2763-08dce39258cc': ['topic:37f2cb9e-1454-4941-228a-08dc0b6af052', 'topic:0fcde9db-2137-44cc-569c-08dc0b6b5532'],
}
# Reference only (not used for the proposal): the LGS Matematik leaves for the same 8th-grade MEB unit.
TDP_LGS_REFERENCE = {
    'topic:b4029917-0e6a-4ec8-274b-08dce39258cc': ['topic:ad650afa-ca7c-4716-91ce-08dd3ad55a15'],
    'topic:303289bf-7827-4138-2750-08dce39258cc': ['topic:78f3c2ce-8ffb-4328-fc43-08dd54a00709'],
    'topic:139f6398-d06a-4a28-2751-08dce39258cc': ['topic:6eb3d293-90df-48cc-26b4-08dd7688daf4'],
    'topic:bd0a9d11-93ef-40fc-2754-08dce39258cc': ['topic:25c4abd5-c087-4826-26d9-08dd7688daf4'],
    'topic:6a5ee913-82ed-47fc-2753-08dce39258cc': ['topic:226d9b02-ede1-401f-7499-08de07173e11', 'topic:498052bd-05cf-4efe-bbbc-08de1c4bdb1f'],
    'topic:8e62586f-0f5e-41bf-2755-08dce39258cc': ['topic:153a76d9-652b-48d2-749a-08de07173e11'],
    'topic:a5657513-6b65-434f-2762-08dce39258cc': ['topic:0698637b-2694-4c91-749c-08de07173e11'],
    'topic:fdc4948b-9262-47dd-275f-08dce39258cc': ['topic:760ce8d8-db61-4953-749d-08de07173e11'],
    'topic:e24af3c3-891b-4df1-2764-08dce39258cc': ['topic:3f134b87-660b-493c-26d7-08dd7688daf4'],
    'topic:02f16954-3c61-42de-2763-08dce39258cc': ['topic:2db1a5c6-ecf8-4c70-26b3-08dd7688daf4'],
}


def read(snapshot, table):
    with open(snapshot / f'{table}.csv', newline='', encoding='utf-8') as handle:
        return list(csv.DictReader(handle))


def norm(name):
    # Same rule as pg_temp.km2_norm in the SQL guards: punctuation-insensitive, case-insensitive name match.
    return re.sub(r'\s+', ' ', re.sub(r"[’'`\-,. ]", ' ', name.lower())).strip()


def lower_median(values):
    return sorted(values)[(len(values) - 1) // 2]


def sql(value):
    return 'NULL' if value is None else ("'" + str(value).replace("'", "''") + "'")


def build(snapshot, out):
    lessons = {r['SourceId']: r for r in read(snapshot, 'study_catalog_lessons') if r['Source'] == SOURCE}
    lesson_id = {role: lessons[source_id]['Id'] for role, source_id in LESSONS.items()}
    topics = read(snapshot, 'study_catalog_topics')
    by_id = {t['Id']: t for t in topics}
    by_source = {t['SourceId']: t for t in topics if t['Source'] == SOURCE}
    parents = {t['ParentId'] for t in topics if t['ParentId']}
    leaves = lambda role: [t for t in topics if t['LessonId'] == lesson_id[role] and t['Id'] not in parents]
    links = read(snapshot, 'study_catalog_topic_outcomes')
    linked = defaultdict(list)
    for link in links:
        linked[link['TopicId']].append(link)
    outcomes = {o['Id']: o for o in read(snapshot, 'study_catalog_outcomes')}
    editions = {e['Id']: e['Code'] for e in read(snapshot, 'official_curriculum_editions')}
    shares = defaultdict(list)
    for share in read(snapshot, 'study_catalog_topic_outcome_shares'):
        shares[(share['TopicId'], share['OutcomeId'])].append(share)

    # 1. AYT Tarih leaves without outcome links -> the outcomes of their same-named TYT Tarih twin.
    tyt = defaultdict(list)
    for topic in leaves('tyt_tarih'):
        tyt[norm(topic['Name'])].append(topic)
    rows = []
    for topic in sorted(leaves('ayt_tarih'), key=lambda t: t['SourceId']):
        if linked[topic['Id']]:
            continue
        twins = tyt[norm(topic['Name'])]
        if len(twins) != 1:
            raise SystemExit(f"AYT Tarih leaf without exactly one TYT twin: {topic['Name']}")
        twin = twins[0]
        for link in linked[twin['Id']]:
            outcome = outcomes[link['OutcomeId']]
            if link['IsApproved'] != 't' or editions[outcome['EditionId']] != TAR_EDITION:
                raise SystemExit(f"Unexpected twin link on {twin['Name']}")
            share, = shares[(twin['Id'], outcome['Id'])]
            rows.append({'ayt_topic_source_id': topic['SourceId'], 'tyt_topic_source_id': twin['SourceId'],
                         'edition_code': TAR_EDITION, 'outcome_code': outcome['Code'],
                         'learning_minutes': share['LearningMinutes'], 'practice_minutes': share['PracticeMinutes'],
                         'check_minutes': share['CheckMinutes'], 'ayt_topic_name': topic['Name'],
                         'tyt_topic_name': twin['Name'], 'outcome_name': outcome['Name']})
    rows.sort(key=lambda r: (r['ayt_topic_source_id'], r['outcome_code']))

    # 2. TDP Matematik leaves -> lower median of comparable TYT Matematik leaf minutes ("koçluk tahmini").
    tyt_math = {t['SourceId']: int(t['EstimatedMinutes']) for t in leaves('tyt_matematik')}
    fallback = lower_median(list(tyt_math.values()))
    tdp_rows = []
    tdp_leaves = sorted(leaves('tdp_matematik'), key=lambda t: t['SourceId'])
    if {t['SourceId'] for t in tdp_leaves} != set(TDP_COMPARABLES):
        raise SystemExit('TDP Matematik leaves differ from the reviewed mapping.')
    for topic in tdp_leaves:
        comparable = TDP_COMPARABLES[topic['SourceId']]
        minutes = [tyt_math[source_id] for source_id in comparable]
        reference = TDP_LGS_REFERENCE.get(topic['SourceId'], [])
        tdp_rows.append({'topic_source_id': topic['SourceId'],
                         'proposed_minutes': lower_median(minutes) if minutes else fallback,
                         'yield_weight': 3,
                         'method': 'comparable-tyt-median' if minutes else 'tyt-matematik-median-fallback',
                         'comparable_tyt_source_ids': ' '.join(comparable),
                         'comparable_minutes': ' '.join(map(str, minutes)),
                         'lgs_reference_minutes': sum(int(by_source[s]['EstimatedMinutes']) for s in reference) if reference else '',
                         'topic_name': topic['Name']})

    (out / 'data').mkdir(parents=True, exist_ok=True)
    for name, data in (('ayt-tarih-links.csv', rows), ('tdp-matematik-estimates.csv', tdp_rows)):
        with open(out / 'data' / name, 'w', newline='', encoding='utf-8') as handle:
            writer = csv.DictWriter(handle, fieldnames=list(data[0]), lineterminator='\n')
            writer.writeheader()
            writer.writerows(data)

    link_values = ',\n'.join(
        f"  ({sql(r['ayt_topic_source_id'])}, {sql(r['tyt_topic_source_id'])}, {sql(r['edition_code'])}, {sql(r['outcome_code'])}, "
        f"{r['learning_minutes']}, {r['practice_minutes']}, {r['check_minutes']}, "
        f"{sql(LINK_EVIDENCE.format(tyt=r['tyt_topic_name'], edition=r['edition_code']))})" for r in rows)
    tdp_values = ',\n'.join(f"  ({sql(r['topic_source_id'])}, {r['proposed_minutes']})" for r in tdp_rows)
    replacements = {'{{AYT_LINK_VALUES}}': link_values, '{{AYT_LINK_COUNT}}': str(len(rows)),
                    '{{AYT_TOPIC_COUNT}}': str(len({r['ayt_topic_source_id'] for r in rows})),
                    '{{TDP_VALUES}}': tdp_values, '{{TDP_COUNT}}': str(len(tdp_rows))}
    for template in sorted((HERE / 'templates').glob('*.sql.in')):
        text = template.read_text(encoding='utf-8')
        for key, value in replacements.items():
            text = text.replace(key, value)
        if '{{' in text:
            raise SystemExit(f'Unrendered placeholder in {template.name}')
        (out / template.name[:-3]).write_text(text, encoding='utf-8')
    return len(rows), len(tdp_rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--snapshot', required=True, type=Path)
    parser.add_argument('--out', type=Path, default=HERE)
    args = parser.parse_args()
    print('ayt links %s | tdp leaves %s' % build(args.snapshot, args.out))
