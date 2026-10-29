import fs from 'node:fs';
import { reviseDay } from './rules.mjs';

const source = JSON.parse(fs.readFileSync(new URL('../v4/programs.json', import.meta.url), 'utf8'));
const programs = source.map(program => {
  const pattern = structuredClone(program.pattern);
  for (let day = 1; day <= 28; day++) {
    const weekKey = `week${Math.floor((day - 1) / 7) + 1}`;
    const dayKey = `day${(day - 1) % 7 + 1}`;
    pattern[weekKey][dayKey] = reviseDay(pattern[weekKey][dayKey]);
  }
  const { previousPattern: _, ...rest } = program;
  return { ...rest, previousPattern: program.pattern, pattern };
});
fs.writeFileSync(new URL('./programs.json', import.meta.url), JSON.stringify(programs, null, 2) + '\n');
