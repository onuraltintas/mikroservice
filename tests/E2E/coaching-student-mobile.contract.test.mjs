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
  assert.deepEqual(oldEntry, [], `link to /coaching-portal/automatic-plan:\n${oldEntry.join('\n')}`);
  const routes = await read(`${portal}/coaching-portal.routes.ts`);
  assert.match(routes, /path:\s*'study-plan-assistant',\s*redirectTo:\s*'automatic-plan'/, 'the old address redirects to the single entry');
  const setup = await read(`${portal}/pages/student-learning-setup.component.html`);
  assert.doesNotMatch(setup, /<app-student-automatic-plan/, 'the setup page links to the wizard instead of hosting a second one');
  const wizard = await read(`${portal}/pages/student-study-plan-assistant.component.html`);
  assert.match(wizard, /<app-student-automatic-plan/, 'the topic-based plan lives inside the wizard');
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
