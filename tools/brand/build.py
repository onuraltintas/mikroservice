"""Builds the eduivme brand files (marks, browser and app icons, social preview images).

Brand decision 18.11.2026: eduivme is the parent brand; maxkoç (Koçluk, maxkoc.com) and Master Hızlı Okuma
(masterhizliokuma.com) are its products. All three marks share the lower-case "i" whose dot jumps up (the learner
gaining momentum); each product keeps its own colour and a small line under the "i".

Run from the repository root:  python3 tools/brand/build.py
Needs rsvg-convert and ImageMagick (convert); the social images also need Python Playwright with Chromium and the
@fontsource packages installed in clients/admin-panel.
"""
import os
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from marks import i_mark  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
ADMIN = ROOT / 'clients/admin-panel/public'
STAFF = ROOT / 'clients/admin-panel/projects/staff-portal/public'
READING = ROOT / 'clients/speed-reading/public'
BG = {'edu': '#1C2430', 'koc': '#1F4E8C', 'ho': '#0B7A64', 'km': '#3A2E14'}
NAME = {'edu': 'eduivme', 'koc': 'maxkoç', 'ho': 'Master Hızlı Okuma', 'km': 'Kurs Yönetimi'}
SLUG = {'edu': 'eduivme', 'koc': 'maxkoc', 'ho': 'master-hizli-okuma', 'km': 'kurs-yonetimi'}


def svg(kind: str, maskable: bool = False) -> str:
    """A standalone SVG of a mark. A maskable icon fills the square and keeps the mark inside the 80 % safe zone."""
    body = i_mark(kind, 64, label=NAME[kind])
    inner = body[body.index('>') + 1:body.rindex('</svg>')]
    if maskable:
        inner = inner.replace('<rect width="64" height="64" rx="15"', '<rect width="64" height="64" rx="0"', 1)
        inner = f'<g transform="translate(6.4 6.4) scale(0.8)">{inner}</g>'
        inner = f'<rect width="64" height="64" fill="{BG[kind]}"></rect>' + inner
    return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" '
            f'aria-label="{NAME[kind]}"><title>{NAME[kind]}</title>{inner}</svg>\n')


def png(svg_text: str, size: int, out: Path) -> None:
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile('w', suffix='.svg', delete=False) as f:
        f.write(svg_text)
    subprocess.run(['rsvg-convert', '-w', str(size), '-h', str(size), '-o', str(out), f.name], check=True)
    os.unlink(f.name)


def ico(kind: str, out: Path) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        parts = []
        for size in (16, 32, 48):
            p = Path(tmp) / f'{size}.png'
            png(svg(kind), size, p)
            parts.append(str(p))
        subprocess.run(['convert', *parts, str(out)], check=True)


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')


def icons() -> None:
    for kind in ('edu', 'koc'):
        write(ADMIN / f'brand/{SLUG[kind]}.svg', svg(kind))
        png(svg(kind), 180, ADMIN / f'brand/{SLUG[kind]}-180.png')
        png(svg(kind, maskable=True), 192, ADMIN / f'brand/{SLUG[kind]}-192.png')
        png(svg(kind, maskable=True), 512, ADMIN / f'brand/{SLUG[kind]}-512.png')
        ico(kind, ADMIN / f'brand/{SLUG[kind]}.ico')
    ico('edu', ADMIN / 'favicon.ico')
    # Kurs Yönetimi has no site of its own yet; its mark is shown on eduivme.com.
    write(ADMIN / 'brand/kurs-yonetimi.svg', svg('km'))
    write(STAFF / 'brand/eduivme.svg', svg('edu'))
    ico('edu', STAFF / 'favicon.ico')

    write(READING / 'brand/master-hizli-okuma.svg', svg('ho'))
    ico('ho', READING / 'favicon.ico')
    png(svg('ho'), 16, READING / 'favicon-16.png')
    png(svg('ho'), 32, READING / 'favicon-32.png')
    png(svg('ho'), 192, READING / 'favicon.png')
    png(svg('ho'), 180, READING / 'assets/icons/apple-touch-icon.png')
    for size in (72, 96, 128, 144, 152, 192, 384, 512):
        png(svg('ho', maskable=True), size, READING / f'icons/icon-{size}x{size}.png')


SOCIAL = Path(__file__).parent / 'social.html'


def social() -> None:
    """Renders the 1200x630 preview cards from social.html with the self-hosted fonts."""
    from playwright.sync_api import sync_playwright
    fonts = ROOT / 'clients/admin-panel/node_modules/@fontsource'
    html = SOCIAL.read_text(encoding='utf-8').replace('FONTS_DIR', fonts.as_uri())
    for kind in ('edu', 'koc', 'ho'):
        html = html.replace(f'MARK_{kind.upper()}_SMALL', i_mark(kind, 14)).replace(f'MARK_{kind.upper()}', i_mark(kind, 62, label=NAME[kind]))
    with tempfile.NamedTemporaryFile('w', suffix='.html', delete=False, encoding='utf-8') as f:
        f.write(html)
    targets = {'edu': [ADMIN / 'brand/og-eduivme.png'], 'koc': [ADMIN / 'brand/og-maxkoc.png'], 'ho': [READING / 'brand/og-master-hizli-okuma.png']}
    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page(viewport={'width': 1200, 'height': 630})
        page.goto(Path(f.name).as_uri())
        page.evaluate('document.fonts.ready')
        page.wait_for_timeout(300)
        for kind, outs in targets.items():
            for out in outs:
                out.parent.mkdir(parents=True, exist_ok=True)
                page.locator(f'#card-{kind}').screenshot(path=str(out))
        browser.close()
    os.unlink(f.name)


if __name__ == '__main__':
    icons()
    social()
    print('brand files written')
