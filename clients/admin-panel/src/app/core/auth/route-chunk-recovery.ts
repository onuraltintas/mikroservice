/** Refresh an outdated route bundle once; authentication and other errors keep their normal handling. */
export function recoverMissingRouteChunk(event: { error: unknown; url: string }): void {
  if (typeof window === 'undefined') return;
  const message = event.error instanceof Error ? event.error.message : String(event.error);
  if (!/Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module|ChunkLoadError|Loading chunk [\w-]+ failed/i.test(message)) return;

  try {
    const target = new URL(event.url, window.location.href);
    if (target.origin !== window.location.origin) return;
    const key = 'admin.route-chunk-reload-at';
    const now = Date.now();
    if (Number(window.sessionStorage.getItem(key)) > now - 60_000) return;
    window.sessionStorage.setItem(key, String(now));
    window.location.assign(target.href);
  } catch {
    // Unavailable storage must not cause an automatic reload loop.
  }
}
