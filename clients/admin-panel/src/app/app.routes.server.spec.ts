import { RenderMode } from '@angular/ssr';
import { describe, expect, it } from 'vitest';
import { serverRoutes } from './app.routes.server';

describe('admin server routes', () => {
  it('does not prerender authentication and registration pages that load runtime legal content', () => {
    const authRoute = serverRoutes.find(route => route.path === 'auth/**');

    expect(authRoute?.renderMode).toBe(RenderMode.Client);
  });
});
