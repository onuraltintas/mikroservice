import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

// Roadmap O1: student screens on the phone. One way of addressing the student ("sen"), one word for the
// person who coaches ("öğretmen"; the service stays "koçluk"), one name and one entry for the plan wizard,
// the camera opens directly for photo hand-ins and messages are shown in one place.

const root = fileURLToPath(new URL('../../', import.meta.url));
const portal = 'clients/admin-panel/src/app/features/coaching-portal';

async function files(dir, extensions) {
  const found = [];
  for (const entry of await readdir(join(root, dir), { withFileTypes: true }).catch(() => [])) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) found.push(...await files(path, extensions));
    else if (extensions.some(ext => entry.name.endsWith(ext)) && !entry.name.includes('.spec.')) found.push(path);
  }
  return found;
}
const read = path => readFile(join(root, path), 'utf8');
const lineOf = (text, index) => text.slice(0, index).split('\n').length;

async function findAll(pattern) {
  const hits = [];
  for (const path of await files(portal, ['.html', '.ts'])) {
    const text = await read(path);
    for (const match of text.matchAll(pattern)) hits.push(`${path}:${lineOf(text, match.index)} ${match[0]}`);
  }
  return hits;
}

const formalVerbs = ['dene', 'ed', 'yap', 'seç', 'kayded', 'aç', 'kapat', 'onayla', 'yükle', 'tamamla', 'kullan', 'geç', 'çık',
  'gir', 'bekle', 'yenile', 'ekle', 'gid', 'bak', 'yaz', 'belirle', 'oluştur', 'gönder', 'düzenle', 'sil', 'başla', 'dön',
  'tıkla', 'güncelle', 'indir', 'ilet', 'kontrol ed', 'vazgeç', 'bırak', 'dikkat ed', 'iste', 'göz at', 'gözden geçir', 'belirt', 'ara', 'tanımla', 'incele'];

test('Student screens address the student as "sen"', async () => {
  const imperative = new RegExp(`(?<!\\p{L})(?:${formalVerbs.join('|')})(?:in|ın|un|ün|yin|yın|yun|yün)(?!\\p{L})`, 'gu');
  const plural = /(?<!\p{L})\p{L}*(?:[ıiuü]n[ıiuü]z|s[ıiuü]n[ıiuü]z)(?:[ıiuüae]|da|de|dan|den|la|le)?(?!\p{L})/gu;
  const hits = [...await findAll(imperative), ...await findAll(plural)];
  assert.deepEqual(hits, [], `formal "siz" forms remain:\n${hits.join('\n')}`);
});

test('The person who coaches is called "öğretmen" everywhere on student screens', async () => {
  const hits = await findAll(/(?<!\p{L})[Kk]oç(?:un|una|unla|unun|a|lar|ların|um|umu|u)?(?!\p{L})/gu);
  assert.deepEqual(hits, [], `use "öğretmen" for the person:\n${hits.join('\n')}`);
});

test('The plan wizard has one name and one entry point', async () => {
  const oldNames = await findAll(/Otomatik plan|Çalışma sihirbazı|ÇALIŞMA SİHİRBAZI|Sihirbazla plan|Sihirbazla planla|Plan asistanı/g);
  assert.deepEqual(oldNames, [], `call it "Plan sihirbazı":\n${oldNames.join('\n')}`);
  const oldEntry = (await findAll(/(?<![\w-])study-plan-assistant['"]/g)).filter(hit => !hit.includes('coaching-portal.routes.ts'));
  assert.deepEqual(oldEntry, [], `link to /coaching-portal/plan/wizard:\n${oldEntry.join('\n')}`);
  const routes = await read(`${portal}/coaching-portal.routes.ts`);
  // T2: the wizard is a tab under Planım; both old addresses lead there.
  assert.match(routes, /\['study-plan-assistant',\s*'plan\/wizard'\]/, 'the old address redirects to the single entry');
  assert.match(routes, /\['automatic-plan',\s*'plan\/wizard'\]/, 'the previous entry redirects to the wizard tab');
  const setup = await read(`${portal}/pages/student-learning-setup.component.html`);
  assert.doesNotMatch(setup, /<app-student-automatic-plan/, 'the setup page links to the wizard instead of hosting a second one');
  const wizard = await read(`${portal}/pages/student-study-plan-assistant.component.html`);
  // Paket H4 (K3): "Konulardan plan" is gone; the summary offers "Şu konuları öne al" instead.
  assert.doesNotMatch(wizard, /<app-student-automatic-plan|Konulardan plan/, 'no second, topic-based plan in the wizard');
  const summary = await read(`${portal}/pages/wizard-summary-step.component.html`);
  assert.match(summary, /Şu konuları öne al/, 'the summary moves chosen topics forward');
  // The old preview address leads to the wizard's Özet step.
  assert.match(routes, /path: 'preview'[\s\S]{0,200}adim: 'ozet'/, 'plan/preview redirects to the summary step');
});

// E6 #29 (live test 06.11): the study times are asked in Çalışma tercihlerim only; the wizard and the setup preview reuse them.
test('Availability is asked in one place and the setup page previews the plan', async () => {
  const wizard = await read(`${portal}/pages/student-study-plan-assistant.component.html`);
  assert.doesNotMatch(wizard, /Yapay zekâ servisine|kurallı olarak değerlendirilir|privacy-note/, 'no technical note in the wizard');
  // Paket H4: the wizard's questions are the routine (H3), which writes the same free hours as Çalışma tercihlerim.
  const hours = await read(`${portal}/pages/wizard-hours-step.component.ts`);
  assert.match(hours, /routines\.save\(/, 'the wizard answers are saved as the routine, which writes the free hours');
  const wizardCode = await read(`${portal}/pages/wizard-summary-step.component.ts`);
  const setup = await read(`${portal}/pages/student-learning-setup.component.html`);
  assert.match(setup, /<app-student-plan-preview/, 'Plan önizlemesi shows the draft itself');
  const preview = await read(`${portal}/pages/student-plan-preview.component.ts`);
  // Paket H2: the wizard and the preview use the same need-based proposal from the server.
  assert.match(preview, /weekly\.proposal\(/, 'the preview uses the wizard\'s proposal');
  assert.match(wizardCode, /weekly\.proposal\(/, 'the wizard proposes on the server');
});

test('Student links stay inside the app instead of reloading the page', async () => {
  const hits = await findAll(/\shref="\/coaching-portal[^"]*"/g);
  assert.deepEqual(hits, [], `use routerLink:\n${hits.join('\n')}`);
});

test('Photo hand-ins open the camera directly', async () => {
  const detail = await read(`${portal}/pages/student-assignment-detail.component.html`);
  assert.match(detail, /<input[^>]*type="file"[^>]*capture="environment"/s);
  assert.match(detail, /Fotoğraf çek/);
});

test('Messages are shown once, next to the form, not again as a pop-up', async () => {
  const feedback = await read(`${portal}/coaching-feedback.ts`);
  const helper = feedback.slice(feedback.indexOf('export function coachingFeedback'));
  assert.doesNotMatch(helper, /toaster|ToasterService/i);
});
