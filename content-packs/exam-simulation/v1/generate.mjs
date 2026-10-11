import { readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const file = name => new URL(name, import.meta.url);
export function buildExamPack() {
  return [
    ['teen', 540, 650, 180000, 48],
    ['young-adult', 430, 650, 180000, 60],
    ['adult', 480, 700, 240000, 60]
  ].map(([age, minWordCount, maxWordCount, maxReadingTimeMs, questionTimeSeconds]) => {
    const catalog = JSON.parse(readFileSync(file(`../../${age}-exercises/v1/catalog.json`), 'utf8'));
    const rows = catalog.filter(row => row.type === 'ExamSimulation');
    if (rows.length !== 1 || rows[0].difficultyLevel !== 5) throw new Error(`Unexpected exam catalog: ${age}`);
    const row = rows[0];
    return { id: row.id, ageGroupId: row.targetAgeGroupId, age, level: 5,
      previousConfiguration: row.configuration,
      configuration: { ...row.configuration, engineConfig: { ...row.configuration.engineConfig,
        readingPurpose: 'evaluation',
        content: { source: 'reading_text', minWordCount, maxWordCount, questionCount: 8 },
        timing: { minReadingTimeMs: 10000, maxReadingTimeMs, questionTimeSeconds },
        display: { fontSize: 'medium', lineHeight: 1.8 }
      } }
    };
  });
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const settings = JSON.stringify(buildExamPack(), null, 2);
  writeFileSync(file('settings.json'), settings + '\n');
  for (const name of ['apply', 'rollback']) {
    const target = name === 'apply' ? 'config' : 'old_config';
    writeFileSync(file(`${name}.sql`), readFileSync(file('settings.template.sql'), 'utf8')
      .replace('{{SETTINGS}}', settings).replaceAll('{{TARGET}}', target));
  }
  console.log('Generated three exam profiles. No text, program or history changes.');
}
