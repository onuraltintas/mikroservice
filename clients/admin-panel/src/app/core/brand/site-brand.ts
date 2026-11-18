import { DOCUMENT } from '@angular/common';
import { inject } from '@angular/core';
import { SiteBrandName, brandDative, brandNameForHost } from '../../../../shared/eduivme-sites';

/** The brand of the address this page is served on; call from a field initializer (injection context). */
export function injectSiteBrand(): { name: SiteBrandName; dative: string } {
  const name = brandNameForHost(inject(DOCUMENT).location?.hostname);
  return { name, dative: brandDative(name) };
}
