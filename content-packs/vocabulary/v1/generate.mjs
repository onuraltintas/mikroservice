import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const counts = { child: [8,10,12,14,16], teen: [10,12,14,16,18], 'young-adult': [12,14,16,18,20], adult: [12,15,18,20,24] };
const settingsUrl = new URL('./settings.json', import.meta.url);
const previous = existsSync(settingsUrl) ? JSON.parse(readFileSync(settingsUrl, 'utf8')) : [];
const patches = [];
const newChildId = 'd281c883-b608-57bd-8d31-bfe196936d7c';
for (const [age, values] of Object.entries(counts)) {
  const url = new URL(`../../${age}-exercises/v1/catalog.json`, import.meta.url);
  const catalog = JSON.parse(readFileSync(url, 'utf8'));
  if (age === 'child' && !catalog.some(row => row.id === newChildId)) {
    const learning = catalog.find(row => row.type === 'Vocabulary' && row.difficultyLevel === 3);
    if (!learning) throw new Error('Missing child level 3 learning exercise');
    const quiz = structuredClone(learning);
    quiz.id = newChildId; quiz.title = 'Kelime Bağlantılarını Sına';
    quiz.configuration.engineConfig.mode = 'quiz';
    catalog.splice(catalog.indexOf(learning) + 1, 0, quiz);
  }
  const rows = catalog.filter(row => row.type === 'Vocabulary');
  if (rows.length !== (age === 'child' ? 8 : 7)) throw new Error(`${age}: unexpected vocabulary catalogue`);
  for (const row of rows) {
    const baseline = previous.find(item => item.id === row.id)?.previousConfiguration
      ?? (row.id === newChildId ? null : structuredClone(row.configuration));
    const config = row.configuration.engineConfig;
    config.vocabulary.count = values[row.difficultyLevel - 1];
    config.vocabulary.requireFullPool = true;
    config.timeLimitPerWord = 0;
    if (config.mode === 'learning') config.learnThenQuiz = true;
    patches.push({ id: row.id, ageGroupId: row.targetAgeGroupId, level: row.difficultyLevel,
      title: row.title, description: row.description, previousConfiguration: baseline, configuration: row.configuration });
  }
  writeFileSync(url, JSON.stringify(catalog, null, 2) + '\n');
  const markdownUrl = new URL(`../../${age}-exercises/v1/catalog.md`, import.meta.url);
  let markdown = readFileSync(markdownUrl, 'utf8');
  for (const row of rows) {
    if (row.id === newChildId && !markdown.includes(`| ${row.title} | Vocabulary |`)) {
      const anchor = rows.find(item => item.difficultyLevel === 3 && item.id !== newChildId);
      const line = markdown.split('\n').find(text => text.startsWith(`| ${anchor.title} | Vocabulary |`));
      if (!line) throw new Error('Missing level 3 markdown anchor');
      markdown = markdown.replace(line, line + `\n| ${row.title} | Vocabulary | \`${JSON.stringify(row.configuration.engineConfig)}\` |`);
    }
    markdown = markdown.split('\n').map(line => line.startsWith(`| ${row.title} | Vocabulary |`)
      ? `| ${row.title} | Vocabulary | \`${JSON.stringify(row.configuration.engineConfig)}\` |` : line).join('\n');
  }
  writeFileSync(markdownUrl, markdown);
}
const settings = JSON.stringify(patches, null, 2);
writeFileSync(settingsUrl, settings + '\n');
const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
writeFileSync(new URL('./apply.sql', import.meta.url), template.replace('{{SETTINGS}}', settings));
console.log(`Generated ${patches.length} vocabulary settings (28 updates, one new child quiz).`);
