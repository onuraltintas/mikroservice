import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const ages = {
  child: [[1,3000,32],[1,2800,40],[1,2600,48],[2,3000,48],[2,2800,56]],
  teen: [[1,2800,40],[1,2500,48],[2,2800,48],[2,2500,56],[3,3000,56]],
  'young-adult': [[1,2600,48],[2,2900,48],[2,2600,56],[3,3000,56],[3,2700,64]],
  adult: [[1,2700,48],[2,3000,48],[2,2700,56],[3,3100,56],[3,2800,64]]
};
const settingsUrl = new URL('./settings.json', import.meta.url);
const previous = existsSync(settingsUrl) ? JSON.parse(readFileSync(settingsUrl, 'utf8')) : [];
const patches = [];
const durationImpact = [];
for (const [age, values] of Object.entries(ages)) {
  const url = new URL(`../../${age}-exercises/v1/catalog.json`, import.meta.url);
  const catalog = JSON.parse(readFileSync(url, 'utf8'));
  const rows = catalog.filter(row => row.type === 'Focus').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
  if (rows.length !== 5 || rows.some((row,index) => row.difficultyLevel !== index + 1))
    throw new Error(`${age}: incomplete Focus catalogue`);
  for (const [index, row] of rows.entries()) {
    const baseline = previous.find(item => item.id === row.id)?.previousConfiguration ?? structuredClone(row.configuration);
    const [nLevel, speedMs, totalSteps] = values[index];
    Object.assign(row.configuration.engineConfig, { nLevel, speedMs, totalSteps });
    patches.push({ id: row.id, ageGroupId: row.targetAgeGroupId, level: row.difficultyLevel,
      previousConfiguration: baseline, configuration: row.configuration });
  }
  writeFileSync(url, JSON.stringify(catalog, null, 2) + '\n');
  const markdown = new URL(`../../${age}-exercises/v1/catalog.md`, import.meta.url);
  let index = 0;
  writeFileSync(markdown, readFileSync(markdown, 'utf8').replace(/^(.*\| Focus \| )`[^`]*`(.*)$/gm,
    (_, prefix, suffix) => `${prefix}\`${JSON.stringify(rows[index++].configuration.engineConfig)}\`${suffix}`));
  const programs = JSON.parse(readFileSync(new URL(`../../../infrastructure/data/${age}-program-plan-v2.json`, import.meta.url), 'utf8'));
  const plans = programs.programs ?? values.map((_, index) => ({ level: index + 1, days: programs.days }));
  for (const program of plans) {
    const config = rows[program.level - 1].configuration.engineConfig;
    const seconds = config.speedMs * config.totalSteps / 1000;
    durationImpact.push({ age, level: program.level, nLevel: config.nLevel, scoredDurationSeconds: seconds,
      days: program.days.map((tasks, index) => {
        const focusSlots = tasks.filter(type => type === 'Focus').length;
        return { day: index + 1, focusSlots, focusDurationSeconds: Math.round(focusSlots * seconds * 10) / 10,
          // Keep the existing five-minute estimate for other tasks. This is not
          // a guarantee of actual daily time, especially for response-based tasks.
          estimatedDailyMinutes: (tasks.length - focusSlots) * 5 + focusSlots * Math.ceil(seconds / 60) };
      }) });
  }
}
const settings = JSON.stringify(patches, null, 2);
writeFileSync(settingsUrl, settings + '\n');
const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
writeFileSync(new URL('./apply.sql', import.meta.url), template.replace('{{SETTINGS}}', settings));
writeFileSync(new URL('./duration-impact.json', import.meta.url), JSON.stringify(durationImpact, null, 2) + '\n');
console.log(`Generated ${patches.length} Focus training updates.`);
