#!/usr/bin/env python3
"""Tasarım değişkenlerini üretir: clients/shared/styles/_design-tokens.scss. Okunurluk eşikleri tutmazsa durur.

Çalıştır: python3 tools/brand/design_tokens.py"""
import pathlib, re, sys
B = {
 'maxkoc': {
  'light': dict(font="'IBM Plex Sans'", bg='#EDF2F8', surface='#FFFFFF', text='#18222F', muted='#4F5D6F', line='#DCE4EE', field='#7D8B9C', primary='#1F4E8C', primaryHover='#183F72', onPrimary='#FFFFFF', link='#1F4E8C', focus='#1F4E8C', accent='#FFE066', onAccent='#18222F', soft='#E6EEFB', visualBg='#E3ECF8', visualText='#18222F', visualMuted='#3E4C5E', nav='rgba(31, 78, 140, 0.08)', mathBg='#E6EEFB', mathFg='#1E4FA3', turkishBg='#FDEBDD', turkishFg='#9A3412', scienceBg='#E3F3E8', scienceFg='#166534', historyBg='#F1E8FB', historyFg='#6B21A8'),
  'dark': dict(bg='#0F141B', surface='#161D27', text='#E8EDF3', muted='#A3B0C0', line='#2A3544', field='#5E6E82', primary='#7FB0F0', primaryHover='#A3C7F5', onPrimary='#0F141B', link='#7FB0F0', focus='#E6C84A', accent='#E6C84A', onAccent='#0F141B', soft='#1E2B3D', visualBg='#0F141B', visualText='#E8EDF3', visualMuted='#A3B0C0', nav='rgba(127, 176, 240, 0.06)', mathBg='#1E2B3D', mathFg='#9CC2F5', turkishBg='#3A2614', turkishFg='#F5B97A', scienceBg='#183326', scienceFg='#8FE0B8', historyBg='#2C1D3A', historyFg='#D3B4F5')},
 'eduivme': {
  'light': dict(font="'IBM Plex Sans'", bg='#F4F5F7', surface='#FFFFFF', text='#1C2430', muted='#555F6D', line='#E1E4E9', field='#7A8492', primary='#1C2430', primaryHover='#2E3A49', onPrimary='#FFFFFF', link='#1C2430', focus='#1C2430', accent='#F2B33D', onAccent='#1C2430', soft='#ECEEF2', visualBg='#1C2430', visualText='#FFFFFF', visualMuted='#C9D2DC', nav='rgba(28, 36, 48, 0.05)'),
  'dark': dict(bg='#12161C', surface='#1A2029', text='#E6E9EE', muted='#A7B0BC', line='#2B3340', field='#677385', primary='#F2B33D', primaryHover='#F5C46A', onPrimary='#1C2430', link='#F2B33D', focus='#F2B33D', accent='#F2B33D', onAccent='#1C2430', soft='#26303D', visualBg='#0D1116', visualText='#FFFFFF', visualMuted='#C9D2DC', nav='rgba(255, 255, 255, 0.03)')},
 'ho': {
  'light': dict(font="'Lexend'", bg='#EAF4F0', surface='#FFFFFF', text='#10201D', muted='#4A5F5B', line='#D3E4DE', field='#6F8A84', primary='#0B7A64', primaryHover='#096553', onPrimary='#FFFFFF', link='#0B7A64', focus='#C2610A', accent='#E8730C', onAccent='#10201D', soft='#E3F3ED', visualBg='#0B7A64', visualText='#FFFFFF', visualMuted='#E1F2EC', nav='rgba(11, 122, 100, 0.09)'),
  'dark': dict(bg='#0C1413', surface='#13201E', text='#E6F0EE', muted='#9FB5B0', line='#24342F', field='#5C7871', primary='#3CC9A8', primaryHover='#62D6BA', onPrimary='#0C1413', link='#3CC9A8', focus='#FF9A3D', accent='#FF9A3D', onAccent='#0C1413', soft='#173029', visualBg='#0A3F35', visualText='#FFFFFF', visualMuted='#E1F2EC', nav='rgba(60, 201, 168, 0.06)')},
}
STATE = {'light': dict(danger='#B42318', dangerSoft='#FEF3F2', dangerLine='#F3B8B2', warning='#8A5300', warningSoft='#FDF0D5', success='#11624F', successSoft='#E3F4EE'),
         'dark': dict(danger='#FDA29B', dangerSoft='#3A1714', dangerLine='#7A2A23', warning='#F5C76A', warningSoft='#3A2E14', success='#8FE0C2', successSoft='#16302A')}
