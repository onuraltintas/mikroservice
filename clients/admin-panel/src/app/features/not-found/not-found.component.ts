import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Live test 14.11.2026: an unknown address was redirected to /dashboard by the route table. Rendered on the server that became an
 * HTTP redirect, and the Coaching site (which does not serve /dashboard) answered 404. An unknown address is now a page of its own:
 * in the browser it sends a signed-in user to their own start page and a guest to the login page.
 */
@Component({
  selector: 'app-not-found', standalone: true, imports: [RouterLink],
  template: `<main class="p-8" data-testid="not-found"><h1 class="text-xl font-semibold">Sayfa bulunamadı</h1>
    <p class="mt-2">Bu adres yok ya da taşınmış. Başlangıç sayfası açılıyor.</p>
    <a class="mt-4 inline-block font-semibold underline" routerLink="/auth/login">Giriş sayfasına git</a></main>`
})
export class NotFoundComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  async ngOnInit(): Promise<void> {
    if (!this.browser) return;
    const signedIn = await this.auth.waitForAuth();
    await this.router.navigateByUrl(signedIn ? this.auth.getLandingRoute() ?? '/dashboard' : '/auth/login', { replaceUrl: true });
  }
}
