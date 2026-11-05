#!/usr/bin/env python3
"""Paket B (05.11.2026): derives the approximate 2026 LGS score model (puan-modelleri-2026-lgs.csv) for the admin
"Puan tahmini verileri" import (year 2026).

Usage: python3 -I derive.py [--check]
       python3 -I derive.py --help

Input
  kaynak-meb-2026-lgs-testler.csv  MEB 2026 LGS Kılavuzu (questions, weights) and MEB ÖDSGM 2026 Merkezî Sınav Raporu
                                   (mean item difficulty p̄ and reliability per test).
Method: see README.md. --check recomputes and fails if the CSV differs.
"""
import csv, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'puan-modelleri-2026-lgs.csv')
HEADER = 'tur;puan_turu;sinav;ders;deger;siralama;yuzdelik'

# MEB does not publish the tests' means and standard deviations nor the smallest and largest TASP. Assumptions (README):
# the largest TASP is the full-net profile (452 students answered every question right in 2026), so full net = 500;
# the smallest TASP belongs to a student with a net of −1/5 of the questions in every test (e.g. 2 right, 18 wrong of 20).
MIN_NET_SHARE, MIN_NET_SHARE_LOW, MIN_NET_SHARE_HIGH = -0.2, 0.0, -1 / 3
UNCERTAINTY_CAP = 30   # points; MEB publishes nothing to check the middle of the scale against (README "Aralık").


def read_tests():
    with open(os.path.join(HERE, 'kaynak-meb-2026-lgs-testler.csv'), encoding='utf-8') as f:
        rows = list(csv.DictReader(f, delimiter=';'))
    tests = {}
    for r in rows:
        k, p, rel = float(r['soru']), float(r['ortalama_guclugu']), float(r['guvenirlik'])
        # KR-20: r = k/(k−1) × (1 − Σpq/σ²) → σ² = Σpq / (1 − r(k−1)/k), with Σpq ≈ k p̄ (1 − p̄) (an upper bound).
        sd = math.sqrt(k * p * (1 - p) / (1 - rel * (k - 1) / k))
        tests[r['test']] = dict(n=k, w=float(r['agirlik']), sd=sd, name=r['ders'])
    return tests


def model(tests, min_share):
    """MSP = 100 + 400 × (TASP − min) / (max − min); TASP = Σ w × (50 + 10 × (net − mean) / sd). The means cancel:
    MSP = 500 − Σ c × (n − net), c = 400 × (w / sd) / Σ (w / sd) × (n − min_net)."""
    per_net = {k: t['w'] / t['sd'] for k, t in tests.items()}
    span = sum(per_net[k] * (t['n'] - min_share * t['n']) for k, t in tests.items())
    coef = {k: 400 * v / span for k, v in per_net.items()}
    base = 500 - sum(coef[k] * t['n'] for k, t in tests.items())
    return base, coef


def uncertainty(tests):
    """The ± range as a share of the distance to 500 (all lines meet at full net): the smallest TASP anywhere between a
    student with no negative net (0) and one who answered every question wrong (−1/3)."""
    _, mid = model(tests, MIN_NET_SHARE)
    worst = max(abs(model(tests, s)[1]['TUR'] / mid['TUR'] - 1) for s in (MIN_NET_SHARE_LOW, MIN_NET_SHARE_HIGH))
    return math.ceil(worst * 1000) / 1000


def fmt(x):
    return f'{x:.4f}'.rstrip('0').rstrip('.')


def build(verbose=True):
    tests = read_tests()
    base, coef = model(tests, MIN_NET_SHARE)
    k = uncertainty(tests)
    lines = [HEADER,
             '# Paket B · 2026 LGS puan modeli (YAKLAŞIK). MEB 2026 LGS Kılavuzu Bölüm 11 + 2026 Merkezî Sınav Raporu (madde güçlüğü, güvenirlik).',
             '# Tam net = 500 (452 öğrenci); ders oranları KR-20 ile türetilen standart sapmalardan; en küçük TASP varsayımı README.md\'de.',
             f'taban;LGS;;;{fmt(round(base, 4))};;']
    lines += [f'katsayi;LGS;LGS;{s};{fmt(round(c, 4))};;' for s, c in coef.items()]
    lines += [f'belirsizlik;LGS;;;{fmt(k)};;', f'belirsizlik_siniri;LGS;;;{UNCERTAINTY_CAP};;']
    if verbose: report(tests, base, coef, k)
    return '\n'.join(lines) + '\n'


def report(tests, base, coef, k):
    print('== Türetilmiş standart sapmalar ve katsayılar (puan / net) ==')
    for s, t in tests.items(): print(f'{s} ({t["name"]}): ss ≈ {t["sd"]:.2f} · {coef[s]:.3f} puan/net')
    print(f'taban (0 net) {base:.1f} · aralık ±{k:.3f} × (500 − puan), en çok ±{UNCERTAINTY_CAP}')
    print('\n== Örnek profiller (toplam net → puan ± aralık) ==')
    for share in (1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4):
        nets = {s: t['n'] * share for s, t in tests.items()}
        sc = max(100, base + sum(coef[s] * nets[s] for s in tests))
        total = sum(nets.values())
        print(f'her dersin %{share * 100:.0f}\'i ({total:.0f} net): {sc:.1f} ± {min(UNCERTAINTY_CAP, k * (500 - sc)):.1f}')
    one = {s: t['n'] for s, t in tests.items()}
    for s in tests:
        print(f'tam net, {s} 1 yanlış: {500 - coef[s] * 4 / 3:.2f}')


def parse_args(argv):
    if '-h' in argv or '--help' in argv:
        print(__doc__)
        sys.exit(0)
    if any(a != '--check' for a in argv):
        print(__doc__, file=sys.stderr)
        sys.exit(2)
    return '--check' in argv


if __name__ == '__main__':
    check = parse_args(sys.argv[1:])
    text = build()
    if check:
        with open(OUT, encoding='utf-8') as f:
            if f.read() != text: sys.exit('puan-modelleri-2026-lgs.csv is out of date')
        print('\nOK: puan-modelleri-2026-lgs.csv is up to date')
    else:
        with open(OUT, 'w', encoding='utf-8', newline='\n') as f: f.write(text)
        print(f'\nwrote {OUT}')
