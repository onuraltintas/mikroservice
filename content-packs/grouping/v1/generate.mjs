import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join, dirname, resolve } from 'node:path';

const directory = dirname(fileURLToPath(import.meta.url));
const root = resolve(directory, '../../..');
const ages = [
  { name: 'child', suffix: 1, sizes: [2, 3, 4, 4, 4], speeds: [100, 130, 160, 200, 230], bands: [[150,200],[195,260],[240,320],[300,400],[345,460]] },
  { name: 'teen', suffix: 2, sizes: [2, 3, 3, 4, 4], speeds: [150,180,210,240,270], bands: [[300,375],[360,450],[420,525],[480,600],[540,675]] },
  { name: 'young-adult', suffix: 4, sizes: [2, 3, 3, 4, 5], speeds: [180,210,240,270,300], bands: [[360,540],[420,630],[480,720],[540,810],[600,900]] },
  { name: 'adult', suffix: 3, sizes: [2, 3, 3, 4, 5], speeds: [200,230,260,290,320], bands: [[400,600],[460,690],[520,780],[580,870],[640,960]] },
];
const sources = [['library', 'Mahalle kütüphanesinden notlar'], ['garden', 'Bahçede birlikte öğrenmek'], ['map', 'Bir haritanın gelişimi']];
const settings = [], texts = [], programs = [];
const words = text => text.trim().split(/\s+/u).length;
for (const age of ages) {
  const catalog = JSON.parse(readFileSync(join(root, `content-packs/${age.name}-exercises/v1/catalog.json`), 'utf8'));
  const ageGroupId = `10000000-0000-0000-0000-00000000000${age.suffix}`;
  for (let level = 1; level <= 5; level++) {
    const previous = catalog.find(exercise => exercise.type === 'Chunking' && exercise.difficultyLevel === level);
    if (level < 5 && !previous) throw new Error(`Missing known catalog entry ${age.name}/${level}`);
    const id = previous?.id ?? `d7b10000-0000-5000-8000-${String(age.suffix).padStart(12, '0')}`;
    const [minWordCount, maxWordCount] = age.bands[level - 1];
    settings.push({ id, age: age.name, ageGroupId, level, previousTitle: previous?.title ?? null,
      previousConfiguration: previous?.configuration ?? null,
      title: `Kelime Gruplarıyla Okuma — Seviye ${level}`,
      configuration: { engineType: 'word_highlight', difficultyLevel: level, engineConfig: {
        mode: 'chunking', readingPurpose: 'practice',
        content: { source: 'reading_text', minWordCount, maxWordCount },
        pacer: { chunkSize: age.sizes[level - 1], speedWpm: age.speeds[level - 1] } } } });
    for (const [source, title] of sources) {
      const paragraphs = readFileSync(join(directory, `texts/${source}.txt`), 'utf8').trim().split(/\r?\n\s*\r?\n/u);
      const selections = paragraphs.map((_, index) => paragraphs.slice(0, index + 1).join('\n\n'))
        .filter(text => words(text) >= minWordCount && words(text) <= maxWordCount);
      if (!selections.length) throw new Error(`No complete-paragraph selection for ${age.name}/${level}/${source}`);
      const target = (minWordCount + maxWordCount) / 2;
      selections.sort((a, b) => Math.abs(words(a) - target) - Math.abs(words(b) - target));
      const content = selections[0];
      texts.push({ id: `d7b20000-0000-5000-8000-${String(texts.length + 1).padStart(12, '0')}`,
        exerciseId: id, ageGroupId, level, title: `${title} — ${age.name} ${level} seçkisi`,
        content, wordCount: words(content), source: `texts/${source}.txt` });
    }
  }
  const plan = JSON.parse(readFileSync(join(root, `infrastructure/data/${age.name}-program-plan-v2.json`), 'utf8'));
  const days = plan.days ?? plan.programs.find(program => program.level === 5).days;
  const previous = { placementLevel: 5 }, desired = { placementLevel: 5 };
  days.forEach((day, index) => {
    const week = `week${Math.floor(index / 7) + 1}`, name = `day${index % 7 + 1}`;
    previous[week] ??= {}; desired[week] ??= {};
    previous[week][name] = day.map(type => ({ type, count: 1, difficulty: type === 'Chunking' ? 4 : 5 }));
    desired[week][name] = day.map(type => ({ type, count: 1, difficulty: 5 }));
  });
  programs.push({ id: `e2c0100${age.suffix}-8d9a-4e6c-a428-000000000005`, ageGroupId,
    previousPattern: previous, pattern: desired });
}
const json = data => JSON.stringify(data, null, 2) + '\n';
const payloads = { SETTINGS: json(settings), CONTENT: json(texts), PROGRAMS: json(programs) };
for (const [name, data] of Object.entries(payloads)) writeFileSync(join(directory, `${name.toLowerCase() === 'settings' ? 'settings' : name.toLowerCase() === 'content' ? 'content' : 'programs'}.json`), data);
let sql = readFileSync(join(directory, 'apply.template.sql'), 'utf8');
for (const [name, data] of Object.entries(payloads)) sql = sql.replace(`{{${name}}}`, data.trim());
writeFileSync(join(directory, 'apply.sql'), sql);
console.log(`Generated ${settings.length} settings, ${texts.length} original-text selections, ${programs.length} program patches.`);
