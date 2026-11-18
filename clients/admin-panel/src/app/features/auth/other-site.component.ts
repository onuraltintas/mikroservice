import { DOCUMENT } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { accountSites, currentSite } from '../../core/auth/auth.guard';
import { EDUIVME_SITE_LABELS, EDUIVME_SITE_URLS, EduivmeSite, siteStartUrl } from '../../../../shared/eduivme-sites';

/**
 * Alan adı planı (user decision 18.11.2026): every group signs in on its own address. An account signed in on another
 * group's address sees where it belongs; the button ends the session here and opens that address.
 */
@Component({
  selector: 'app-other-site',
  standalone: true,
  template: `
    <main class="mx-auto max-w-lg p-8" data-testid="other-site">
      <h1 class="text-xl font-semibold">Bu adres {{ hereLabel() }} girişi için</h1>
      @if (target(); as site) {
        <p class="mt-3">Bu hesap <strong>{{ host(site) }}</strong> adresinde açılır. Buradaki oturum kapatılır.</p>
        <button type="button" class="mt-5 rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white"
                data-testid="other-site-go" [disabled]="busy()" (click)="go(site)">{{ host(site) }} adresine git</button>
      } @else {
        <p class="mt-3">Bu hesabın açılacağı bir adres bulunamadı.</p>
        <button type="button" class="mt-5 rounded-lg border px-4 py-2 font-semibold" [disabled]="busy()" (click)="signOut()">Çıkış yap</button>
      }
    </main>`
})
export class OtherSiteComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly here = currentSite();
  readonly busy = signal(false);
  readonly hereLabel = computed(() => this.here ? EDUIVME_SITE_LABELS[this.here] : 'başka bir hesap');
  readonly target = computed<EduivmeSite | null>(() =>
    accountSites(this.auth.userProfile()).find(site => site !== this.here) ?? null);

  /** Canlı test B4: an account that belongs on this address has nothing to move; it goes to its own start page. */
  ngOnInit(): void {
    if (this.here && accountSites(this.auth.userProfile()).includes(this.here))
      void this.router.navigateByUrl(this.auth.getLandingRoute() ?? '/');
  }

  host(site: EduivmeSite): string {
    return new URL(EDUIVME_SITE_URLS[site]).host;
  }

  async go(site: EduivmeSite): Promise<void> {
    this.busy.set(true);
    await this.auth.signOutHere();
    this.document.defaultView?.location.assign(siteStartUrl(site));
  }

  async signOut(): Promise<void> {
    this.busy.set(true);
    await this.auth.logout();
  }
}
