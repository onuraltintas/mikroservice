import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-legacy-staff-redirect',
  standalone: true,
  template: `<main class="p-8"><h1 class="text-xl font-semibold">Personel paneli taşındı</h1><p class="mt-2">Hızlı Okuma öğretmen ve kurum işlemleri artık ortak personel portalında.</p><a class="mt-4 inline-block font-semibold text-(--ui-brand-text)" [href]="destination">Yeni personel portalını aç</a></main>`
})
export class LegacyStaffRedirectComponent implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  readonly destination = environment.teacherPortalUrl + '?product=speed-reading';

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) window.location.replace(this.destination);
  }
}
