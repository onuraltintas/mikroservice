import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { isDeepStrictEqual } from 'node:util';

function withoutTiming(configuration) {
  const copy = structuredClone(configuration);
  delete copy.engineConfig.timing.holdMs;
  delete copy.engineConfig.timing.durationSeconds;
  return copy;
}

const ages = {
  child: { interval: [900,850,800,750,700], seconds: [40,45,50,55,60] },
  teen: { interval: [850,800,750,700,650], seconds: [45,50,60,70,80] },
  'young-adult': { interval: [800,750,700,650,600], seconds: [50,60,70,80,90] },
  adult: { interval: [850,800,750,700,650], seconds: [50,60,70,80,90] }
};
const settingsUrl = new URL('./settings.json', import.meta.url);
const previous = existsSync(settingsUrl) ? JSON.parse(readFileSync(settingsUrl, 'utf8')) : [];
const patches = [];
for (const [age, values] of Object.entries(ages)) {
  const url = new URL(`../../${age}-exercises/v1/catalog.json`, import.meta.url);
  const catalog = JSON.parse(readFileSync(url, 'utf8'));
  const rows = catalog.filter(row => row.type === 'Saccade').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
  if (rows.length !== 5 || rows.some((row,index) => row.difficultyLevel !== index + 1))
    throw new Error(`${age}: incomplete Saccade catalogue`);
  for (const [index, row] of rows.entries()) {
    const old = structuredClone(row.configuration);
    row.configuration.engineConfig.timing.holdMs = values.interval[index];
    row.configuration.engineConfig.timing.durationSeconds = values.seconds[index];
    const baseline = previous.find(item => item.id === row.id)?.previousConfiguration ?? old;
    if (row.configuration.engineType !== 'motion_path' || row.configuration.engineConfig.mode !== 'saccade'
        || !isDeepStrictEqual(withoutTiming(baseline), withoutTiming(row.configuration)))
      throw new Error(`${age}: unapproved Saccade configuration change at level ${row.difficultyLevel}`);
    patches.push({ id: row.id, ageGroupId: row.targetAgeGroupId, level: row.difficultyLevel,
      previousConfiguration: baseline,
      configuration: row.configuration });
  }
  writeFileSync(url, JSON.stringify(catalog, null, 2) + '\n');
  const markdown = new URL(`../../${age}-exercises/v1/catalog.md`, import.meta.url);
  let index = 0;
  writeFileSync(markdown, readFileSync(markdown, 'utf8').replace(/^(.*\| Saccade \| )`[^`]*`(.*)$/gm,
    (_, prefix, suffix) => `${prefix}\`${JSON.stringify(rows[index++].configuration.engineConfig)}\`${suffix}`));
}
const settings = JSON.stringify(patches, null, 2);
writeFileSync(settingsUrl, settings + '\n');
const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
writeFileSync(new URL('./apply.sql', import.meta.url), template.replace('{{SETTINGS}}', settings));
console.log(`Generated ${patches.length} Saccade training updates.`);
