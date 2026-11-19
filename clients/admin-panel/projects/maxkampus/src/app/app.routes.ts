import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { MaxkampusSession } from './core/session.service';

/** /panel and /plan need a maxkampüs session; without one the visitor signs in and comes back. */
export const signedIn: CanActivateFn = async (_route, state) => {
  const session = inject(MaxkampusSession);
  if (await session.ensure().catch(() => false)) return true;
  return inject(Router).createUrlTree(['/giris'], { queryParams: { donus: state.url } });
};

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./site/site-shell.component').then(m => m.SiteShellComponent),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./site/home.component').then(m => m.HomeComponent) },
      { path: 'fiyatlar', loadComponent: () => import('./site/content-pages.component').then(m => m.PricingComponent) },
      { path: 'blog', loadComponent: () => import('./site/blog.component').then(m => m.BlogComponent) },
      { path: 'blog/:slug', loadComponent: () => import('./site/blog.component').then(m => m.PostComponent) },
      { path: 'sayfa/:slug', loadComponent: () => import('./site/content-pages.component').then(m => m.ContentPageComponent) },
      { path: 'yasal/:slug', loadComponent: () => import('./site/content-pages.component').then(m => m.LegalPageComponent) },
      { path: 'iletisim', loadComponent: () => import('./site/contact.component').then(m => m.ContactComponent) },
      { path: 'bulten/onay', data: { mode: 'onay' }, loadComponent: () => import('./site/newsletter-result.component').then(m => m.NewsletterResultComponent) },
      { path: 'bulten/ayril', data: { mode: 'ayril' }, loadComponent: () => import('./site/newsletter-result.component').then(m => m.NewsletterResultComponent) },
      // Addresses of the plan (ozellikler, sss) lead to their place on the home page.
      { path: 'ozellikler', redirectTo: () => inject(Router).createUrlTree(['/'], { fragment: 'ozellikler' }) },
      { path: 'sss', redirectTo: '/' }
    ]
  },
  { path: 'giris', loadComponent: () => import('./account/login.component').then(m => m.LoginComponent) },
  { path: 'kayit', loadComponent: () => import('./account/register.component').then(m => m.RegisterComponent) },
  { path: 'kayit/google', loadComponent: () => import('./account/google-finish.component').then(m => m.GoogleFinishComponent) },
  { path: 'sifremi-unuttum', loadComponent: () => import('./account/recovery.component').then(m => m.ForgotComponent) },
  { path: 'sifre-yenile', loadComponent: () => import('./account/recovery.component').then(m => m.ResetComponent) },
  { path: 'eposta-dogrula', loadComponent: () => import('./account/recovery.component').then(m => m.VerifyEmailComponent) },
  { path: 'davet/:kod', loadComponent: () => import('./account/invite.component').then(m => m.InviteComponent) },
  { path: 'panel', canActivate: [signedIn], loadComponent: () => import('./panel/panel-gate.component').then(m => m.PanelGateComponent) },
  { path: 'plan', canActivate: [signedIn], loadComponent: () => import('./panel/panel-gate.component').then(m => m.PlanComponent) },
  {
    path: '**',
    loadComponent: () => import('./site/site-shell.component').then(m => m.SiteShellComponent),
    children: [{ path: '**', loadComponent: () => import('./site/content-pages.component').then(m => m.NotFoundComponent) }]
  }
];
