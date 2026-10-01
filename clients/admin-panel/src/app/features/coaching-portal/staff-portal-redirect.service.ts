import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class StaffPortalRedirectService {
  url(product: 'coaching' | 'speed-reading' = 'coaching'): string {
    return `https://onuraltintas.net/staff/?product=${product}`;
  }

  redirect(product: 'coaching' | 'speed-reading' = 'coaching'): void {
    if (typeof window !== 'undefined') window.location.replace(this.url(product));
  }
}
