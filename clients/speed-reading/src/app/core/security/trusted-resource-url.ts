const trustedMapHosts = new Set(['www.google.com', 'maps.google.com']);

export function isTrustedMapEmbedUrl(value: string | null | undefined): boolean {
  if (!value?.trim()) return false;

  try {
    const url = new URL(value.trim());
    return url.protocol === 'https:'
      && trustedMapHosts.has(url.hostname.toLowerCase())
      && url.pathname.startsWith('/maps/embed');
  } catch {
    return false;
  }
}
