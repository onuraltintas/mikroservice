import { describe, expect, it } from 'vitest';

import { resolveServerAllowedHosts, serverAllowedHosts, serverTrustProxyHeaders } from './server-config';
import { googleAuthOpenerPolicy } from './server-config';

describe('admin SSR server configuration', () => {
  it('allows Google popups only on authentication documents', () => {
    expect(googleAuthOpenerPolicy('/auth/login')).toBe('same-origin-allow-popups');
    expect(googleAuthOpenerPolicy('/auth/register/student')).toBe('same-origin-allow-popups');
    expect(googleAuthOpenerPolicy('/coaching-portal')).toBeNull();
    expect(googleAuthOpenerPolicy('/authentic')).toBeNull();
  });
  it('allows the public admin domains and local health-check hosts', () => {
    expect(serverAllowedHosts).toEqual(
      expect.arrayContaining([
        'eduivme.com',
        'www.eduivme.com',
        'admin.eduivme.com',
        'ogretmen.eduivme.com',
        'kurum.eduivme.com',
        'maxkoc.com',
        'www.maxkoc.com',
        'veli.maxkoc.com',
        'localhost',
        '127.0.0.1',
      ]),
    );
  });

  it('adds exact configured staging hosts without allowing wildcard or URL entries', () => {
    const allowedHosts = resolveServerAllowedHosts(
      ' extra.example.test, EDUivme.com, *.example.test, https://untrusted.example, host.test:443 ',
    );

    expect(allowedHosts).toContain('extra.example.test');
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
