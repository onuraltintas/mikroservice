import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const files = [
  'CoachingErasureAssessmentConsumerTests.cs',
  'CoachingErasureExecutionConsumerTests.cs',
  'NotificationErasureAssessmentConsumerTests.cs',
  'NotificationErasureExecutionConsumerTests.cs',
  'SpeedReadingErasureAssessmentConsumerTests.cs',
  'SpeedReadingErasureExecutionConsumerTests.cs',
  'CoachingNotificationConsumerTests.cs',
];

for (const file of files) {
  test(`${file} allows loaded CI runners enough time to observe messages`, async () => {
    const source = await readFile(new URL(file, import.meta.url), 'utf8');
    const registrations = source.match(/AddMassTransitTestHarness\(configurator =>/g) ?? [];
    const timeoutConfigurations = source.match(/configurator\.SetTestTimeouts\(/g) ?? [];

    assert.ok(registrations.length > 0);
    assert.equal(timeoutConfigurations.length, registrations.length);
  });
}
