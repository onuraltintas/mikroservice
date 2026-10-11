import { readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

const file = name => new URL(name, import.meta.url);
const read = name => JSON.parse(readFileSync(file(name), 'utf8'));
const settings = read('./settings.json');
const bank = read('./questions.json');
const sources = {
  child: [
    ['garden', '../../grouping/v1/texts/garden.txt', 'Bir bahçede ortak gözlem'],
    ['map', '../../grouping/v1/texts/map.txt', 'Yol gösteren harita']
  ],
  teen: [
    ['route', '../../guided-reading/v1/texts/teen-5-route.txt', 'Okul girişini araştırmak'],
    ['exhibition', '../../guided-reading/v1/texts/teen-5-exhibition.txt', 'Nesnelerin hikâyelerini anlatmak']
  ],
  'young-adult': [
    ['repair', './texts/repair-workshop.txt', 'Tamir atölyesinde kararlar'],
    ['archive', './texts/community-archive.txt', 'Ortak bir arşiv kurmak']
  ],
  adult: [
    ['repair', './texts/repair-workshop.txt', 'Tamir atölyesinde kararlar'],
    ['archive', './texts/community-archive.txt', 'Ortak bir arşiv kurmak']
  ]
};
const uuid = (prefix, number) => `${prefix}-0000-5000-8000-${String(number).padStart(12, '0')}`;
const words = text => text.trim().split(/\s+/u).length;
const content = [];
let questionNumber = 0;
for (const setting of settings) {
  const old = read(`../../${setting.age}-exercises/v1/catalog.json`).find(row => row.id === setting.id);
  if (!old || old.type !== 'Comprehension') throw new Error(`Unknown baseline ${setting.id}`);
  setting.previousTitle = old.title;
  for (const [key, source, title] of sources[setting.age]) {
    const original = readFileSync(file(source), 'utf8').trim().replace(/\r\n/g, '\n');
    const paragraphs = original.split(/\n\s*\n/u);
    const band = setting.configuration.engineConfig.content;
    const candidates = paragraphs.map((_, index) => paragraphs.slice(0, index + 1).join('\n\n'))
      .filter(text => words(text) >= band.minWordCount && words(text) <= band.maxWordCount);
    // At entry level, prefer the shorter complete selection; later levels grow toward the band midpoint.
    const target = setting.level === 1 ? band.minWordCount : (band.minWordCount + band.maxWordCount) / 2;
    candidates.sort((a, b) => Math.abs(words(a) - target) - Math.abs(words(b) - target));
    if (!candidates.length) throw new Error(`No paragraph-complete text: ${setting.age}/${setting.level}/${key}`);
    const text = candidates[0];
    const eligible = bank[key].filter(q => text.includes(q.evidence) && (setting.level >= 4 || q.type !== 3));
    // Higher levels use evidence later in the longer selection, not just opening facts.
    eligible.sort((a, b) => setting.level >= 3 ? text.indexOf(b.evidence) - text.indexOf(a.evidence)
      : text.indexOf(a.evidence) - text.indexOf(b.evidence));
    const selected = [];
    const take = (type, count) => {
      for (const q of eligible.filter(q => q.type === type && !selected.includes(q)).slice(0, count)) selected.push(q);
    };
    if (setting.level >= 4) take(3, 2);
    take(2, setting.level >= 3 ? 3 : setting.level === 2 ? 2 : 1);
    take(1, band.questionCount - selected.length);
    for (const q of eligible) if (selected.length < band.questionCount && !selected.includes(q)) selected.push(q);
    if (selected.length !== band.questionCount || !selected.some(q => q.type === 2)
      || (setting.level >= 4 && !selected.some(q => q.type === 3))) {
      throw new Error(`Insufficient evidenced questions: ${setting.age}/${setting.level}/${key}`);
    }
    // Preserve an editorial question order, while distributing A–D keys deterministically.
    selected.sort((a, b) => text.indexOf(a.evidence) - text.indexOf(b.evidence));
    const questions = selected.map((q, index) => {
      const answerIndex = (index + content.length) % 4;
      const options = [...q.distractors];
      options.splice(answerIndex, 0, q.answer);
      return { id: uuid('c4a20000', ++questionNumber), questionText: q.questionText,
        type: q.type, bloomLevel: q.bloomLevel, explanation: q.explanation,
        optionA: options[0], optionB: options[1], optionC: options[2], optionD: options[3],
        correctAnswer: 'ABCD'[answerIndex], evidence: q.evidence, orderIndex: index };
    });
    content.push({ id: uuid('c4a10000', content.length + 1), exerciseId: setting.id,
      ageGroupId: setting.ageGroupId, level: setting.level,
      title: `${title} — ${setting.age} S${setting.level} seçkisi`, content: text,
      wordCount: words(text), source, sourceSha256: createHash('sha256').update(original).digest('hex'), questions });
  }
}
const json = data => JSON.stringify(data, null, 2);
writeFileSync(file('./settings.json'), json(settings) + '\n');
writeFileSync(file('./content.json'), json(content) + '\n');
writeFileSync(file('./apply.sql'), readFileSync(file('./apply.template.sql'), 'utf8')
  .replace('{{SETTINGS}}', json(settings)).replace('{{CONTENT}}', json(content)));
writeFileSync(file('./rollback.sql'), readFileSync(file('./rollback.template.sql'), 'utf8')
  .replace('{{SETTINGS}}', json(settings)).replace('{{CONTENT}}', json(content)));
const ids = values => values.map(id => `'${id}'::uuid`).join(',');
const filters = {
  exercises: settings.map(row => row.id), reading_texts: content.map(row => row.id),
  reading_questions: content.flatMap(row => row.questions.map(q => q.id))
};
// Use PostgreSQL dollar quotes for the WHERE strings, avoiding nested SQL escaping.
writeFileSync(file('./inventory.sql'), `-- Generated: fingerprints of every non-target row in the Speed Reading schema.\nBEGIN READ ONLY;\nSELECT format('SELECT %L || ''|'' || count(*) || ''|'' || md5(COALESCE(string_agg(row_to_json(t)::text, ''|'' ORDER BY row_to_json(t)::text), '''')) FROM %I.%I t %s;',\n  tablename,schemaname,tablename,CASE tablename\n${Object.entries(filters).map(([table, values]) => `    WHEN '${table}' THEN $where$WHERE id NOT IN (${ids(values)})$where$`).join('\n')}\n    ELSE '' END)\nFROM pg_tables WHERE schemaname='speed_reading' ORDER BY tablename\n\\gexec\nCOMMIT;\n`);
console.log(`Generated ${settings.length} settings, ${content.length} scoped text records, ${questionNumber} questions.`);
