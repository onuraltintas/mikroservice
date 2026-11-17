import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export function resolveCoachingDestination(pathname: string): string {
  return pathname.replace(/\/+$/, '') === '/student/coaching'
    ? new URL('/coaching-portal', environment.coachingSiteUrl).href
    : environment.teacherPortalUrl + '?product=coaching';
}

@Component({
  selector: 'app-coaching-redirect',
  standalone: true,
  template: `<main><h1>Koçluk paneli</h1><p>Güncel koçluk çalışma alanı açılıyor.</p><a [href]="destination">Koçluk panelini aç</a></main>`
})
export class CoachingRedirectComponent implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  destination = new URL('/coaching-portal', environment.coachingSiteUrl).href;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.destination = resolveCoachingDestination(window.location.pathname);
    window.location.replace(this.destination);
  }
}
