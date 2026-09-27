import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const template = readFileSync(new URL('./assessment.component.html', import.meta.url), 'utf8');
const styles = readFileSync(new URL('./assessment.component.scss', import.meta.url), 'utf8');
const model = readFileSync(new URL('../../../models/student-panel.model.ts', import.meta.url), 'utf8');

assert.match(template, /Okuma hızınız/);
assert.match(template, /result\(\)\?\.averageWPM/);
assert.match(template, /Hedef hız/);
assert.match(template, /result\(\)\?\.targetWPM/);
assert.match(template, /role="progressbar"/);
assert.match(template, /aria-valuenow/);
assert.match(template, /Sonraki adım/);
assert.match(styles, /prefers-reduced-motion/);
assert.match(model, /averageWPM: number;/);
assert.match(model, /targetWPM\?: number \| null;/);
