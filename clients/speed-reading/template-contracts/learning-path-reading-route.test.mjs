import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const routes = readFileSync(new URL('../src/app/features/student/student.routes.ts', import.meta.url), 'utf8');
const model = readFileSync(new URL('../src/app/core/models/learning-path.model.ts', import.meta.url), 'utf8');

assert.match(routes, /path: 'reading\/activity\/:textId'/);
assert.match(model, /\['\/student\/reading\/activity', contentId\]/);
