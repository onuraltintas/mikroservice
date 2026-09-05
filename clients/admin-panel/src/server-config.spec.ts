import { describe, expect, it } from 'vitest';

import { serverAllowedHosts, serverTrustProxyHeaders } from './server-config';

describe('admin SSR server configuration', () => {
  it('allows the public admin domains and local health-check hosts', () => {
    expect(serverAllowedHosts).toEqual(
      expect.arrayContaining([
        'eduivme.com',
        'www.eduivme.com',
        'eduivme.com.tr',
        'www.eduivme.com.tr',
        'localhost',
        '127.0.0.1',
      ]),
    );
  });

  it('trusts only the forwarded headers used by the production proxy chain', () => {
    expect(serverTrustProxyHeaders).toEqual([
      'x-forwarded-for',
      'x-forwarded-host',
      'x-forwarded-port',
      'x-forwarded-proto',
    ]);
  });
});
