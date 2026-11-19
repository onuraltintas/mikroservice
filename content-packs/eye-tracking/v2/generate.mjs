import { readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

// Göz Takibi v2 (user decision 19.11.2026): every level runs 45 seconds for children and 60 seconds for the other ages.
// The level keeps its path (horizontal, circle, figure eight) and its cycle; only the duration changes. The baseline is
// the v1 setting that is live.
const file = name => new URL(name, import.meta.url);
export const DURATION_SECONDS = { child: 45, teen: 60, 'young-adult': 60, adult: 60 };

export function buildTrackingPack() {
  const v1 = JSON.parse(readFileSync(file('../v1/settings.json'), 'utf8'));
  if (v1.length !== 20) throw new Error('v1 must hold the 20 EyeTracking records');
  return v1.map(row => {
    const seconds = DURATION_SECONDS[row.age];
    if (!seconds) throw new Error(`Unknown age: ${row.age}`);
    const configuration = structuredClone(row.configuration);
    configuration.engineConfig.timing.durationSeconds = seconds;
    return { id: row.id, ageGroupId: row.ageGroupId, age: row.age, level: row.level, previousConfiguration: row.configuration, configuration };
  });
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const settings = JSON.stringify(buildTrackingPack(), null, 2);
  writeFileSync(file('settings.json'), settings + '\n');
  for (const name of ['apply', 'rollback'])
    writeFileSync(file(`${name}.sql`), readFileSync(file(`${name}.template.sql`), 'utf8').replace('{{SETTINGS}}', settings));
  console.log('Generated 20 EyeTracking v2 durations; only timing.durationSeconds changes.');
}
