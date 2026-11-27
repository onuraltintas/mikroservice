# Yeni metin yuvalarını hesaplar: her düz okuma egzersizinin aralığında en az 10 uygun metin, sonra her yaş ve seviye 40 metin.
# Girdi: canlı anlık görüntü + shared-pool/v1 + pool-questions/v1-v2. Çıktı: targets.json (öncelik sırasıyla).
import gzip, json, collections, os
D = os.path.dirname(os.path.abspath(__file__)); C = os.path.join(D, '..', '..')
S = os.path.join(C, 'live-snapshot/20261126/')
L = lambda f: [json.loads(l) for l in gzip.open(S + f)]
AGE = {'10000000-0000-0000-0000-000000000001': 'Çocuk', '10000000-0000-0000-0000-000000000002': 'Genç',
       '10000000-0000-0000-0000-000000000004': 'Genç yetişkin', '10000000-0000-0000-0000-000000000003': 'Yetişkin'}
ORDER = list(AGE)
texts = [t for t in L('texts.jsonl.gz') if t['is_active'] and not t['is_deleted'] and t['content']]
qs = [q for q in L('questions.jsonl.gz') if not q['is_deleted']]
qc = collections.Counter(q['reading_text_id'] for q in qs if 1 <= q['type'] <= 3 and 1 <= q['bloom_level'] <= 6
    and q['correct_answer'].strip().upper() in ('A', 'B', 'C', 'D') and all(q['option_' + x].strip() for x in 'abcd'))
for p in ['pool-questions/v1/questions.json', 'pool-questions/v2/questions.json']:
    for q in json.load(open(os.path.join(C, p))): qc[q['readingTextId']] += 1
moved = {m['id'] for m in json.load(open(os.path.join(C, 'shared-pool/v1/moves.json')))['moves']}
exid = lambda t: None if t['id'] in moved else t['exercise_id']
PLAIN = {'Chunking', 'FreeReading', 'RegressionReduction', 'RSVP', 'SubvocalizationReduction'}
bands = collections.defaultdict(list)
for e in L('exercises.jsonl.gz'):
    if e['is_deleted'] or not e['is_active'] or e['type_name'] not in PLAIN: continue
    ec = e['configuration_json']['engineConfig']; c = ec.get('content', {}); lo, hi = c.get('minWordCount'), c.get('maxWordCount')
    if lo is None: continue
    n = 4 if e['type_name'] == 'Chunking' else 0 if e['type_name'] == 'FreeReading' else ((c.get('questionCount') or 0) if ec.get('readingPurpose') == 'evaluation' else 0)
    k = sum(1 for t in texts if t['difficulty_level'] == e['difficulty_level'] and t['target_age_group_id'] in (None, e['target_age_group_id'])
            and exid(t) in (None, e['id']) and lo <= t['word_count'] <= hi and qc[t['id']] >= n)
    bands[(e['target_age_group_id'], e['difficulty_level'])].append({'exercise': e['type_name'], 'min': lo, 'max': hi, 'have': k})
pooled = collections.Counter((t['target_age_group_id'], t['difficulty_level']) for t in texts
    if exid(t) is None and t['target_age_group_id'] and qc[t['id']] >= 4)
slots = []
for cell in sorted(bands, key=lambda c: (ORDER.index(c[0]), c[1])):
    b = bands[cell]; total = pooled[cell]
    def pick(score):
        best = None
        for x in b:
            for w in range(x['min'], x['max'] + 1, 5):
                s = score(w)
                if best is None or s > best[0]: best = (s, w)
        return best[1]
    def add(w, kind):
        cover, lo, hi = [], 0, 10**6
        for x in sorted((x for x in b if x['min'] <= w <= x['max']), key=lambda x: x['have']):
            nlo, nhi = max(lo, x['min']), min(hi, x['max'])
            if not cover or nhi - nlo >= min(40, x['max'] - x['min'], hi - lo): cover.append(x); lo, hi = nlo, nhi
        slots.append({'age': AGE[cell[0]], 'ageGroupId': cell[0], 'level': cell[1], 'min': lo, 'max': hi, 'kind': kind,
                      'need': min(x['have'] for x in cover), 'exercises': sorted(x['exercise'] for x in cover)})
        for x in cover: x['have'] += 1
    while any(x['have'] < 10 for x in b):
        need = [x for x in b if x['have'] < 10]
        w = pick(lambda w: (sum(1 for x in need if x['min'] <= w <= x['max']), -min((x['have'] for x in need if x['min'] <= w <= x['max']), default=99)))
        add(w, 'band'); total += 1
    while total < 40:
        w = pick(lambda w: sum(1 / (1 + x['have']) for x in b if x['min'] <= w <= x['max']))
        add(w, 'fill'); total += 1
slots.sort(key=lambda s: (s['kind'] != 'band', s['need'], ORDER.index(s['ageGroupId']), s['level']))
for i, s in enumerate(slots, 1): s['slot'] = i
json.dump(slots, open(os.path.join(D, 'targets.json'), 'w'), ensure_ascii=False, indent=1)
print(len(slots), collections.Counter(s['kind'] for s in slots))
