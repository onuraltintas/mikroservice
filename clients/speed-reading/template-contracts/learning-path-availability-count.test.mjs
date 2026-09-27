import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const backend = readFileSync(new URL('../../../services/speed-reading-service/SpeedReading.Application/Content/SpeedReadingContentContracts.cs', import.meta.url), 'utf8');
const model = readFileSync(new URL('../src/app/core/models/learning-path.model.ts', import.meta.url), 'utf8');
const widget = readFileSync(new URL('../src/app/features/student/dashboard/widgets/assignments-widget.component.html', import.meta.url), 'utf8');
const page = readFileSync(new URL('../src/app/features/student/learning-path/learning-path-page.component.html', import.meta.url), 'utf8');

test('learning-path completed-day field matches the API response in both views', () => {
  assert.match(backend, /record PersonalizedPathAvailability\([\s\S]*?int CompletedProgramDays/);
  assert.match(model, /interface PersonalizedPathAvailabilityDto\s*\{[^}]*completedProgramDays: number/s);
  assert.match(widget, /availability\?\.completedProgramDays/);
  assert.match(page, /availability\?\.completedProgramDays/);
});
