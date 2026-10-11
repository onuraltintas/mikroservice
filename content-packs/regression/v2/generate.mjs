import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const ages = [
  { name: 'child', suffix: 1, speeds: [null,140,160,180,200], seconds: [null,60,70,80,90] },
  { name: 'teen', suffix: 2, speeds: [160,190,220,260,290], seconds: [60,75,90,105,120] },
  { name: 'young-adult', suffix: 4, speeds: [180,210,250,290,320], seconds: [60,75,90,105,120] },
  { name: 'adult', suffix: 3, speeds: [180,220,270,310,340], seconds: [60,75,90,105,120] }
];
const settingsUrl = new URL('./settings.json', import.meta.url);
const previous = existsSync(settingsUrl) ? JSON.parse(readFileSync(settingsUrl)) : [];
const settings = [];
const texts = [], programs = [];
const questions = JSON.parse(readFileSync(new URL('./questions.json', import.meta.url)));
const sources = [['library','Kütüphanede bir keşif'],['garden','Bahçedeki gözlemler'],['map','Bir haritanın gelişimi']];
const groupingPrograms = JSON.parse(readFileSync(new URL('../../grouping/v1/programs.json', import.meta.url)));
for (const age of ages) {
  const url = new URL(`../../${age.name}-exercises/v1/catalog.json`, import.meta.url);
  const catalog = JSON.parse(readFileSync(url));
  for (let level = age.name === 'child' ? 2 : 1; level <= 5; level++) {
    const old = catalog.find(row => row.type === 'RegressionReduction' && row.difficultyLevel === level);
    const id = old?.id ?? `d8f20000-0000-5000-8000-${String(age.suffix * 10 + level).padStart(12,'0')}`;
    const targetWords = age.speeds[level-1] * age.seconds[level-1] / 60;
    const configuration = { engineType: 'regression_reduction', difficultyLevel: level, engineConfig: {
      targetWpm: age.speeds[level-1], chunkSize: 1, maskingType: 'trailing', visibleHistoryWords: [2,1,1,0,0][level-1],
      readingPurpose: 'evaluation', content: { source: 'reading_text', questionCount: 5,
        minWordCount: Math.floor(targetWords * 0.9), maxWordCount: Math.ceil(targetWords * 1.1) } } };
    const row = { id, type: 'RegressionReduction', title: `Regresyon Azaltma — Seviye ${level}`,
      description: 'Maskelenen geçmiş kelimelerle sabit tempolu metin takibi; sonunda beş anlama sorusu.',
      difficultyLevel: level, targetAgeGroupId: `10000000-0000-0000-0000-00000000000${age.suffix}`, configuration };
    const baseline = previous.find(item => item.id === id);
    settings.push({ id, age: age.name, ageGroupId: row.targetAgeGroupId, level, seconds: age.seconds[level-1],
      previousTitle: baseline ? baseline.previousTitle : old?.title ?? null,
      previousConfiguration: baseline ? baseline.previousConfiguration : old?.configuration ?? null,
      title: row.title, configuration });
    if (old) Object.assign(old, row); else catalog.push(row);
    for (const [source, title] of sources) {
      const paragraphs = readFileSync(new URL(`../../grouping/v1/texts/${source}.txt`, import.meta.url), 'utf8').trim().split(/\r?\n\s*\r?\n/u);
      const selections = paragraphs.map((_, index) => paragraphs.slice(0,index+1).join('\n\n'))
        .filter(text => text.split(/\s+/u).length >= configuration.engineConfig.content.minWordCount
          && text.split(/\s+/u).length <= configuration.engineConfig.content.maxWordCount)
        .sort((a,b) => Math.abs(a.split(/\s+/u).length-targetWords)-Math.abs(b.split(/\s+/u).length-targetWords));
      if (!selections.length) throw new Error(`Missing complete text: ${age.name}/${level}/${source}`);
      const content = selections[0], number = texts.length+1;
      if (!questions[source].every(question => content.includes(question.evidence))) throw new Error('Question evidence missing');
      texts.push({ id: `d8f30000-0000-5000-8000-${String(number).padStart(12,'0')}`, exerciseId: id,
        ageGroupId: row.targetAgeGroupId, level, title: `${title} — ${age.name} ${level}`, content,
        wordCount: content.split(/\s+/u).length, source: `texts/${source}.txt`,
        questions: questions[source].map((question,index) => ({ ...question, type: 1, bloomLevel: index === 4 ? 2 : 1,
          id: `d8f40000-0000-5000-8000-${String(number*10+index).padStart(12,'0')}`, orderIndex: index })) });
    }
    const plan = JSON.parse(readFileSync(new URL(`../../../infrastructure/data/${age.name}-program-plan-v2.json`, import.meta.url)));
    const days = plan.days ?? plan.programs.find(program => program.level === level).days;
    const programId = `e2c0100${age.suffix}-8d9a-4e6c-a428-00000000000${level}`;
    const base = { placementLevel: level };
    days.forEach((day,index) => {
      const week = `week${Math.floor(index/7)+1}`, name = `day${index%7+1}`;
      base[week] ??= {};
      base[week][name] = day.map(type => ({ type, count: 1,
        difficulty: type === 'Chunking' ? Math.min(level,4) : age.name === 'child' && type === 'FreeReading' && level === 4 ? 3 : level }));
    });
    const previousPattern = groupingPrograms.find(program => program.id === programId)?.pattern ?? base;
    const pattern = structuredClone(previousPattern);
    for (const day of [21,26]) {
      const tasks = pattern[`week${Math.floor((day-1)/7)+1}`][`day${(day-1)%7+1}`];
      const slot = tasks.findIndex(task => ['SpeedReading','FreeReading','Chunking'].includes(task.type));
      if (slot < 0) throw new Error(`Reading replacement slot missing ${age.name}/${level}/${day}`);
      tasks[slot] = { type: 'RegressionReduction', count: 1, difficulty: level };
    }
    programs.push({ id: programId, ageGroupId: row.targetAgeGroupId, level, basePattern: base, previousPattern, pattern });
  }
  writeFileSync(url, JSON.stringify(catalog, null, 2) + '\n');
  const markdown = new URL(`../../${age.name}-exercises/v1/catalog.md`, import.meta.url);
  let document = readFileSync(markdown, 'utf8');
  document = document.replace(/^.*\| RegressionReduction \|.*\r?\n/gm, '');
  document = document.replace(/\n## Regresyon v2[\s\S]*$/, '');
  const rows = settings.filter(row => row.age === age.name);
  document += '\n## Regresyon v2\n\n| Egzersiz | Tür | Motor parametreleri |\n|---|---|---|\n'
    + rows.map(row => `| ${row.title} | RegressionReduction | \`${JSON.stringify(row.configuration.engineConfig)}\` |`).join('\n') + '\n';
  const counts = Array.from({length:5},(_,i) => catalog.filter(row => row.difficultyLevel === i+1).length);
  document = document.replace(/^5 seviye,.*$/m, `5 seviye, ${new Set(catalog.map(row => row.type)).size} tür ve toplam ${catalog.length} kayıt. Seviye dağılımı: ${counts.join(' / ')}.`);
  writeFileSync(markdown, document);
}
const json = JSON.stringify(settings, null, 2);
writeFileSync(settingsUrl, json + '\n');
writeFileSync(new URL('./content.json', import.meta.url), JSON.stringify(texts,null,2)+'\n');
writeFileSync(new URL('./programs.json', import.meta.url), JSON.stringify(programs,null,2)+'\n');
const templateUrl = new URL('./apply.template.sql', import.meta.url);
if (existsSync(templateUrl)) writeFileSync(new URL('./apply.sql', import.meta.url), readFileSync(templateUrl, 'utf8')
  .replace('{{SETTINGS}}', json).replace('{{CONTENT}}',JSON.stringify(texts,null,2)).replace('{{PROGRAMS}}',JSON.stringify(programs,null,2)));
console.log(`Generated ${settings.length} regression settings.`);
