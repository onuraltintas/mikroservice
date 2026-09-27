import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const widget = readFileSync(new URL('../src/app/features/student/dashboard/widgets/assignments-widget.component.html', import.meta.url), 'utf8');
const component = readFileSync(new URL('../src/app/features/student/dashboard/widgets/assignments-widget.component.ts', import.meta.url), 'utf8');

assert.match(widget, /Kişisel Öğrenme Yolu/);
assert.match(component, /getPersonalizedPathAvailability/);
assert.match(component, /getPersonalizedLearningPathProgress/);
assert.doesNotMatch(component, /AssignmentService/);
assert.match(widget, /Şu an ek çalışma önerilmiyor/);
assert.match(component, /\/student\/learning-path/);
