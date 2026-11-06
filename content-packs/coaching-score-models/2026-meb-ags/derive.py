#!/usr/bin/env python3
"""Paket B (05.11.2026): derives the approximate 2026 MEB-AGS score models (puan-modelleri-2026-meb-ags.csv) for the
admin "Puan tahmini verileri" import (year 2026).

Usage: python3 -I derive.py [--check]
       python3 -I derive.py --help

Input
  kaynak-osym-2026-meb-ags-testler.csv  ÖSYM "2026 MEB-AGS Sınav Sonuçlarına İlişkin Sayısal Bilgiler": per test the
                                        question count, raw-score (net) mean and standard deviation, candidates.
Method: see README.md (2026 MEB-AGS Kılavuzu 3.10). --check recomputes and fails if the CSV differs.
"""
import csv, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'puan-modelleri-2026-meb-ags.csv')
HEADER = 'tur;puan_turu;sinav;ders;deger;siralama;yuzdelik'

# 2026 MEB-AGS Kılavuzu 3.10: weights (%) of the AGS tests in P1; P2 halves them and adds ÖABT 50.
AGS_W = {'SOZ': 20, 'SAY': 20, 'TAR': 15, 'COG': 10, 'EGT': 25, 'MEV': 10}
# P2-16 (Din Kültürü ve Ahlak Bilgisi) and P2-17 (İHL Meslek Dersleri): the ÖABT is weighted by its three sub-tests.
DIN_W = {'P2_16': {'DIN_OAB': 15, 'DIN_TIB': 15, 'DIN_ITF': 20}, 'P2_17': {'DIN_OAB': 15, 'DIN_TIB': 20, 'DIN_ITF': 15}}
FIELDS = ['TUR', 'IMAT', 'FEN', 'SOS', 'TDE', 'TAR', 'COG', 'MAT', 'FIZ', 'KIM', 'BIO', 'REH', 'SINIF', 'OKUL', 'BED', 'OZEL']

# Not published by ÖSYM: X (mean AP), S (standard deviation of AP), B (highest AP). Assumptions (README "Varsayımlar"):
X = 50.0                    # AP is a weighted mean of standard scores (mean 50) with weights summing to 100 %.
# S = 10 × sqrt(Σa² + ρ(1 − Σa²)) for a common correlation ρ between the tests' standard scores. Central value ρ = 0,5;
# the shown range takes every possible ρ (0: independent tests, S = 10 × sqrt(Σa²); 1: identical tests, S = 10).
RHO, RHO_LOW, RHO_HIGH = 0.5, 0.0, 1.0
# B: the AP of the full-net profile (every test answered completely right).


def read_tests():
    with open(os.path.join(HERE, 'kaynak-osym-2026-meb-ags-testler.csv'), encoding='utf-8') as f:
        return {r['test']: dict(n=float(r['soru']), mean=float(r['ortalama']), sd=float(r['standart_sapma'])) for r in csv.DictReader(f, delimiter=';')}


def score_types():
    """(code, weights per test, practice sections: (exam, section) -> {test: share of the section's net})."""
    ags = {('AGS', k): {k: 1.0} for k in AGS_W}
    types = [('P1', dict(AGS_W), ags)]
    half = {k: v / 2 for k, v in AGS_W.items()}
    for field in FIELDS:
        types.append((f'P2_{field}', {**half, f'OABT_{field}': 50}, {**ags, (f'OABT_{field}', 'ALAN'): {f'OABT_{field}': 1.0}}))
    for code, din in DIN_W.items():
        # The platform's ÖABT practice exam records one 50-question section, so its net is split over the three sub-tests in
        # proportion to their question counts (10 / 23 / 17).
        share = {k: None for k in din}
        types.append((code, {**half, **din}, {**ags, ('OABT_DIN', 'ALAN'): share}))
    return types


