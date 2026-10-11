import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const file = name => new URL(name, import.meta.url);
const profiles = {
  child: { seconds: [45,60,75,90,105], cycles: [4000,3800,3600,3400,3200] },
  teen: { seconds: [60,75,90,105,120], cycles: [3600,3400,3200,3000,2800] },
  'young-adult': { seconds: [75,90,105,120,135], cycles: [3400,3200,3000,2800,2600] },
  adult: { seconds: [90,105,120,135,150], cycles: [3200,3000,2800,2600,2400] }
};

export function buildTrackingPack() {
  const previous = existsSync(file('settings.json')) ? JSON.parse(readFileSync(file('settings.json'), 'utf8')) : [];
  const settings = [];
  for (const [age, profile] of Object.entries(profiles)) {
    const catalog = JSON.parse(readFileSync(file(`../../${age}-exercises/v1/catalog.json`), 'utf8'));
    const rows = catalog.filter(row => row.type === 'EyeTracking').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
    if (rows.length !== 5 || rows.some((row,index) => row.difficultyLevel !== index+1))
      throw new Error(`Incomplete EyeTracking catalog: ${age}`);
    for (const [index,row] of rows.entries()) {
      const baseline = previous.find(item => item.id === row.id)?.previousConfiguration ?? row.configuration;
      const configuration = structuredClone(baseline);
      if (configuration.engineType !== 'motion_path' || configuration.engineConfig.mode !== 'tracking')
        throw new Error(`Wrong EyeTracking engine: ${age}/${index+1}`);
      configuration.engineConfig.timing.durationSeconds = profile.seconds[index];
      configuration.engineConfig.timing.speedMs = profile.cycles[index];
      configuration.engineConfig.path.type = ['horizontal','circle','circle','infinity8','infinity8'][index];
      settings.push({ id: row.id, ageGroupId: row.targetAgeGroupId, age, level: row.difficultyLevel,
        previousConfiguration: baseline, configuration });
    }
  }
  return settings;
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const settings = JSON.stringify(buildTrackingPack(), null, 2);
  writeFileSync(file('settings.json'), settings + '\n');
  for (const name of ['apply','rollback'])
    writeFileSync(file(`${name}.sql`), readFileSync(file(`${name}.template.sql`), 'utf8').replace('{{SETTINGS}}', settings));
  console.log('Generated 20 EyeTracking pilot settings; no history or age catalog changes.');
}
