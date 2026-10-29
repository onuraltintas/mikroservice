import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';

// Hedef Avı v1 (decision 29.10.2026): replaces N-back in programs. Product settings, not validated norms.
// Each cell: [target, distractors, rows, columns, targetCount, timeLimitSeconds]
const ages = {
  '10000000-0000-0000-0000-000000000001': [ // Çocuk 9–12
    ['b', ['a', 'e', 'o'], 5, 8, 10, 60],
    ['b', ['d', 'o', 'e'], 6, 8, 12, 70],
    ['b', ['d', 'p'], 6, 10, 15, 80],
    ['b', ['d', 'p', 'q'], 7, 10, 17, 90],
    ['ve', ['ev', 'ne', 'ye'], 7, 10, 17, 90]
  ],
  '10000000-0000-0000-0000-000000000002': [ // Genç 13–16
    ['b', ['d', 'o', 'e'], 6, 10, 15, 75],
    ['b', ['d', 'p'], 7, 10, 17, 85],
    ['b', ['d', 'p', 'q'], 8, 10, 20, 95],
    ['ve', ['ev', 'ne', 'ye'], 8, 10, 20, 90],
    ['kar', ['kra', 'rak', 'kır'], 8, 10, 20, 90]
  ],
  '10000000-0000-0000-0000-000000000004': [ // Genç yetişkin 17–21
    ['b', ['d', 'p'], 7, 10, 17, 75],
    ['b', ['d', 'p', 'q'], 8, 10, 20, 85],
    ['ve', ['ev', 'ne', 'ye'], 8, 10, 20, 80],
    ['kar', ['kra', 'rak', 'kır'], 9, 10, 22, 90],
    ['bir', ['dir', 'bil', 'rib'], 10, 10, 25, 95]
  ],
  '10000000-0000-0000-0000-000000000003': [ // Yetişkin 22+
    ['b', ['d', 'p'], 7, 10, 17, 75],
    ['b', ['d', 'p', 'q'], 8, 10, 20, 85],
    ['ve', ['ev', 'ne', 'ye'], 8, 10, 20, 80],
    ['kar', ['kra', 'rak', 'kır'], 9, 10, 22, 90],
    ['bir', ['dir', 'bil', 'rib'], 10, 10, 25, 95]
  ]
};

// Stable name-based UUIDs (version 5 layout) so re-generation keeps the same ids.
const uuid = name => {
  const bytes = createHash('sha1').update(`target-hunt-v1:${name}`).digest().subarray(0, 16);
  bytes[6] = (bytes[6] & 0x0f) | 0x50;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = bytes.toString('hex');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
};
const quoted = values => values.map(value => `"${value}"`);
const list = values => values.length === 1 ? values[0] : `${values.slice(0, -1).join(', ')} ve ${values.at(-1)}`;

const exercises = Object.entries(ages).flatMap(([ageGroupId, levels]) =>
  levels.map(([target, distractors, rows, columns, targetCount, timeLimit], index) => {
    const level = index + 1;
    const kind = target.length === 1 ? 'harfi' : 'kelimesi';
    return {
      id: uuid(`${ageGroupId}:${level}`),
      ageGroupId,
      level,
      title: `Hedef Avı: "${target}" ${kind} (Seviye ${level})`,
      description: `Satırlardaki her "${target}" ${kind}ne dokun; ${list(quoted(distractors))} gibi benzerlerine dokunma.`,
      configuration: {
        engineType: 'target_hunt',
        engineConfig: { target, distractors, rows, columns, targetCount, timeLimit },
        difficultyLevel: level
      }
    };
  }));

const settings = {
  type: {
    id: uuid('type'),
    name: 'TargetHunt',
    displayName: 'Hedef Avı',
    description: 'Satırlardaki hedef harf veya kelimeyi süre bitmeden bulma; benzer görünenlere dokunmama.',
    engineType: 'target_hunt',
    iconName: 'ads_click',
    colorCode: '#f57c00',
    category: 'attention-and-eye-movement'
  },
  exercises
};

const json = JSON.stringify(settings, null, 2);
writeFileSync(new URL('./settings.json', import.meta.url), json + '\n');
const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
writeFileSync(new URL('./apply.sql', import.meta.url), template.replace('{{SETTINGS}}', json));
console.log(`Generated ${exercises.length} target hunt exercises.`);
