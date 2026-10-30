import fs from 'node:fs';
import { buildPattern, describe } from './rules.mjs';

const source = JSON.parse(fs.readFileSync(new URL('../v5/programs.json', import.meta.url), 'utf8'));
const programs = source.map(program => {
  const { previousPattern: _, ...rest } = program;
  return {
    ...rest,
    description: describe(program.age),
    previousDescription: program.description,
    previousPattern: program.pattern,
    pattern: { placementLevel: program.pattern.placementLevel, ...buildPattern(program.age, program.level) }
  };
});
fs.writeFileSync(new URL('./programs.json', import.meta.url), JSON.stringify(programs, null, 2) + '\n');
