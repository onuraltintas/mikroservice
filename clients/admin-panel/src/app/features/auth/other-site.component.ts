import { DOCUMENT } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { accountSites, currentSite } from '../../core/auth/auth.guard';
import { AuthShellComponent } from './auth-shell/auth-shell.component';
import { EDUIVME_SITE_LABELS, EDUIVME_SITE_URLS, EduivmeSite, siteStartUrl } from '../../../../shared/eduivme-sites';

/**
 * Alan adı planı (user decision 18.11.2026): every group signs in on its own address. An account signed in on another
 * group's address sees where it belongs; the button ends the session here and opens that address.
 */
@Component({
  selector: 'app-other-site',
  standalone: true,
  imports: [AuthShellComponent],
  template: `
    <app-auth-shell>
      <section class="ds-auth__form" data-testid="other-site">
        <div class="ds-auth__heading">
          <h1 class="ds-auth__title">Bu adres {{ hereLabel() }} girişi için</h1>
          @if (target(); as site) {
            <p class="ds-auth__lead">Bu hesap <strong>{{ host(site) }}</strong> adresinde açılır. Buradaki oturum kapatılır.</p>
          } @else {
            <p class="ds-auth__lead">Bu hesabın açılacağı bir adres bulunamadı.</p>
          }
        </div>
        @if (target(); as site) {
          <button type="button" class="ds-btn ds-btn--primary ds-btn--large ds-btn--block"
                  data-testid="other-site-go" [disabled]="busy()" (click)="go(site)">{{ host(site) }} adresine git</button>
        } @else {
          <button type="button" class="ds-btn ds-btn--secondary ds-btn--large ds-btn--block" [disabled]="busy()" (click)="signOut()">Çıkış yap</button>
        }
      </section>
    </app-auth-shell>`
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
