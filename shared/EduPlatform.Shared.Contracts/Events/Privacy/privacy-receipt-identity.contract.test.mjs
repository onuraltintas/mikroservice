import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const receiptFiles = [
  '../../../../services/coaching-service/Coaching.Domain/Entities/CoachingPrivacyEntities.cs',
  '../../../../services/notification-service/Notification.Infrastructure/Persistence/NotificationErasureExecutionReceipt.cs',
  '../../../../services/speed-reading-service/SpeedReading.Infrastructure/Persistence/SpeedReadingErasureExecution.cs',
];

for (const relativePath of receiptFiles) {
  test(`${relativePath} gives an execution receipt its own event identity`, async () => {
    const source = await readFile(new URL(relativePath, import.meta.url), 'utf8');
    const completionFactory = source.split('Complete(').at(-1);

    assert.match(completionFactory, /Guid\.NewGuid\(\)/);
    assert.doesNotMatch(completionFactory, /Id\s*=\s*requestId/);
    assert.doesNotMatch(completionFactory, /new\s+CoachingErasureExecution\(requestId\)/);
  });
}
