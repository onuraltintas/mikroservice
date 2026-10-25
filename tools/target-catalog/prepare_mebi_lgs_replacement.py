"""Seal the reviewed MEBI export as PostgreSQL input; never infer missing fields."""
import hashlib
import json
from pathlib import Path

SOURCE = Path('data/target-catalog/2026/raw/mebi-lgs-full-20261020-export.json')
FILE_HASH = '73479b81ce93fae93d3d598f316d0b64f7ba16e7214b1d3a7e0cf201639eb752'


def prepare(source=SOURCE):
    raw = source.read_bytes()
    if hashlib.sha256(raw).hexdigest() != FILE_HASH:
        raise ValueError('The reviewed MEBI export has changed.')
    payload = json.loads(raw)
    rows = payload['items']
    if len(rows) != 3150 or len({row['id'] for row in rows}) != 3150:
        raise ValueError('Exactly 3150 unique MEBI programs are required.')
    compact = json.dumps(rows, ensure_ascii=False, separators=(',', ':'))
    if hashlib.sha256(compact.encode()).hexdigest() != payload['itemsSha256']:
        raise ValueError('Source item hash mismatch.')
    for index, page in enumerate(payload['pages']):
        part = rows[index * 100:(index + 1) * 100]
        digest = hashlib.sha256(json.dumps(part, ensure_ascii=False, separators=(',', ':')).encode()).hexdigest()
        if page['pageNumber'] != index + 1 or page['count'] != len(part) or page['sha256'] != digest:
            raise ValueError('Source page hash mismatch.')
    if len(payload['pages']) != 32 or payload['sourceYear'] is not None or payload['placementStage'] is not None:
        raise ValueError('Missing year and placement stage must remain unknown.')
    marker = '$mebi_lgs_20261020$'
    if marker in compact:
        raise ValueError('Unsafe source delimiter.')
    return (
        'CREATE TEMP TABLE incoming ON COMMIT DROP AS SELECT *, id AS target_id FROM jsonb_to_recordset('
        + marker + compact + marker + '::jsonb) AS r(id uuid, city text, town text, "schoolName" text, '
        '"schoolType" text, branch text, language text, "minScore" numeric);\n'
        'ALTER TABLE incoming ADD PRIMARY KEY (id);\n'
    )


if __name__ == '__main__':
    output = Path('artifacts/mebi-lgs-import-20261020/package')
    output.mkdir(parents=True, exist_ok=True)
    (output / 'incoming.sql').write_text(prepare(), encoding='utf-8', newline='\n')
    print(json.dumps({'rows': 3150, 'sourceSha256': FILE_HASH, 'output': str(output / 'incoming.sql')}))
