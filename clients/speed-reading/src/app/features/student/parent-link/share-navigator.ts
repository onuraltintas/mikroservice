import { DOCUMENT } from '@angular/common';
import { InjectionToken, inject } from '@angular/core';

/** The slice of `navigator` the share box uses; a token so specs can stand in for browsers with or without these APIs. */
export interface ShareNavigator {
  clipboard?: { writeText(text: string): Promise<void> };
  share?: (data: ShareData) => Promise<void>;
}

export const SHARE_NAVIGATOR = new InjectionToken<ShareNavigator | null>('SHARE_NAVIGATOR', {
  factory: () => (inject(DOCUMENT).defaultView?.navigator as ShareNavigator | undefined) ?? null
});
