import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

// T1 (student panel redesign, 31.10.2026): one token set drives the student coaching portal in light and dark mode.
// Text must reach WCAG AA (4.5:1), interface parts 3:1, in both themes; pages may not carry their own colours.
const root = new URL('../../../clients/admin-panel/', import.meta.url).pathname;
const tokensFile = path.join(root, 'src/app/features/coaching-portal/layout/coaching-student-tokens.scss');
const tokens = fs.readFileSync(tokensFile, 'utf8');

function block(selectorStart) {
  const at = tokens.indexOf(selectorStart);
  assert.notEqual(at, -1, `missing token block ${selectorStart}`);
  const open = tokens.indexOf('{', at), close = tokens.indexOf('\n}', open);
  const vars = {};
  for (const match of tokens.slice(open + 1, close).matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) vars[match[1]] = match[2].trim();
  return vars;
}
const light = block('body:has(.coaching-shell), .coaching-shell {');
const dark = block('.dark body:has(.coaching-shell), .dark .coaching-shell {');
const resolve = (theme, value, seen = 0) => {
  const ref = /^var\((--[\w-]+)\)$/.exec(value ?? '');
  if (ref && seen < 5) return resolve(theme, theme[ref[1]] ?? light[ref[1]], seen + 1);
  return value;
};
const hex = (theme, name) => {
  const value = name.startsWith('#') ? name : resolve(theme, theme[name] ?? light[name]);
  assert.match(value ?? '', /^#[0-9a-f]{6}$/i, `${name} must resolve to a #rrggbb colour (got ${value})`);
  return value;
};
const luminance = value => {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(value.slice(i, i + 2), 16) / 255)
    .map(c => c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const ratio = (a, b) => { const [x, y] = [luminance(a), luminance(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

const required = ['--ui-page', '--ui-surface', '--ui-surface-muted', '--ui-surface-raised', '--ui-border', '--ui-border-strong',
  '--ui-text', '--ui-text-muted', '--ui-brand', '--ui-brand-strong', '--ui-brand-soft', '--ui-brand-text', '--ui-brand-contrast',
  '--ui-accent', '--ui-accent-soft', '--ui-accent-text', '--ui-success', '--ui-success-soft', '--ui-warning', '--ui-warning-soft',
  '--ui-danger', '--ui-danger-soft', '--ui-info', '--ui-info-soft', '--coaching-focus', '--ui-font-body',
  '--coaching-radius-card', '--coaching-radius-control', '--coaching-text-xl', '--coaching-text-lg', '--coaching-text-md',
  '--coaching-text-body', '--coaching-text-sm', ...Array.from({ length: 8 }, (_, i) => [`--coaching-lesson-${i}`, `--coaching-lesson-text-${i}`]).flat()];
const themed = required.filter(name => !/font|radius|text-(xl|lg|md|body|sm)$/.test(name));

test('both themes define the full token set', () => {
  for (const name of required) assert.ok(light[name], `light theme lacks ${name}`);
  for (const name of themed) assert.ok(dark[name], `dark theme lacks ${name}`);
});

const textPairs = [['--ui-text', '--ui-page'], ['--ui-text', '--ui-surface'], ['--ui-text', '--ui-surface-muted'],
  ['--ui-text-muted', '--ui-page'], ['--ui-text-muted', '--ui-surface'], ['--ui-text-muted', '--ui-surface-muted'],
  ['--ui-brand-text', '--ui-surface'], ['--ui-brand-text', '--ui-brand-soft'], ['--ui-brand-text', '--ui-surface-muted'],
  ['--ui-brand-contrast', '--ui-brand'], ['--ui-brand-contrast', '--ui-brand-strong'],
  ['--ui-accent-text', '--ui-surface'], ['--ui-accent-text', '--ui-accent-soft'],
  ...['success', 'warning', 'danger', 'info'].flatMap(kind => [[`--ui-${kind}`, '--ui-surface'], [`--ui-${kind}`, `--ui-${kind}-soft`]]),
  ...Array.from({ length: 8 }, (_, i) => [`--coaching-lesson-text-${i}`, `--coaching-lesson-${i}`]),
  ...['--coaching-sidebar', '--coaching-sidebar-raised'].flatMap(bg => ['--coaching-sidebar-text', '--coaching-sidebar-muted',
    '--coaching-sidebar-active'].map(fg => [fg, bg])),
  ['--coaching-avatar-text', '--coaching-avatar'], ['--coaching-badge-text', '--coaching-badge']];
const uiPairs = [['--coaching-focus', '--ui-surface'], ['--coaching-focus', '--ui-page'], ['--ui-border-strong', '--ui-surface'],
  ['--ui-brand', '--ui-surface'], ['--ui-accent', '--ui-surface']];

for (const [name, theme] of [['light', light], ['dark', dark]]) {
  test(`${name} theme text reaches 4.5:1 and interface parts 3:1`, () => {
    const failures = [];
    for (const [fg, bg] of textPairs) { const r = ratio(hex(theme, fg), hex(theme, bg)); if (r < 4.5) failures.push(`${fg} on ${bg} ${r.toFixed(2)}`); }
    for (const [fg, bg] of uiPairs) { const r = ratio(hex(theme, fg), hex(theme, bg)); if (r < 3) failures.push(`${fg} on ${bg} ${r.toFixed(2)}`); }
    assert.deepEqual(failures, []);
  });
}

test('the palette has no indigo or violet brand', () => {
  for (const theme of [light, dark]) {
    const [r, g, b] = [1, 3, 5].map(i => parseInt(hex(theme, '--ui-brand').slice(i, i + 2), 16));
    assert.ok(!(b > r && b > g && r > g), 'brand colour must not be indigo/violet');
  }
});

// Pages and shared student components use tokens only.
const scanned = [];
const walk = dir => { for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
  const full = path.join(dir, entry.name);
  if (entry.isDirectory()) walk(full);
  else if (/\.(scss|html|ts)$/.test(entry.name) && !entry.name.endsWith('.spec.ts') && full !== tokensFile) scanned.push(full);
} };
walk(path.join(root, 'src/app/features/coaching-portal'));
scanned.push(path.join(root, 'shared/coaching-messages.component.ts'), path.join(root, 'shared/coaching-messages.component.scss'));
const banned = [[/(?<![&\w])#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{3})?(?:[0-9a-fA-F]{2})?\b(?!\s*[-\w]*=)/g, 'hex colour'],
  [/\b(?:rgba?|hsla?)\(/g, 'colour function'], [/\b[\w:-]*(?:indigo|violet|purple)-\d{2,3}\b/g, 'indigo/violet class'],
  [/(?:\b(?:color|background(?:-color)?|border(?:-color)?|fill|stroke)\s*:\s*|,\s*)(?:white|black)\b(?![-\w])/g, 'named colour']];

test('student portal files carry no hard-coded colours or indigo classes', () => {
  const found = [];
  for (const file of scanned) {
    const text = fs.readFileSync(file, 'utf8');
    for (const [pattern, label] of banned) for (const match of text.matchAll(pattern)) {
      const line = text.slice(0, match.index).split('\n').length;
      found.push(`${path.relative(root, file)}:${line} ${label} ${match[0]}`);
    }
  }
  assert.deepEqual(found, []);
});

test('the theme is applied before the app starts, from a same-origin script', () => {
  const index = fs.readFileSync(path.join(root, 'src/index.html'), 'utf8');
  const head = index.slice(0, index.indexOf('</head>'));
  assert.match(head, /<script src="theme-boot\.js"><\/script>/);
  const boot = fs.readFileSync(path.join(root, 'public/theme-boot.js'), 'utf8');
  assert.match(boot, /localStorage/); assert.match(boot, /prefers-color-scheme: dark/); assert.match(boot, /classList/); assert.match(boot, /colorScheme/);
});
