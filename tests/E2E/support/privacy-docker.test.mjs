import assert from 'node:assert/strict';
import test from 'node:test';
import {
  requireDisposableContainer,
  createMassTransitEnvelope,
  requireDisposableEnvironment,
} from './privacy-docker.mjs';

test('privacy Docker E2E refuses an unmarked environment', () => {
  assert.throws(() => requireDisposableEnvironment({}), /E2E_DISPOSABLE_ENV=true/);
  assert.throws(() => requireDisposableEnvironment({
    E2E_DISPOSABLE_ENV: 'true', ENVIRONMENT: 'Production',
  }), /Production/);
  assert.doesNotThrow(() => requireDisposableEnvironment({
    E2E_DISPOSABLE_ENV: 'true', ENVIRONMENT: 'Development',
  }));
});

test('privacy Docker E2E verifies the actual Identity container environment', () => {
  assert.throws(() => requireDisposableContainer(['ASPNETCORE_ENVIRONMENT=Production']), /Production/);
  assert.throws(() => requireDisposableContainer(['ASPNETCORE_ENVIRONMENT=Development'], 'Staging'), /mismatch/);
  assert.doesNotThrow(() => requireDisposableContainer(['ASPNETCORE_ENVIRONMENT=Development'], 'Development'));
});

test('completion messages keep request correlation separate from message identity', () => {
  const requestId = 'b8305498-f4c3-4517-a541-4dfa984ec67c';
  const contract = 'EduPlatform.Shared.Contracts.Events.Privacy:PersonalDataErasureExecutionRequestedV1';
  const message = { requestId };
  const envelope = createMassTransitEnvelope(contract, message, requestId);

  assert.equal(envelope.correlationId, requestId);
  assert.notEqual(envelope.messageId, requestId);
  assert.deepEqual(envelope.messageType, [`urn:message:${contract}`]);
  assert.equal(envelope.message, message);
});
