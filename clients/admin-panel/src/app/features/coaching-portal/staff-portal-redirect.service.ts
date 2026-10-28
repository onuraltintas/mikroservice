import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class StaffPortalRedirectService {
  url(product: 'coaching' | 'speed-reading' = 'coaching'): string {
    return `${environment.staffPortalUrl}?product=${product}`;
  }

  redirect(product: 'coaching' | 'speed-reading' = 'coaching'): void {
    if (typeof window !== 'undefined') window.location.replace(this.url(product));
  }
}
