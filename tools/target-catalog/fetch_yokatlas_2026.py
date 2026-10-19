"""Read the same anonymous paginated search used by the official YÖK Atlas UI."""
import hashlib
import json
import time
import urllib.request
from pathlib import Path

URL='https://yokatlas.yok.gov.tr/api/tercih-kilavuz/search'
ROOT=Path('data/target-catalog/2026/raw/yokatlas-pages')
FIELDS=['yil','donem','kilavuzKodu','universiteId','universiteAdi','fymkAdi','birimAdi','puanTuru',
    'ilKodu','ilAdi','ilceAdi','universiteTuru','birimTuruId','ogrenimDiliAdi','bursOraniAdi',
    'ogrenimTuruAdi','ogrenimSuresi','kontenjan','gkY','minPuan','basariSirasi','kosul','minBasariSirasiKosul']


def main():
    ROOT.mkdir(parents=True,exist_ok=True)
    page=0
    while True:
        path=ROOT/f'{page:04}.json'
        if path.exists():
            data=json.loads(path.read_text('utf-8'))
        else:
            body=json.dumps(dict(filters={},page=page,size=100,sortBy='basariSirasi',direction='ASC')).encode()
            req=urllib.request.Request(URL,data=body,headers={'Content-Type':'application/json'})
            with urllib.request.urlopen(req,timeout=60) as response:
                raw=response.read()
            data=json.loads(raw)
            if data.get('yil')!=2026 or any(x.get('yil')!=2026 for x in data['content']):
                raise ValueError('Only 2026 data is authorized')
            data={**{k:v for k,v in data.items() if k!='content'},'content':[{k:x.get(k) for k in FIELDS} for x in data['content']],
                'responseSha256':hashlib.sha256(raw).hexdigest(),'sourceUrl':URL,'retrievedAt':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())}
            path.write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
        if page%20==0:print(f"page={page} totalPages={data['totalPages']} year={data['yil']}",flush=True)
        if data['last']:break
        page+=1
        time.sleep(.15)
    rows=[x for p in sorted(ROOT.glob('*.json')) for x in json.loads(p.read_text('utf-8'))['content']]
    if len(rows)!=data['totalElements'] or len({x['kilavuzKodu'] for x in rows})!=len(rows):
        raise ValueError('Pagination was incomplete or unstable; do not publish')
    print(f'Complete: {len(rows)} unique 2026 programs',flush=True)


if __name__=='__main__':main()
