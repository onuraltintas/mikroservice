import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const ages = [
  { name: 'child', suffix: 1, speeds: [null,null,null,160,180], seconds: [null,null,null,60,75] },
  { name: 'teen', suffix: 2, speeds: [null,null,210,240,270], seconds: [null,null,120,120,120] },
  { name: 'young-adult', suffix: 4, speeds: [null,210,240,270,300], seconds: [null,120,120,120,120] },
  { name: 'adult', suffix: 3, speeds: [null,230,260,290,320], seconds: [null,120,120,120,120] }
];
const settingsUrl = new URL('./settings.json', import.meta.url);
const previous = existsSync(settingsUrl) ? JSON.parse(readFileSync(settingsUrl)) : [];
const questions = JSON.parse(readFileSync(new URL('../../regression/v2/questions.json', import.meta.url)));
const priorPrograms = JSON.parse(readFileSync(new URL('../../regression/v2/programs.json', import.meta.url)));
const sources = [['library','Kütüphanede bir keşif'],['garden','Bahçedeki gözlemler'],['map','Bir haritanın gelişimi']];
const settings = [], content = [], programs = [], outputs = [];
for (const age of ages) {
  const catalogUrl = new URL(`../../${age.name}-exercises/v1/catalog.json`, import.meta.url);
  const catalog = JSON.parse(readFileSync(catalogUrl));
  for (let level = 1; level <= 5; level++) {
    if (!age.speeds[level-1]) continue;
    const old = catalog.find(row => row.type === 'SubvocalizationReduction' && row.difficultyLevel === level);
    const id = old?.id ?? `d9f20000-0000-5000-8000-${String(age.suffix*10+level).padStart(12,'0')}`;
    const words = age.speeds[level-1]*age.seconds[level-1]/60;
    const configuration = { engineType: 'subvocalization_reduction', difficultyLevel: level, engineConfig: {
      targetWpm: age.speeds[level-1], msPerWord: Math.round(60000/age.speeds[level-1]),
      chunkSize: level === 5 ? 3 : 2, displayMode: 'highlight',
      metronomeEnabled: true, visualMetronome: true, metronomeBpm: 60, readingPurpose: 'evaluation',
      content: { source: 'reading_text', questionCount: 5, minWordCount: Math.floor(words*0.9), maxWordCount: Math.ceil(words*1.1) }
    } };
    const row = { id, type: 'SubvocalizationReduction', title: `İç Ses Azaltma — Seviye ${level}`,
      description: 'Okurken hafif sesle bir–iki–üç–dört sayın; kelime gruplarının anlamını takip edin. Sonunda beş anlama sorusu. İç ses azalması ölçülmez.',
      difficultyLevel: level, targetAgeGroupId: `10000000-0000-0000-0000-00000000000${age.suffix}`, configuration };
    const baseline = previous.find(item => item.id === id);
    settings.push({ id, age: age.name, ageGroupId: row.targetAgeGroupId, level, seconds: age.seconds[level-1],
      previousTitle: baseline ? baseline.previousTitle : old?.title ?? null,
      previousConfiguration: baseline ? baseline.previousConfiguration : old?.configuration ?? null,
      title: row.title, configuration });
    if (old) Object.assign(old, row); else catalog.push(row);
    for (const [source,title] of sources) {
      const paragraphs = readFileSync(new URL(`../../grouping/v1/texts/${source}.txt`, import.meta.url),'utf8').trim().split(/\r?\n\s*\r?\n/u);
      const selections = paragraphs.map((_,index) => paragraphs.slice(0,index+1).join('\n\n'))
        .filter(text => text.split(/\s+/u).length >= configuration.engineConfig.content.minWordCount
          && text.split(/\s+/u).length <= configuration.engineConfig.content.maxWordCount)
        .sort((a,b) => Math.abs(a.split(/\s+/u).length-words)-Math.abs(b.split(/\s+/u).length-words));
      if (!selections.length) throw new Error(`Missing complete text ${age.name}/${level}/${source}`);
      const text = selections[0], number = content.length+1;
      if (!questions[source].every(question => text.includes(question.evidence))) throw new Error('Question evidence missing');
      content.push({ id: `d9f30000-0000-5000-8000-${String(number).padStart(12,'0')}`, exerciseId: id,
        ageGroupId: row.targetAgeGroupId, level, title: `${title} — ${age.name} ${level}`, content: text,
        wordCount: text.split(/\s+/u).length, source: `texts/${source}.txt`,
        questions: questions[source].map((question,index) => ({ ...question, type: 1, bloomLevel: index === 4 ? 2 : 1,
          id: `d9f40000-0000-5000-8000-${String(number*10+index).padStart(12,'0')}`, orderIndex: index })) });
    }
    const previousProgram = priorPrograms.find(program => program.ageGroupId === row.targetAgeGroupId && program.level === level);
    if (!previousProgram) throw new Error(`Missing regression v2 program ${age.name}/${level}`);
    const previousPattern = previousProgram.pattern, pattern = structuredClone(previousPattern);
    for (const day of [23,28]) {
      const tasks = pattern[`week${Math.floor((day-1)/7)+1}`][`day${(day-1)%7+1}`];
      const slot = tasks.findIndex(task => ['SpeedReading','FreeReading','Chunking'].includes(task.type));
      if (slot < 0) throw new Error('Reading replacement slot missing');
      tasks[slot] = { type: 'SubvocalizationReduction', count: 1, difficulty: level };
    }
    programs.push({ id: previousProgram.id, ageGroupId: row.targetAgeGroupId, level,
      basePattern: previousPattern, previousPattern, pattern });
  }
  outputs.push([catalogUrl, JSON.stringify(catalog,null,2)+'\n']);
  const markdownUrl = new URL(`../../${age.name}-exercises/v1/catalog.md`, import.meta.url);
  let document = readFileSync(markdownUrl,'utf8').replace(/^.*\| SubvocalizationReduction \|.*\r?\n/gm,'')
    .replace(/\n## İç Ses Azaltma v2[\s\S]*$/u,'');
  const distribution = [1,2,3,4,5].map(level => catalog.filter(row => row.difficultyLevel === level).length).join(' / ');
  document = document.replace(/^.*(?:toplam \d+ (?:kayıt|egzersiz)).*$/mu,
    `${age.name === 'child' ? '9–12 yaş' : age.name === 'teen' ? '13–16 yaş' : age.name === 'young-adult' ? '17–21 yaş' : '22 yaş ve üzeri'}; ${new Set(catalog.map(row => row.type)).size} tür; toplam ${catalog.length} egzersiz. Seviye dağılımı: ${distribution}.`)
    .replace(/RSVP ve TextFading 3–5, RegressionReduction ve SubvocalizationReduction 4–5; Tachistoscope ve Visualization 1–5(?: seviyelerde)?\./u,
      `RSVP ve TextFading 3–5; RegressionReduction 1–5; SubvocalizationReduction ${age.name === 'teen' ? '3–5' : '2–5'}; Tachistoscope ve Visualization 1–5 seviyelerde.`)
    .replace('Geri dönüş çalışmasında maskeleme yok; iç ses çalışmasında metronom kapalıdır.',
      'Regresyon v2 geçmiş kelimeleri maskeler. İç Ses Azaltma v2 metni görünür tutar ve bağımsız 60 BPM sayı sayma ritmi kullanır.');
  document += '\n## İç Ses Azaltma v2\n\nHer okuma oturumunda hafif sesli 1–2–3–4 sayma ve 60 BPM görsel ritim. Anlama bozulabilir; iç ses azalması ölçülmez.\n\n| Seviye | Motor parametreleri |\n|---|---|\n'
    + settings.filter(row => row.age === age.name).map(row => `| ${row.level} | \`${JSON.stringify(row.configuration.engineConfig)}\` |`).join('\n')+'\n';
  outputs.push([markdownUrl, document]);
}
outputs.push([settingsUrl,JSON.stringify(settings,null,2)+'\n'],
  [new URL('./content.json',import.meta.url),JSON.stringify(content,null,2)+'\n'],
  [new URL('./programs.json',import.meta.url),JSON.stringify(programs,null,2)+'\n']);
const template = readFileSync(new URL('./apply.template.sql',import.meta.url),'utf8');
outputs.push([new URL('./apply.sql',import.meta.url),template.replace('{{SETTINGS}}',JSON.stringify(settings,null,2))
  .replace('{{CONTENT}}',JSON.stringify(content,null,2)).replace('{{PROGRAMS}}',JSON.stringify(programs,null,2))]);
for (const [url,data] of outputs) writeFileSync(url,data);
console.log(`Generated ${settings.length} settings, ${content.length} texts and ${programs.length} program patches.`);
