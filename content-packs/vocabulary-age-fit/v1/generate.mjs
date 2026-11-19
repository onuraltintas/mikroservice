// Renders apply.sql from apply.template.sql and words.json: node content-packs/vocabulary-age-fit/v1/generate.mjs
import { readFileSync, writeFileSync } from 'node:fs';

export const BASELINE = { level4: 271, level5: 105 }; // shared level 4-5 words on 19.11.2026 (admin pool, read only)

export function render() {
  const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
  const { words } = JSON.parse(readFileSync(new URL('./words.json', import.meta.url), 'utf8'));
  const payload = JSON.stringify(words.map(({ id, word, definition, category, level, ageGroupId }) =>
    ({ id, word, definition, category, level, ageGroupId })));
  if (payload.includes('$words$')) throw new Error('Payload must not contain the dollar-quote tag.');
  return template
    .replace('{{WORDS}}', payload)
    .replace('{{WORD_COUNT}}', String(words.length))
    .replace('{{BASELINE_LEVEL_4}}', String(BASELINE.level4))
    .replace('{{BASELINE_LEVEL_5}}', String(BASELINE.level5));
}

if (import.meta.url === `file://${process.argv[1]}`) {
  writeFileSync(new URL('./apply.sql', import.meta.url), render());
  console.log('apply.sql written');
}
