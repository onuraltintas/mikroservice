import { describe, expect, it } from 'vitest';

import { resolveServerAllowedHosts, serverAllowedHosts, serverTrustProxyHeaders } from './server-config';

describe('admin SSR server configuration', () => {
  it('allows the public admin domains and local health-check hosts', () => {
    expect(serverAllowedHosts).toEqual(
      expect.arrayContaining([
        'eduivme.com',
        'www.eduivme.com',
        'eduivme.com.tr',
        'www.eduivme.com.tr',
        'onuraltintas.net',
        'www.onuraltintas.net',
        'localhost',
        '127.0.0.1',
      ]),
    );
  });

  it('adds exact configured staging hosts without allowing wildcard or URL entries', () => {
    const allowedHosts = resolveServerAllowedHosts(
      ' staging.onuraltintas.net, EDUivme.com, *.example.test, https://untrusted.example, host.test:443 ',
    );

    expect(allowedHosts).toContain('staging.onuraltintas.net');
    expect(allowedHosts.filter((host) => host === 'eduivme.com')).toHaveLength(1);
    expect(allowedHosts).not.toContain('*.example.test');
    expect(allowedHosts).not.toContain('https://untrusted.example');
    expect(allowedHosts).not.toContain('host.test:443');
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
