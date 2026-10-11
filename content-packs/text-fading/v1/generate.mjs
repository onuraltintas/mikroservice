import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve, join } from 'node:path';

const directory = dirname(fileURLToPath(import.meta.url));
const root = resolve(directory, '../../..');
const ages = [
  { name: 'child', suffix: 1, speeds: [60,80,100,120,140], lag: 5000 },
  { name: 'teen', suffix: 2, speeds: [150,180,210,240,270], lag: 2500 },
  { name: 'young-adult', suffix: 4, speeds: [180,210,240,270,300], lag: 2000 },
  { name: 'adult', suffix: 3, speeds: [200,230,260,290,320], lag: 2000 }
];
const settings = [];
for (const age of ages) {
  const catalog = JSON.parse(readFileSync(join(root, `content-packs/${age.name}-exercises/v1/catalog.json`), 'utf8'));
  for (let level = 1; level <= 5; level++) {
    const previous = catalog.find(item => item.type === 'TextFading' && item.difficultyLevel === level);
    settings.push({ id: previous?.id ?? `d8f10000-0000-5000-8000-${String(age.suffix * 10 + level).padStart(12,'0')}`,
      age: age.name, ageGroupId: `10000000-0000-0000-0000-00000000000${age.suffix}`, level,
      previousTitle: previous?.title ?? null, previousConfiguration: previous?.configuration ?? null,
      title: `Kaybolan Metinle Okuma — Seviye ${level}`,
      configuration: { engineType: 'text_fade', difficultyLevel: level, engineConfig: {
        content: { source: 'reading_text', questionCount: 4 },
        fading: { speedWpm: age.speeds[level-1], lagMs: age.lag },
        timing: { timeLimitSec: 0 }, readingPurpose: 'evaluation' } } });
  }
}
const json = JSON.stringify(settings, null, 2) + '\n';
writeFileSync(join(directory, 'settings.json'), json);
writeFileSync(join(directory, 'apply.sql'), readFileSync(join(directory, 'apply.template.sql'), 'utf8').replace('{{SETTINGS}}', json.trim()));
console.log(`Generated ${settings.length} Text Fading settings; ${settings.filter(item => !item.previousConfiguration).length} new levels.`);
