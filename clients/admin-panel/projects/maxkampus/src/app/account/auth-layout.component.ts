import { Location } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { BrandMarkComponent } from '../site/brand-mark.component';

/** Sign-in and registration frame: what maxkampüs does on the left (graphite), the form on the right. */
@Component({
  selector: 'mk-auth-layout',
  standalone: true,
  imports: [RouterLink, BrandMarkComponent],
  styles: `.mk-back { border: 0; background: none; padding: 0; cursor: pointer; font: inherit; }`,
  template: `
    <div class="ds-auth">
      <aside class="ds-auth__visual" aria-label="maxkampüs">
        <p class="ds-auth__headline">{{ headline() }}</p>
        <p class="ds-auth__sub">Ders programı, yoklama, ödev ve tahsilat tek yerde. Veliler devamsızlığı ve taksiti e-postayla öğrenir.</p>
        <ul class="ds-auth__points">
          <li>Öğretmenler yalnız kendi derslerini görür</li>
          <li>Velilere giden e-postaları siz seçersiniz</li>
          <li>Kayıtlar kurumunuzundur</li>
        </ul>
      </aside>
      <main class="ds-auth__main">
        <div class="ds-auth__panel">
          <button type="button" class="ds-link ds-back-link mk-back" (click)="back()">← Geri</button>
          <a routerLink="/" class="ds-auth__brand" aria-label="maxkampüs ana sayfa">
            <mk-brand-mark [size]="40" />
            <span><span class="ds-auth__brand-name">maxkampüs</span></span>
          </a>
          <ng-content />
        </div>
      </main>
    </div>
  `
})
export class AuthLayoutComponent {
  readonly headline = input('Kursunuzun bütün haftası tek ekranda');
  private readonly location = inject(Location);
  private readonly router = inject(Router);

  /**
   * "← Geri" (20.11.2026 live test): the page the visitor came from inside maxkampus.com, else the home page.
   * Angular numbers in-app navigations in history.state; 1 means this page was opened directly.
   */
  back(): void {
    const navigationId = (globalThis.history?.state as { navigationId?: number } | null)?.navigationId ?? 1;
    if (navigationId > 1) this.location.back();
    else void this.router.navigateByUrl('/');
  }
}
