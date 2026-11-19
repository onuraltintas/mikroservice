import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // Authentication pages load runtime session and legal content; don't prerender them.
    path: 'auth/**',
    renderMode: RenderMode.Client
  },
  {
    // The dashboard is authenticated and contains parameterized detail routes;
    // it must not be prerendered as public HTML.
    path: 'dashboard/**',
    renderMode: RenderMode.Client
  },
  {
    // Coaching portal data is authenticated and includes parameterized assignment details.
    path: 'coaching-portal/**',
    renderMode: RenderMode.Client
  },
  {
    // Coaching CMS pages are public and parameterized; load them client-side to avoid prerendering dynamic slugs.
    path: 'coaching/**',
    renderMode: RenderMode.Client
  },
  {
    // Shared legal pages are populated from Identity at runtime, not during static prerendering.
    path: 'legal/**',
    renderMode: RenderMode.Client
  },
  {
    // Redesign 18.11.2026: "/" depends on the address (eduivme.com shows its own page, other hosts redirect), so it is
    // decided in the browser; a prerendered "/" would be one fixed redirect for every host.
    path: '',
    renderMode: RenderMode.Client
  },
  {
    // eduivme.com product pages: dynamic content and only on that address.
    path: 'urunler/**',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
