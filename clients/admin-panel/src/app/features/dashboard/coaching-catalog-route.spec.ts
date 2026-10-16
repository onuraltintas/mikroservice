import { DASHBOARD_ROUTES } from './dashboard.routes';
import { ADMIN_PERMISSIONS } from '../../core/auth/permissions';
import { permissionGuard } from '../../core/auth/auth.guard';

describe('Coaching catalog route', () => {
  it('requires global content management for the curriculum workspace', () => {
    const route = DASHBOARD_ROUTES.find(route => route.path === 'coaching/curriculum');
    expect(route?.canActivate).toContain(permissionGuard);
    expect(route?.data?.['role']).toBe('SystemAdmin');
    expect(route?.data?.['permission']).toBe(ADMIN_PERMISSIONS.coachingContentManage);
  });
  it('requires the global admin role and coaching read permission', () => {
    const route = DASHBOARD_ROUTES.find(route => route.path === 'coaching/catalog');
    expect(route?.canActivate).toContain(permissionGuard);
    expect(route?.data?.['role']).toBe('SystemAdmin');
    expect(route?.data?.['permission']).toBe(ADMIN_PERMISSIONS.coachingView);
  });
});
