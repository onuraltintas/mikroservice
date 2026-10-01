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
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
