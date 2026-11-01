#!/usr/bin/env python3
"""KM4: derives the 2026 YKS score models (puan-modelleri-2026.csv) for the admin "Puan tahmini verileri" import.

Usage: python3 -I derive.py <target_catalog.csv from the production catalog export> [--check]

Inputs
  kaynak-osym-2026-yks-testler.csv  ÖSYM 2026-YKS Sayısal Bilgiler: per-test valid questions, mean and standard deviation.
  kaynak-osym-2026-yks-dagilim.csv  ÖSYM 2026-YKS Sayısal Bilgiler: cumulative distribution of exam and placement scores.
  target_catalog.csv                2026 placements (MinimumScore = placement score of the last placed candidate, Rank =
                                    that candidate's başarı sırası) per programme. Read-only export, never committed.
Method: see README.md. --check recomputes and fails if puan-modelleri-2026.csv differs.
"""
import csv, math, os, random, statistics, sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'puan-modelleri-2026.csv')
HEADER = 'tur;puan_turu;sinav;ders;deger;siralama;yuzdelik'

# ÖSYM 2020 YKS Değerlendirme Raporu, p. 12-13 (Tablo 1 and 2): test weights (%) per score type.
TYT_W = {'TUR': 33, 'SOS': 17, 'TMAT': 33, 'FEN': 17}
WEIGHTS = {
    'TYT': dict(TYT_W),
    'SAY': {**{k: v * 0.4 for k, v in TYT_W.items()}, 'AMAT': 30, 'FIZ': 10, 'KIM': 10, 'BIO': 10},
    'EA': {**{k: v * 0.4 for k, v in TYT_W.items()}, 'EDB': 18, 'TAR1': 7, 'COG1': 5, 'AMAT': 30},
    'SOZ': {**{k: v * 0.4 for k, v in TYT_W.items()}, 'EDB': 18, 'TAR1': 7, 'COG1': 5, 'TAR2': 8, 'COG2': 8, 'FEL': 9, 'DIN': 5},
    'DIL': {**{k: v * 0.4 for k, v in TYT_W.items()}, 'ENG': 60},
}
# Practice-exam sections of the platform (PracticeExamRules) -> ÖSYM test.
SECTIONS = {
    'TYT': {'TUR': 'TUR', 'MAT': 'TMAT', 'TAR': 'SOS', 'COG': 'SOS', 'FEL': 'SOS', 'DIN': 'SOS', 'FIZ': 'FEN', 'KIM': 'FEN', 'BIO': 'FEN'},
    'AYT_SAY': {'MAT': 'AMAT', 'FIZ': 'FIZ', 'KIM': 'KIM', 'BIO': 'BIO'},
    'AYT_EA': {'MAT': 'AMAT', 'EDB': 'EDB', 'TAR1': 'TAR1', 'COG1': 'COG1'},
    'AYT_SOZ': {'EDB': 'EDB', 'TAR1': 'TAR1', 'COG1': 'COG1', 'TAR2': 'TAR2', 'COG2': 'COG2', 'FEL': 'FEL', 'DIN': 'DIN'},
    'YDT_EN': {'ENG': 'ENG'},
}
EXAMS = {'TYT': ['TYT'], 'SAY': ['TYT', 'AYT_SAY'], 'EA': ['TYT', 'AYT_EA'], 'SOZ': ['TYT', 'AYT_SOZ'], 'DIL': ['TYT', 'YDT_EN']}
# ÖSYM: the TYT profile with 5 net in Türkçe and Temel Matematik and 2,5 in the others gets 150; for SAY/SÖZ/EA/DİL the
# profile with 20% of each test's questions gets 170; the highest weighted score gets 500.
ANCHOR = {'TYT': 150.0}
OBP_WEIGHT = 0.12  # ÖSYM 2026-YKS SSS Q50: OBP × 0,12 is added to the exam score (halved for candidates placed in 2025).
CATALOG_TYPE = {'TYT': 'TYT', 'SAY': 'SAY', 'EA': 'EA', 'SOZ': 'SOZ', 'YDT': 'DIL'}
TYPES = ['TYT', 'SAY', 'EA', 'SOZ', 'DIL']