def L(h):
    h=h.lstrip('#'); c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    c=[x/12.92 if x<=0.03928 else ((x+0.055)/1.055)**2.4 for x in c]
    return 0.2126*c[0]+0.7152*c[1]+0.0722*c[2]
def cr(a,b):
    a,b=L(a),L(b); return (max(a,b)+0.05)/(min(a,b)+0.05)

def check():
    bad = []
    for br, modes in B.items():
        for mode in ('light', 'dark'):
            t = {**modes['light'], **modes[mode], **STATE[mode]}
            for a, b, need in [('text', 'bg', 4.5), ('text', 'surface', 4.5), ('muted', 'bg', 4.5), ('muted', 'surface', 4.5),
                               ('link', 'surface', 4.5), ('link', 'bg', 4.5), ('onPrimary', 'primary', 4.5), ('onPrimary', 'primaryHover', 4.5),
                               ('field', 'surface', 3), ('focus', 'surface', 3), ('focus', 'bg', 3), ('visualText', 'visualBg', 4.5),
                               ('visualMuted', 'visualBg', 4.5), ('danger', 'dangerSoft', 4.5), ('warning', 'warningSoft', 4.5),
                               ('success', 'successSoft', 4.5), ('onAccent', 'accent', 4.5), ('text', 'soft', 4.5), ('link', 'soft', 4.5), ('mathFg', 'mathBg', 4.5),
                               ('turkishFg', 'turkishBg', 4.5), ('scienceFg', 'scienceBg', 4.5), ('historyFg', 'historyBg', 4.5)]:
                if a not in t:
                    continue
                r = cr(t[a], t[b])
                if r < need:
                    bad.append(f'{br} {mode}: {a} / {b} = {r:.2f} (en az {need})')
    return bad
kebab=lambda s: re.sub(r'([A-Z])', lambda m: '-'+m[1].lower(), s)
SHAPE = {'maxkoc': dict(radius='10px', radiusControl='8px', radiusButton='8px', shadow='0 1px 2px rgba(23, 43, 77, 0.06), 0 8px 20px -10px rgba(23, 43, 77, 0.18)'),
         'eduivme': dict(radius='10px', radiusControl='8px', radiusButton='8px', shadow='0 1px 2px rgba(28, 36, 48, 0.06), 0 8px 20px -12px rgba(28, 36, 48, 0.18)'),
         'ho': dict(radius='18px', radiusControl='14px', radiusButton='999px', shadow='0 1px 2px rgba(11, 60, 50, 0.06), 0 8px 20px -10px rgba(11, 60, 50, 0.18)')}
DARK = ":is(html.dark, [data-sp-theme='dark'], [data-theme='dark'])"
out = ['''// eduivme tasarım değişkenleri (redesign, 18.11.2026). Kurallar: .claude/skills/eduivme-design/SKILL.md
// Üretici: tools/brand/design_tokens.py. Elle değiştirme; üreticiyi değiştirip yeniden üret.
// Kullanım: bir kapsayıcıya data-brand="maxkoc" | "eduivme" | "ho" ver; bileşenler yalnız --ds-* değişkenlerini kullanır.
// Koyu tema mevcut düzenlerden gelir: html.dark (admin-panel, staff-portal) ve data-sp-theme / data-theme (Hızlı Okuma).
// Yazı ve zemin çiftleri en az 4,5:1; kenarlık ve odak halkası en az 3:1 (üretici bunu denetler).
''']
def block(sel, d):
    lines=[f'{sel} {{']
    for k,v in d.items():
        if k=='font': v=f"{v}, system-ui, -apple-system, 'Segoe UI', sans-serif"
        lines.append(f'  --ds-{kebab(k)}: {v};')
    return '\n'.join(lines)+'\n}\n'
