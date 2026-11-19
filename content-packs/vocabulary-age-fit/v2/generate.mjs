// Renders apply.sql from apply.template.sql and words.json: node content-packs/vocabulary-age-fit/v2/generate.mjs
import { readFileSync, writeFileSync } from 'node:fs';

// Shared level 3 words on 19.11.2026 (admin pool, read only): 323 in all, 154 in the adult-only categories,
// 39 that Çocuk keeps (geography and the keep list); the other 130 leave Çocuk.
export const BASELINE = { total: 323, adult: 154, child: 39 };

export function render() {
  const template = readFileSync(new URL('./apply.template.sql', import.meta.url), 'utf8');
  const pack = JSON.parse(readFileSync(new URL('./words.json', import.meta.url), 'utf8'));
  const words = JSON.stringify(pack.words.map(({ id, word, definition, category, level, ageGroupId }) =>
    ({ id, word, definition, category, level, ageGroupId })));
  for (const payload of [words, JSON.stringify(pack.adultOnlyCategories), JSON.stringify(pack.childKeepWords)])
    if (payload.includes('$words$') || payload.includes('$json$')) throw new Error('Payload must not contain a dollar-quote tag.');
  return template
    .replace('{{WORDS}}', words)
    .replace('{{ADULT_CATEGORIES}}', JSON.stringify(pack.adultOnlyCategories))
    .replace('{{CHILD_KEEP_WORDS}}', JSON.stringify(pack.childKeepWords))
    .replace('{{CHILD_KEEP_CATEGORIES}}', JSON.stringify(pack.childKeepCategories))
    .replace('{{WORD_COUNT}}', String(pack.words.length))
    .replace('{{BASELINE_TOTAL}}', String(BASELINE.total))
    .replace('{{BASELINE_ADULT}}', String(BASELINE.adult))
    .replace('{{BASELINE_CHILD}}', String(BASELINE.child));
}

if (import.meta.url === `file://${process.argv[1]}`) {
  writeFileSync(new URL('./apply.sql', import.meta.url), render());
  console.log('apply.sql written');
}
