import { Component, OnInit, inject } from '@angular/core';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';

@Component({
  selector: 'app-legacy-teacher-redirect',
  standalone: true,
  template: `<main class="p-8"><h1 class="text-xl font-semibold">Öğretmen paneli taşındı</h1><p class="mt-2">Öğretmen ve kurum işlemleri yeni personel portalında.</p><a class="mt-4 inline-block font-semibold text-indigo-700" [href]="destination">Yeni personel portalını aç</a></main>`
})
export class LegacyTeacherRedirectComponent implements OnInit {
  private readonly redirectService = inject(StaffPortalRedirectService);
  readonly destination = this.redirectService.url('coaching');

  ngOnInit(): void {
    this.redirectService.redirect('coaching');
  }
}
