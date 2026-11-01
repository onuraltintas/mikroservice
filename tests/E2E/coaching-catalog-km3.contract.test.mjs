import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// KM3 (01.11.2026): AGS, ÖABT (17 fields), YDS and YÖKDİL (English) catalog drafts, content-packs/coaching-catalog-km3.
// Checks the authored data against the official numbers it cites and that the generated files are current.
//   node --test tests/E2E/coaching-catalog-km3.contract.test.mjs
// With COACHING_CATALOG_SNAPSHOT_DIR set (read-only production catalog export, never committed) the YKS-equivalent
// references are checked against the existing topic ids too.
const packDir = fileURLToPath(new URL('../../content-packs/coaching-catalog-km3/', import.meta.url));
const read = name => readFileSync(packDir + name, 'utf8');
const catalog = () => JSON.parse(read('catalog.json'));
const built = () => JSON.parse(spawnSync(process.execPath, [packDir + 'build.mjs', '--json'], { encoding: 'utf8' }).stdout);

// KM1 (feat/km1-exam-types, PracticeExamRules.Profiles): the ÖABT field profile codes the lessons must reuse.
const oabtFields = {
  OABT_TUR: 'Türkçe', OABT_IMAT: 'İlköğretim Matematik', OABT_FEN: 'Fen Bilimleri', OABT_SOS: 'Sosyal Bilgiler',
  OABT_TDE: 'Türk Dili ve Edebiyatı', OABT_TAR: 'Tarih', OABT_COG: 'Coğrafya', OABT_MAT: 'Matematik', OABT_FIZ: 'Fizik',
  OABT_KIM: 'Kimya/Kimya Teknolojisi', OABT_BIO: 'Biyoloji', OABT_DIN: 'Din Kültürü ve Ahlâk Bilgisi / İmam-Hatip Lisesi Meslek Dersleri',
  OABT_REH: 'Rehberlik', OABT_SINIF: 'Sınıf Öğretmenliği', OABT_OKUL: 'Okul Öncesi', OABT_BED: 'Beden Eğitimi', OABT_OZEL: 'Özel Eğitim',
};

test('the generated SQL, review table and README totals are current', () => {
  const result = spawnSync(process.execPath, [packDir + 'build.mjs', '--check'], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr || result.stdout);
});

test('lesson codes: six AGS sections, the 17 KM1 ÖABT fields, YDS_EN and the three YÖKDİL fields', () => {
  const lessons = catalog().lessons;
  assert.deepEqual(lessons.filter(x => x.exam === 'AGS').map(x => x.code),
    ['AGS_SOZ', 'AGS_SAY', 'AGS_TAR', 'AGS_COG', 'AGS_EGT', 'AGS_MEV']);
  assert.deepEqual(lessons.filter(x => x.exam === 'OABT').map(x => x.code).sort(), Object.keys(oabtFields).sort());
  for (const lesson of lessons.filter(x => x.exam === 'OABT')) assert.equal(lesson.field, oabtFields[lesson.code], lesson.code);
  assert.deepEqual(lessons.filter(x => x.exam === 'YDS').map(x => x.code), ['YDS_EN']);
  assert.deepEqual(lessons.filter(x => x.exam === 'YOKDIL').map(x => x.code), ['YOKDIL_FEN', 'YOKDIL_SAG', 'YOKDIL_SOS']);
  assert.equal(new Set(lessons.map(x => x.code)).size, lessons.length);
});

test('AGS follows the 2026 ÖSYM distribution: section weights, topic rows and the four laws', () => {
  const ags = catalog().lessons.filter(x => x.exam === 'AGS');
  assert.deepEqual(ags.map(x => x.units[0].sectionPct), [18.75, 18.75, 7.5, 7.5, 37.5, 10]);
  const names = code => ags.find(x => x.code === code).units.flatMap(u => u.topics.map(t => t.name));
  assert.deepEqual(names('AGS_SOZ'), ['Sözcükte Anlam', 'Cümlede Anlam', 'Anlatımın Oluşması', 'Paragrafta Anlam', 'Sözel Mantık']);
  assert.deepEqual(names('AGS_SAY'), ['Temel Matematik', 'Grafik ve Tablo Yorumlama', 'Mantıksal Muhakeme Problemleri']);
  assert.deepEqual(names('AGS_TAR'), ['Osmanlı Öncesi Türk Devletleri Tarihi', 'Osmanlı Tarihi', 'Atatürk İlkeleri ve İnkılap Tarihi',
    'Çağdaş Türk ve Dünya Tarihi']);
  assert.deepEqual(names('AGS_COG'), ['Türkiye Fiziki Coğrafyası', 'Türkiye Beşerî ve Ekonomik Coğrafyası']);
  assert.equal(names('AGS_EGT').length, 11);
  assert.ok(names('AGS_EGT').includes('Türkiye Yüzyılı Maarif Modeli'));
  assert.deepEqual(names('AGS_MEV'), [
    'Türkiye Cumhuriyeti Anayasası (Kanun Numarası: 2709, Kabul Tarihi: 18/10/1982), İnsan Hakları Hukuku',
    '1739 sayılı Millî Eğitim Temel Kanunu', '222 sayılı İlköğretim ve Eğitim Kanunu', '7528 sayılı Öğretmenlik Mesleği Kanunu']);
  // 80 questions: 15 / 15 / 6 / 6 / 30 / 8.
  assert.deepEqual(ags.map(x => Math.round(x.units[0].sectionPct * 80 / 100)), [15, 15, 6, 6, 30, 8]);
});