def read_tests(sd_column='son_sinif_standart_sapma'):
    with open(os.path.join(HERE, 'kaynak-osym-2026-yks-testler.csv'), encoding='utf-8') as f:
        return {r['test']: dict(n=float(r['gecerli_soru']), mean=float(r['son_sinif_ortalama']), sd=float(r[sd_column]),
                                mean_all=float(r['tum_ortalama'])) for r in csv.DictReader(f, delimiter=';')}


def read_distribution():
    rows = {}
    with open(os.path.join(HERE, 'kaynak-osym-2026-yks-dagilim.csv'), encoding='utf-8') as f:
        for r in csv.DictReader(f, delimiter=';'):
            rows.setdefault(r['tablo'], []).append((float(r['puan_ve_ustu']), {t: int(r[t]) for t in TYPES}))
    return rows


def coefficients(score_type, tests):
    """score = base + Σ c_i × net_i on the upper part of ÖSYM's 100–500 scale (see README)."""
    weights = WEIGHTS[score_type]
    per_net = {k: w / tests[k]['sd'] for k, w in weights.items()}             # ∝ ASP change per net
    anchor = {k: (5 if k in ('TUR', 'TMAT') else 2.5) for k in weights} if score_type == 'TYT' else {k: 0.2 * tests[k]['n'] for k in weights}
    anchor_score = ANCHOR.get(score_type, 170.0)
    full = sum(per_net[k] * tests[k]['n'] for k in weights)
    at_anchor = sum(per_net[k] * anchor[k] for k in weights)
    slope = (500 - anchor_score) / (full - at_anchor)
    base = anchor_score - slope * at_anchor
    return base, {k: slope * v for k, v in per_net.items()}


def score(base, coef, nets):
    return base + sum(coef[k] * nets.get(k, 0) for k in coef)


def catalog_pairs(path):
    pairs = {t: [] for t in TYPES}
    with open(path, encoding='utf-8') as f:
        for r in csv.DictReader(f):
            if r['CatalogType'] != 'UniversityProgram' or r['PlacementYear'] != '2026' or not r['Rank'] or not r['MinimumScore']:
                continue
            t = CATALOG_TYPE.get(r['ScoreType'])
            if t and float(r['MinimumScore']) > 0:
                pairs[t].append((float(r['MinimumScore']), int(r['Rank'])))
    return pairs


def log_interp(points, s):
    """points: (score, rank) sorted by descending score; log-linear between rows, clamped outside."""
    if s >= points[0][0]: return points[0][1]
    if s <= points[-1][0]: return points[-1][1]
    for (s1, r1), (s2, r2) in zip(points, points[1:]):
        if s1 >= s >= s2:
            t = (s1 - s) / (s1 - s2)
            return math.exp(math.log(r1) + t * (math.log(r2) - math.log(r1)))


def service_rank(table, s):
    """Same reading as CoachingScoreModel.RankOf: linear in rank between rows, nearest row outside."""
    if s >= table[0][0]: return table[0][1]
    if s <= table[-1][0]: return table[-1][1]
    upper = [r for r in table if r[0] >= s][-1]; lower = [r for r in table if r[0] < s][0]
    t = (upper[0] - s) / (upper[0] - lower[0])
    return round(upper[1] + t * (lower[1] - upper[1]))


