import { afterEach, describe, expect, it, vi } from 'vitest';
import { recoverMissingRouteChunk } from './route-chunk-recovery';

afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers(); });
function browser() {
  const assign = vi.fn();
  const values = new Map<string, string>();
  const sessionStorage = { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value) };
  vi.stubGlobal('window', { location: { href: 'https://eduivme.com/auth/login', origin: 'https://eduivme.com', assign }, sessionStorage });
  return { assign, sessionStorage };
}

describe('outdated route chunk recovery', () => {
  it.each(['Failed to fetch dynamically imported module: https://eduivme.com/chunk-old.js', 'Importing a module script failed.', 'error loading dynamically imported module', 'Loading chunk dashboard failed', 'ChunkLoadError'])('reloads the requested same-origin route after %s', message => {
    const { assign } = browser();
    recoverMissingRouteChunk({ error: new TypeError(message), url: '/dashboard?tab=users#list' });
    expect(assign).toHaveBeenCalledWith('https://eduivme.com/dashboard?tab=users#list');
  });

  it('limits repeated failures to one reload per minute', () => {
    const { assign } = browser();
    vi.useFakeTimers();
    const event = { error: new Error('Failed to fetch dynamically imported module'), url: '/dashboard' };
    recoverMissingRouteChunk(event);
    recoverMissingRouteChunk(event);
    expect(assign).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(60_001);
    recoverMissingRouteChunk(event);
    expect(assign).toHaveBeenCalledTimes(2);
  });

  it('leaves authentication, application and ordinary network failures unchanged', () => {
    const { assign } = browser();
    for (const message of ['403 Forbidden', 'Failed to fetch', 'Invalid password', 'Cannot read properties of undefined'])
      recoverMissingRouteChunk({ error: new Error(message), url: '/dashboard' });
    expect(assign).not.toHaveBeenCalled();
  });

  it('does not navigate outside the current origin or reload without storage', () => {
    const { assign, sessionStorage } = browser();
    recoverMissingRouteChunk({ error: new Error('ChunkLoadError'), url: '//example.test/phishing' });
    sessionStorage.setItem = () => { throw new Error('Storage blocked'); };
    recoverMissingRouteChunk({ error: new Error('ChunkLoadError'), url: '/dashboard' });
    expect(assign).not.toHaveBeenCalled();
  });

  it('does not access browser APIs during server rendering', () => {
    vi.stubGlobal('window', undefined);
    expect(() => recoverMissingRouteChunk({ error: new Error('ChunkLoadError'), url: '/dashboard' })).not.toThrow();
  });
});
