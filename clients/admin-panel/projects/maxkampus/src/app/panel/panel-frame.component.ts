import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { MaxkampusSession } from '../core/session.service';
import { BrandMarkComponent } from '../site/brand-mark.component';

/** The signed-in frame of maxkampus.com: brand, the owner's plan page, sign-out. */
@Component({
  selector: 'mk-panel-frame',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, BrandMarkComponent],
  template: `
    <header class="bar">
      <a routerLink="/panel" class="brand" aria-label="maxkampüs paneli"><mk-brand-mark [size]="30" /><strong>maxkampüs</strong></a>
      <nav aria-label="Hesap">
        <a routerLink="/panel" routerLinkActive="on" [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">Panel</a>
        @if (session.role() === 'owner') { <a routerLink="/plan" routerLinkActive="on" ariaCurrentWhenActive="page">Planım ve ödemeler</a> }
        <a routerLink="/iletisim">Destek</a>
        <button type="button" (click)="signOut()">Çıkış yap</button>
      </nav>
    </header>
    <ng-content />
  `,
  styles: `
    :host { display: block; min-height: 100dvh; background: var(--ds-bg); }
    .bar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 20px; background: var(--ds-surface); border-bottom: 1px solid var(--ds-line); }
    .brand { display: inline-flex; align-items: center; gap: 10px; color: var(--ds-text); text-decoration: none; font-size: 17px; }
    nav { display: flex; flex-wrap: wrap; gap: 4px; }
    nav a, nav button { display: inline-flex; align-items: center; min-height: 44px; padding: 0 12px; border: 0; border-radius: 8px; background: transparent; color: var(--ds-text); font: inherit; text-decoration: none; cursor: pointer; }
    nav a:hover, nav button:hover { background: var(--ds-soft); }
    nav a.on { font-weight: 600; box-shadow: inset 0 -3px 0 var(--ds-accent); }
  `
})
export class PanelFrameComponent {
  readonly session = inject(MaxkampusSession);
  private readonly router = inject(Router);
  async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/');
  }
}
