import { randomUUID } from 'node:crypto';

export function requireDisposableEnvironment(env) {
  if (env.E2E_DISPOSABLE_ENV !== 'true') {
    throw new Error('Privacy Docker E2E requires E2E_DISPOSABLE_ENV=true.');
  }
  if (!['Development', 'Staging'].includes(env.ENVIRONMENT)) {
    throw new Error('Privacy Docker E2E cannot run against Production or an unknown environment.');
  }
}

export function requireDisposableContainer(containerEnv, expectedEnvironment) {
  const actual = containerEnv.find(value => value.startsWith('ASPNETCORE_ENVIRONMENT='))
    ?.slice('ASPNETCORE_ENVIRONMENT='.length);
  if (!['Development', 'Staging'].includes(actual)) {
    throw new Error('Privacy Docker E2E cannot use a Production or unknown Identity container.');
  }
  if (actual !== expectedEnvironment) {
    throw new Error('Privacy Docker E2E environment mismatch between configuration and Identity container.');
  }
}

export function createMassTransitEnvelope(contract, message, requestId) {
  return {
    messageId: randomUUID(),
    correlationId: requestId,
    conversationId: randomUUID(),
    sourceAddress: 'rabbitmq://rabbitmq/privacy-docker-e2e',
    destinationAddress: `rabbitmq://rabbitmq/${contract}`,
    messageType: [`urn:message:${contract}`],
    message,
  };
}
