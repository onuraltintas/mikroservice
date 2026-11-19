BG = {'edu': '#1C2430', 'koc': '#1F4E8C', 'ho': '#0B7A64', 'km': '#3A2E14'}
ACC = {'edu': '#F2B33D', 'koc': '#FFE066', 'ho': '#FF9A3D', 'km': '#F2B33D'}
def svg(size, body, label=None):
    a = f' role="img" aria-label="{label}"' if label else ' aria-hidden="true"'
    return f'<svg width="{size}" height="{size}" viewBox="0 0 64 64"{a}>{body}</svg>'
def tile(k): return f'<rect width="64" height="64" rx="15" fill="{BG[k]}"></rect>'

def page_mark(k, size, label=None):  # A: kıvrık sayfa köşesi
    lines = ''
    if k == 'koc': lines = '<path d="M21 31h18M21 39h12" stroke="#1F4E8C" stroke-width="3.5" stroke-linecap="round"></path><path d="M20 47h16" stroke="#FFE066" stroke-width="5" stroke-linecap="round"></path>'
    if k == 'ho': lines = '<path d="M21 30h20M21 37.5h20M21 45h13" stroke="#0B7A64" stroke-width="3" stroke-linecap="round"></path><rect x="31" y="34" width="3" height="7" rx="1.5" fill="#FF9A3D"></rect>'
    body = tile(k) + f'<path d="M15 13h22l12 12v26a2 2 0 0 1-2 2H17a2 2 0 0 1-2-2z" fill="#FFFFFF"></path><path d="M37 13v10a2 2 0 0 0 2 2h10z" fill="{ACC[k]}"></path>' + lines
    return svg(size, body, label)

def i_mark(k, size, label=None):  # B: i harfinin noktası
    extra = ''
    if k == 'koc': extra = '<path d="M22 50h20" stroke="#FFE066" stroke-width="5" stroke-linecap="round"></path>'
    if k == 'ho': extra = '<path d="M20 50h24" stroke="#FFFFFF" stroke-width="3" stroke-linecap="round" opacity="0.55"></path><rect x="36" y="46" width="3" height="8" rx="1.5" fill="#FF9A3D"></rect>'
    # Kurs Yönetimi (19.11.2026): three short marks under the "i", a week of lessons.
    if k == 'km': extra = '<rect x="18" y="50" width="7" height="4" rx="2" fill="#F2B33D"></rect><rect x="28.5" y="50" width="7" height="4" rx="2" fill="#F2B33D"></rect><rect x="39" y="50" width="7" height="4" rx="2" fill="#F2B33D" opacity="0.45"></rect>'
    body = tile(k) + '<rect x="27" y="27" width="10" height="20" rx="5" fill="#FFFFFF"></rect>' + f'<path d="M24 20c3-6 9-9 14-8" fill="none" stroke="{ACC[k]}" stroke-width="2.5" stroke-linecap="round" opacity="0.6"></path><circle cx="40" cy="14" r="6.5" fill="{ACC[k]}"></circle>' + extra
    return svg(size, body, label)

def book_mark(k, size, label=None):  # C: açık kitap, sağ sayfa yükselir
    lines = ''
    if k == 'koc': lines = '<path d="M17 34l9 2" stroke="#FFE066" stroke-width="4" stroke-linecap="round"></path>'
    if k == 'ho': lines = '<path d="M36 33l10-3M36 39l10-3" stroke="#0B7A64" stroke-width="2.5" stroke-linecap="round"></path><circle cx="47" cy="29.5" r="2.4" fill="#FF9A3D"></circle>'
    body = tile(k) + '<path d="M32 26c-5-4-11-5-18-4v24c7-1 13 0 18 4z" fill="#FFFFFF" opacity="0.92"></path><path d="M32 26c5-6 11-9 19-10v25c-8 1-14 4-19 9z" fill="#FFFFFF"></path>' + f'<circle cx="50" cy="12" r="5" fill="{ACC[k]}"></circle>' + lines
    return svg(size, body, label)

CURVE = "M15 47 C 30 47, 40 39, 45.5 23"
def curve_mark(k, size, label=None):  # D: mevcut ivme eğrisi
    extra = ''
    stroke, dot = '#FFFFFF', ACC[k]
    if k == 'koc': extra = '<path d="M15 18h22M15 27h13" stroke="#FFFFFF" stroke-width="4" stroke-linecap="round" opacity="0.9"></path>'; stroke, dot = '#FFE066', '#FFFFFF'
    if k == 'ho': extra = '<path d="M15 18h20M15 27h14" stroke="#FFFFFF" stroke-width="4" stroke-linecap="round" opacity="0.85"></path>'
    body = tile(k) + extra + f'<path d="{CURVE}" fill="none" stroke="{stroke}" stroke-width="6.5" stroke-linecap="round"></path><circle cx="47.5" cy="16.5" r="5.5" fill="{dot}"></circle>'
    return svg(size, body, label)