def model(weights, sections, tests, rho):
    """score = base + Σ c × net on the 0–100 scale; AP = Σ w/100 × (50 + 10 × (net − mean) / sd)."""
    a = {k: w / 100 for k, w in weights.items()}
    split = {}
    for key, share in sections.items():
        if any(v is None for v in share.values()):
            total = sum(tests[k]['n'] for k in share)
            share = {k: tests[k]['n'] / total for k in share}
        split[key] = share
    ap_per_net = {key: sum(a[k] * 10 * s / tests[k]['sd'] for k, s in share.items()) for key, share in split.items()}
    ap_zero = 50 - sum(a[k] * 10 * tests[k]['mean'] / tests[k]['sd'] for k in a)
    b = 50 + sum(a[k] * 10 * (tests[k]['n'] - tests[k]['mean']) / tests[k]['sd'] for k in a)
    sum_sq = sum(x * x for x in a.values())
    s = 10 * math.sqrt(sum_sq + rho * (1 - sum_sq))
    d = 2 * (b - X) - s
    slope = 60 / d                               # points per AP unit: 70 + 30 × [2(AP − X) − S] / [2(B − X) − S]
    base = 70 + 30 * (2 * (ap_zero - X) - s) / d
    return dict(base=base, coef={key: slope * v for key, v in ap_per_net.items()}, b=b, s=s, ap_zero=ap_zero, ap_per_net=ap_per_net)


def score(m, nets):
    return m['base'] + sum(c * nets.get(key, 0) for key, c in m['coef'].items())


def uncertainty(weights, sections, tests):
    """The ± range as a share of the distance to 100: S from RHO_LOW / RHO_HIGH against the central RHO. All three lines
    meet at 100 (AP = B), so the gap is a fixed share of (100 − score)."""
    mid = model(weights, sections, tests, RHO)
    worst = 0.0
    for rho in (RHO_LOW, RHO_HIGH):
        other = model(weights, sections, tests, rho)
        for ap in (mid['b'] - 30, mid['b'] - 10):   # any AP below B gives the same share
            def at(m): return 70 + 30 * (2 * (ap - X) - m['s']) / (2 * (m['b'] - X) - m['s'])
            worst = max(worst, abs(at(other) - at(mid)) / (100 - at(mid)))
    return math.ceil(worst * 1000) / 1000


def fmt(x):
    return f'{x:.4f}'.rstrip('0').rstrip('.')


def build(verbose=True):
    tests = read_tests()
    lines = [HEADER,
             '# Paket B · 2026 MEB-AGS puan modelleri (YAKLAŞIK). ÖSYM 2026 MEB-AGS Sayısal Bilgiler (test ortalama/ss) + 2026 MEB-AGS Kılavuzu 3.10.',
             '# X = 50, B = tam net profilinin AP\'si, S = testler arası korelasyon 0,5 varsayımıyla (aralık: korelasyon 0–1). Ayrıntı: README.md']
    log = []
    for code, weights, sections in score_types():
        m = model(weights, sections, tests, RHO)
        k = uncertainty(weights, sections, tests)
        lines.append(f'taban;{code};;;{fmt(round(m["base"], 4))};;')
        for (exam, section), c in m['coef'].items():
            lines.append(f'katsayi;{code};{exam};{section};{fmt(round(c, 4))};;')
        lines.append(f'belirsizlik;{code};;;{fmt(k)};;')
        log.append((code, m, k))
    if verbose: report(tests, log)
    return '\n'.join(lines) + '\n'


def report(tests, log):
    print('== Puan türleri: taban, B (tam net AP), S, aralık payı ==')
    for code, m, k in log:
        print(f'{code}: taban {m["base"]:.2f} · B {m["b"]:.2f} · S {m["s"]:.2f} · aralık ±{k:.3f} × (100 − puan) · '
              + ', '.join(f'{e}/{s} {c:.3f}' for (e, s), c in m['coef'].items()))
    print('\n== Örnek profiller ==')
    for code, m, k in log:
        if code not in ('P1', 'P2_IMAT', 'P2_16', 'P2_17'): continue
        for label, z in (('ortalama', 0), ('ortalama + 1 ss', 1), ('ortalama + 2 ss', 2)):
            nets = {}
            for (exam, section) in m['coef']:
                test = section if exam == 'AGS' else exam
                t = tests[test] if test in tests else tests['OABT_DIN']
                nets[(exam, section)] = min(t['n'], t['mean'] + z * t['sd'])
            sc = score(m, nets)
            print(f'{code} {label}: {sc:.1f} ± {k * (100 - sc):.1f}')
        full = {key: (tests[key[1]]['n'] if key[0] == 'AGS' else 50) for key in m['coef']}
        print(f'{code} tam net: {score(m, full):.2f}')


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
            if f.read() != text: sys.exit('puan-modelleri-2026-meb-ags.csv is out of date')
        print('\nOK: puan-modelleri-2026-meb-ags.csv is up to date')
    else:
        with open(OUT, 'w', encoding='utf-8', newline='\n') as f: f.write(text)
        print(f'\nwrote {OUT}')