def isotonic_knots(points, width=2.0):
    """Pool-adjacent-violators on log(rank) by descending score, then 2-point bins (geometric mean) to keep the table small."""
    blocks = []
    for s, r in sorted(points, key=lambda p: -p[0]):
        blocks.append([math.log(r), 1, s, s])
        while len(blocks) > 1 and blocks[-2][0] / blocks[-2][1] > blocks[-1][0] / blocks[-1][1]:
            b = blocks.pop(); blocks[-1][0] += b[0]; blocks[-1][1] += b[1]; blocks[-1][2] = b[2]
    knots = [((b[2] + b[3]) / 2, b[0] / b[1]) for b in blocks]
    binned = {}
    for s, lr in knots: binned.setdefault(math.floor(s / width), []).append((s, lr))
    out = []
    for key in sorted(binned, reverse=True):
        group = binned[key]
        s, lr = statistics.mean(x[0] for x in group), statistics.mean(x[1] for x in group)
        if out and lr < math.log(out[-1][1]): lr = math.log(out[-1][1])
        out.append((s, math.exp(lr)))
    return out


def rank_table(score_type, points, osym):
    """Placement score -> başarı sırası. Within the catalog's score range the isotonic fit of the 2026 placements is read
    every 2 points; outside it ÖSYM's cumulative placement distribution (count at or above the score) is used."""
    total = osym[-1][1][score_type]
    brackets = [(b, c[score_type]) for b, c in osym]
    knots = isotonic_knots(points)
    # The fit is trusted only where at least three programmes lie beyond it; the thin edges come from ÖSYM.
    scores = sorted((p[0] for p in points), reverse=True)
    hi, lo = scores[min(2, len(scores) - 1)], scores[max(-3, -len(scores))]
    rows = {560.0: 1}
    for b, c in brackets:
        if b > hi or b < lo: rows[b] = c
    s = math.floor(hi / 2) * 2
    while s >= lo:
        rows[float(s)] = round(log_interp(knots, s)); s -= 2
    table, best = [], 0
    for s in sorted(rows, reverse=True):
        r = max(rows[s], best, 1); best = r
        table.append((s, r))
    return [(s, r, round(min(100.0, r / total * 100), 2)) for s, r in table], total


def outliers(points, brackets):
    """Pairs more than 25% away (in rank) from ÖSYM's own distribution are data errors (e.g. an empty programme's rank).
    Checked only between 150 and 530, where ÖSYM's 20-point brackets are dense enough to interpolate."""
    ref = sorted(brackets, key=lambda p: -p[0])
    keep, drop = [], []
    for s, r in points:
        ok = not 150 <= s <= 530 or abs(math.log(r / log_interp(ref, s))) <= math.log(1.25)
        (keep if ok else drop).append((s, r))
    return keep, drop


def fmt(x):
    return f'{x:.4f}'.rstrip('0').rstrip('.') if isinstance(x, float) else str(x)


def build(catalog_path, verbose=True):
    tests = read_tests(); dist = read_distribution(); pairs = catalog_pairs(catalog_path)
    lines = [HEADER,
             '# KM4 · 2026 YKS puan modelleri (YAKLAŞIK). Katsayılar: ÖSYM 2026-YKS Sayısal Bilgiler standart sapmaları + ÖSYM standart puan yöntemi.',
             '# Sıralama: 2026 yerleştirme puanı -> başarı sırası (2026 yerleşme verisi, ÖSYM yığınsal dağılımıyla doğrulandı). Ayrıntı: README.md']
    log = []
    for t in TYPES:
        base, coef = coefficients(t, tests)
        lines.append(f'taban;{t};;;{fmt(round(base, 4))};;')
        lines.append(f'obp;{t};;;{OBP_WEIGHT};;')
        for exam in EXAMS[t]:
            for section, test in SECTIONS[exam].items():
                lines.append(f'katsayi;{t};{exam};{section};{fmt(round(coef[test], 4))};;')
        brackets = [(b, c[t]) for b, c in dist['yerlestirme']]
        keep, drop = outliers(pairs[t], brackets)
        table, total = rank_table(t, keep, dist['yerlestirme'])
        for s, r, p in table: lines.append(f'siralama;{t};;;{fmt(s)};{r};{p:.2f}')
        log.append((t, base, coef, len(pairs[t]), len(drop), table, keep))
    text = '\n'.join(lines) + '\n'
    if verbose: report(tests, dist, log)
    return text


