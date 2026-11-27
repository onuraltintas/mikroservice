import { environment } from '../../../environments/environment';

/** Canonical origin of the public site, for example `https://masterhizliokuma.com`. */
export const PUBLIC_SITE_URL = new URL(environment.publicSiteUrl).origin;

const bareHost = (host: string) => host.toLowerCase().replace(/^www\./, '');

/**
 * Returns the address search engines should index for a page of this site:
 * always `https://masterhizliokuma.com/<path>`, without query string or fragment.
 * Addresses on the site's own host, its www variant or the host the page is open on
 * (a local or preview address) are folded onto the canonical origin; addresses on
 * other sites are returned unchanged.
 */
export function canonicalPageUrl(
  url: string,
  siteUrl: string = PUBLIC_SITE_URL,
  currentHost: string = globalThis.location?.hostname ?? '',
): string {
  const site = new URL(siteUrl);
  let parsed: URL;
  try {
    parsed = new URL(url, site.origin);
  } catch {
    return url;
  }
  const sameSite = bareHost(parsed.hostname) === bareHost(site.hostname)
    || (currentHost !== '' && parsed.hostname.toLowerCase() === currentHost.toLowerCase());
  return sameSite ? `${site.origin}${parsed.pathname || '/'}` : url;
}
