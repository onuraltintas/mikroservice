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
  }
  writeFileSync(url, JSON.stringify(catalog, null, 2) + '\n');
  const markdown = new URL(`../../${age.name}-exercises/v1/catalog.md`, import.meta.url);
  let document = readFileSync(markdown, 'utf8');
  document = document.replace(/^.*\| RegressionReduction \|.*\r?\n/gm, '');
  document = document.replace(/\n## Regresyon v2[\s\S]*$/, '');
  const rows = settings.filter(row => row.age === age.name);
  document += '\n## Regresyon v2\n\n| Egzersiz | Tür | Motor parametreleri |\n|---|---|---|\n'
    + rows.map(row => `| ${row.title} | RegressionReduction | \`${JSON.stringify(row.configuration.engineConfig)}\` |`).join('\n') + '\n';
  document = document.replace(/(toplam )\d+( kayıt)/, `$1${catalog.length}$2`);
  writeFileSync(markdown, document);
}
const json = JSON.stringify(settings, null, 2);
writeFileSync(settingsUrl, json + '\n');
const templateUrl = new URL('./apply.template.sql', import.meta.url);
if (existsSync(templateUrl)) writeFileSync(new URL('./apply.sql', import.meta.url), readFileSync(templateUrl, 'utf8').replace('{{SETTINGS}}', json));
console.log(`Generated ${settings.length} regression settings.`);