for br in B:
    light={**B[br]['light'],**STATE['light'],**SHAPE[br]}
    out.append(block(f"[data-brand='{br}']", light))
    dark={**B[br]['dark'],**STATE['dark'],'shadow':'none'}
    out.append(block(f"{DARK} [data-brand='{br}'],\n[data-brand='{br}']{DARK}", dark))
out.append('''// Eski bileşenler (--ui-*) marka kapsamı içinde yeni renkleri kullanır; böylece kayıt formları gibi henüz yenilenmemiş
// parçalar da aynı kimlikte görünür.
[data-brand] {
  --ui-brand: var(--ds-primary);
  --ui-brand-strong: var(--ds-primary-hover);
  --ui-brand-hover: var(--ds-primary-hover);
  --ui-brand-soft: var(--ds-soft);
  --ui-brand-contrast: var(--ds-on-primary);
  --ui-brand-text: var(--ds-link);
  --ui-brand-spark: var(--ds-accent);
  --ui-accent: var(--ds-accent);
  --ui-accent-soft: color-mix(in srgb, var(--ds-accent) 14%, var(--ds-surface));
  --ui-accent-text: var(--ds-warning);
  --ui-info: var(--ds-link);
  --ui-info-soft: var(--ds-soft);
  --ui-surface-raised: var(--ds-surface);
  --ui-page: var(--ds-bg);
  --ui-surface: var(--ds-surface);
  --ui-surface-muted: var(--ds-bg);
  --ui-border: var(--ds-line);
  --ui-border-strong: var(--ds-field);
  --ui-text: var(--ds-text);
  --ui-text-muted: var(--ds-muted);
  --ui-text-subtle: var(--ds-muted);
  --ui-danger: var(--ds-danger);
  --ui-danger-soft: var(--ds-danger-soft);
  --ui-warning: var(--ds-warning);
  --ui-warning-soft: var(--ds-warning-soft);
  --ui-success: var(--ds-success);
  --ui-success-soft: var(--ds-success-soft);
  --ui-font-body: var(--ds-font);
  --ui-font-display: var(--ds-font);
  // Angular Material bileşenleri (form alanları, menüler, onay kutuları) aynı kimliği kullanır.
  --mat-sys-primary: var(--ds-primary);
  --mat-sys-on-primary: var(--ds-on-primary);
  --mat-sys-primary-container: var(--ds-soft);
  --mat-sys-on-primary-container: var(--ds-text);
  --mat-sys-surface: var(--ds-surface);
  --mat-sys-surface-container-lowest: var(--ds-surface);
  --mat-sys-surface-container-low: var(--ds-surface);
  --mat-sys-surface-container: var(--ds-surface);
  --mat-sys-surface-container-high: var(--ds-surface);
  --mat-sys-surface-container-highest: var(--ds-bg);
  --mat-sys-on-surface: var(--ds-text);
  --mat-sys-on-surface-variant: var(--ds-muted);
  --mat-sys-outline: var(--ds-field);
  --mat-sys-outline-variant: var(--ds-line);
  --mat-sys-error: var(--ds-danger);
  --mat-sys-body-large-font: var(--ds-font);
  --mat-sys-body-medium-font: var(--ds-font);
  --mat-sys-body-small-font: var(--ds-font);
  --mat-sys-label-large-font: var(--ds-font);
  --mat-sys-label-medium-font: var(--ds-font);
  --mat-sys-label-small-font: var(--ds-font);
  --mat-sys-title-medium-font: var(--ds-font);
  --mat-form-field-outlined-container-shape: var(--ds-radius-control);
  --mat-form-field-container-text-font: var(--ds-font);
  --mat-form-field-outlined-label-text-font: var(--ds-font);
  font-family: var(--ds-font);
  color: var(--ds-text);
}
''')
problems = check()
if problems:
    sys.exit('Okunurluk eşiği tutmadı:\n' + '\n'.join(problems))
target = pathlib.Path(__file__).resolve().parents[2] / 'clients/shared/styles/_design-tokens.scss'
target.write_text('\n'.join(out))
print(target)
