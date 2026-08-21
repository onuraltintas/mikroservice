import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // The dashboard is authenticated and contains parameterized detail routes;
    // it must not be prerendered as public HTML.
    path: 'dashboard/**',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