def report(tests, dist, log):
    print('== Katsayılar (puan / net) ==')
    for t, base, coef, n, dropped, table, keep in log:
        print(f'{t}: taban {base:.2f} · ' + ', '.join(f'{k} {v:.3f}' for k, v in coef.items()))
    print('\n== Ortalama kontrolü: ortalama netlerle hesaplanan puan / ÖSYM sınav puanı dağılımının ortalaması ==')
    for t, base, coef, *_ in log:
        brackets = dist['sinav']; total = brackets[-1][1][t]
        mean = (500 * brackets[0][1][t] + sum((brackets[i][1][t] - brackets[i - 1][1][t]) * (brackets[i][0] + 10) for i in range(1, len(brackets)))) / total
        takers = {'TYT': 2254830, 'DIL': 136745}.get(t, 1473064)  # valid candidates in the AYT / YDT İngilizce / TYT
        nets = {k: tests[k]['mean_all'] * (takers / total if (t == 'TYT' or k not in TYT_W) else 1) for k in coef}
        print(f'{t}: model {score(base, coef, nets):.1f} · ÖSYM {mean:.1f}')
    print('\n== Sıralama tablosu: geri tutulan programlar (%20) ve ÖSYM karşılaştırması ==')
    rnd = random.Random(2026)
    dist_pl = dist['yerlestirme']
    for t, base, coef, n, dropped, table, keep in log:
        pts = list(keep); rnd.shuffle(pts); k = len(pts) // 5
        held, train = pts[:k], pts[k:]
        trained, _ = rank_table(t, train, dist_pl)
        tbl = [(s, r) for s, r, _ in trained]
        errs = sorted(abs(service_rank(tbl, s) - r) / r for s, r in held)
        top = sorted(abs(service_rank(tbl, s) - r) / r for s, r in held if r <= 50000)
        cross = [abs(service_rank([(s, r) for s, r, _ in table], b) - c[t]) / c[t] for b, c in dist_pl if 150 <= b <= 550]
        print(f'{t}: {n} program ({dropped} aykırı atıldı), tablo {len(table)} satır · geri tutulan {len(held)}: medyan %{statistics.median(errs)*100:.2f}, '
              f'p90 %{errs[int(len(errs)*.9)]*100:.2f}, en kötü %{errs[-1]*100:.1f}; sıralama ≤50.000 olanlarda medyan %{(statistics.median(top) if top else 0)*100:.2f} · '
              f'ÖSYM dilimleri (150–550): medyan %{statistics.median(cross)*100:.2f}, en kötü %{max(cross)*100:.1f}')
    print('\n== Standart sapmaların yuvarlanmasına (±0,05) duyarlılık: her testte ortalamanın 0,5–2 sd üstündeki adayda puan farkı ==')
    for t, base, coef, *_ in log:
        worst = 0.0
        for seed in range(200):
            r = random.Random(seed)
            perturbed = {k: dict(v, sd=v['sd'] + r.uniform(-0.05, 0.05)) for k, v in tests.items()}
            b2, c2 = coefficients(t, perturbed)
            for z in (0.5, 1.0, 1.5, 2.0):  # a candidate z standard deviations above every test's mean
                nets = {k: min(tests[k]['n'], tests[k]['mean'] + z * tests[k]['sd']) for k in coef}
                worst = max(worst, abs(score(b2, c2, nets) - score(base, coef, nets)))
        print(f'{t}: en fazla ±{worst:.1f} puan')


if __name__ == '__main__':
    if len(sys.argv) < 2: sys.exit(__doc__)
    text = build(sys.argv[1])
    if '--check' in sys.argv:
        with open(OUT, encoding='utf-8') as f:
            if f.read() != text: sys.exit('puan-modelleri-2026.csv is out of date')
        print('\nOK: puan-modelleri-2026.csv is up to date')
    else:
        with open(OUT, 'w', encoding='utf-8', newline='\n') as f: f.write(text)
        print(f'\nwrote {OUT}')
