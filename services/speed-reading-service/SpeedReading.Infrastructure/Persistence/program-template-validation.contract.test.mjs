import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('./OwnedSpeedReadingProgramAdminWriter.cs', import.meta.url), 'utf8');
assert.match(source, /OwnedSpeedReadingProgramSchedule\.BuildAsync/);