test('every ÖABT field has its official topic weights and they add up to 100', () => {
  const rows = built().lessons.filter(x => x.exam === 'OABT');
  assert.equal(rows.length, 17);
  for (const lesson of rows) {
    const total = lesson.topics.reduce((sum, t) => sum + t.pct, 0);
    assert.ok(Math.abs(total - 100) < 0.01, `${lesson.code}: ${total}`);
    assert.ok(lesson.complete, `${lesson.code} has an official distribution`);
  }
  const ozel = rows.find(x => x.code === 'OABT_OZEL');
  assert.deepEqual(ozel.units, ['Alan Bilgisi Testi', 'Alan Eğitimi Testi']);
  assert.equal(ozel.topics.length, 19);
  const din = rows.find(x => x.code === 'OABT_DIN');
  assert.equal(din.topics.find(t => t.name === 'Fıkıh').pct, 14);
});

test('YDS and YÖKDİL question types follow the counted 2026 booklets (80 questions each)', () => {
  const lessons = built().lessons;
  const types = code => Object.fromEntries(lessons.find(x => x.code === code).units.map((u, i) => [u, lessons.find(x => x.code === code).unitQuestions[i]]));
  assert.deepEqual(types('YDS_EN'), {
    'Sözcük ve ifade bilgisi': 6, 'Dil bilgisi': 10, 'Cloze test (paragrafta boşluk doldurma)': 10, 'Cümle tamamlama': 10,
    'Çeviri': 6, 'Okuduğunu anlama': 20, 'Diyalog tamamlama': 5, 'Anlamca en yakın cümle': 4, 'Paragraf tamamlama': 4,
    'Anlam bütünlüğünü bozan cümle': 5 });
  for (const code of ['YOKDIL_FEN', 'YOKDIL_SAG', 'YOKDIL_SOS']) {
    assert.deepEqual(types(code), {
      'Cümlede boşluk: sözcük ve dil bilgisi': 20, 'Cloze test (paragrafta boşluk doldurma)': 10, 'Cümle tamamlama': 11,
      'Çeviri': 12, 'Paragraf tamamlama': 6, 'Anlam bütünlüğünü bozan cümle': 6, 'Okuduğunu anlama': 15 }, code);
    const names = lessons.find(x => x.code === code).topics.map(t => t.name);
    assert.ok(!names.some(x => /Diyalog|Anlamca en yakın/.test(x)), 'YÖKDİL has no dialogue or restatement questions');
  }
});

test('every record is passive, sourced, weighted 1–5 and plannable (1–1440 minutes)', () => {
  const data = built();
  for (const lesson of data.lessons)
    for (const topic of lesson.topics) {
      assert.match(topic.sourceUrl, /^https:\/\/dokuman\.osym\.gov\.tr\//, `${lesson.code}/${topic.name}`);
      assert.ok(topic.yieldWeight >= 1 && topic.yieldWeight <= 5, `${lesson.code}/${topic.name}`);
      assert.ok(topic.minutes >= 30 && topic.minutes <= 1440 && topic.minutes % 5 === 0, `${lesson.code}/${topic.name}: ${topic.minutes}`);
      assert.ok(topic.rawMinutes >= topic.minutes);
      assert.match(topic.minutesNote, /koçluk tahmini/);
    }
  const sql = read('apply.sql');
  assert.doesNotMatch(sql, /"IsActive"\s*=\s*true|IsApproved"?\s*=\s*true/i);
  assert.match(sql, /RAISE NOTICE 'before:/);
  assert.match(sql, /RAISE NOTICE 'after:/);
});

test('total study hours per exam are reported in the README', () => {
  const readme = read('README.md');
  for (const exam of ['AGS', 'ÖABT', 'YDS', 'YÖKDİL']) assert.match(readme, new RegExp(`\\| ${exam}`));
  assert.match(readme, /koçluk tahmini/);
});

test('YKS-equivalent references point at existing approved-catalog topics', { skip: !process.env.COACHING_CATALOG_SNAPSHOT_DIR && 'set COACHING_CATALOG_SNAPSHOT_DIR' }, () => {
  const snapshot = process.env.COACHING_CATALOG_SNAPSHOT_DIR;
  assert.ok(existsSync(`${snapshot}/study_catalog_topics.csv`));
  const ids = new Set(readFileSync(`${snapshot}/study_catalog_topics.csv`, 'utf8').split('\n').slice(1).map(x => x.split(',')[0]));
  const rows = read('yks-equivalents.csv').trim().split('\n').slice(1);
  assert.ok(rows.length >= 20);
  for (const row of rows) assert.ok(ids.has(row.split(',')[2]), row);
});
