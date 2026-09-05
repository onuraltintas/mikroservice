/** Hostnames accepted by the public admin edge and the local container health check. */
export const serverAllowedHosts = [
  'eduivme.com',
  'www.eduivme.com',
  'eduivme.com.tr',
  'www.eduivme.com.tr',
  'localhost',
  '127.0.0.1',
] as const;

/** Headers added by the trusted LiteSpeed/Caddy proxy chain. */
export const serverTrustProxyHeaders = [
  'x-forwarded-for',
  'x-forwarded-host',
  'x-forwarded-port',
  'x-forwarded-proto',
] as const;
