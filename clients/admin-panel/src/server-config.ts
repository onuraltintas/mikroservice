/** Hostnames accepted by the public admin edge and the local container health check. */
const defaultServerAllowedHosts = [
  'eduivme.com',
  'www.eduivme.com',
  'eduivme.com.tr',
  'www.eduivme.com.tr',
  'onuraltintas.net',
  'www.onuraltintas.net',
  'localhost',
  '127.0.0.1',
] as const;

const hostnameLabelPattern = /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/;

function isValidHostname(hostname: string): boolean {
  return hostname.length <= 253
    && hostname.split('.').every((label) => hostnameLabelPattern.test(label));
}

export function resolveServerAllowedHosts(
  additionalHosts = '',
): string[] {
  const configuredHosts = additionalHosts
    .split(',')
    .map((host) => host.trim().toLowerCase())
    .filter(isValidHostname);

  return [...new Set([...defaultServerAllowedHosts, ...configuredHosts])];
}

export const serverAllowedHosts = resolveServerAllowedHosts();

/** Google GIS popup compatibility is limited to authentication documents. */
export function googleAuthOpenerPolicy(path: string): string | null {
  return path === '/auth' || path.startsWith('/auth/') ? 'same-origin-allow-popups' : null;
}

/** Headers added by the trusted LiteSpeed/Caddy proxy chain. */
export const serverTrustProxyHeaders = [
  'x-forwarded-for',
  'x-forwarded-host',
  'x-forwarded-port',
  'x-forwarded-proto',
] as const;
