import { accountSites } from './auth.guard';
import { EDUIVME_SITE_URLS, siteForHost, siteHref, siteStartUrl } from '../../../../shared/eduivme-sites';

// Alan adı planı (user decision 18.11.2026): every group signs in on its own address.
describe('eduivme sites', () => {
  it('maps every public host to its group', () => {
    expect(siteForHost('maxkoc.com')).toBe('student');
    expect(siteForHost('WWW.maxkoc.com.')).toBe('student');
    expect(siteForHost('veli.maxkoc.com')).toBe('parent');
    expect(siteForHost('ogretmen.eduivme.com')).toBe('teacher');
    expect(siteForHost('kurum.eduivme.com')).toBe('institution');
    expect(siteForHost('admin.eduivme.com')).toBe('admin');
    expect(siteForHost('eduivme.com')).toBe('admin');
  });

  it('applies no rule on other hosts', () => {
    for (const host of ['localhost', '127.0.0.1', '', null, undefined, 'onuraltintas.net', 'evil.maxkoc.com.example'])
      expect(siteForHost(host)).toBeNull();
  });

  it('starts teachers and institutions in the staff portal', () => {
    expect(siteStartUrl('teacher')).toBe('https://ogretmen.eduivme.com/staff/');
    expect(siteStartUrl('institution')).toBe('https://kurum.eduivme.com/staff/');
    expect(siteStartUrl('parent')).toBe('https://veli.maxkoc.com/');
    expect(Object.values(EDUIVME_SITE_URLS).every(url => url.startsWith('https://'))).toBe(true);
  });

  it('lists the addresses of an account from its roles', () => {
    expect(accountSites({ product: 'coaching', roles: ['Student'] })).toEqual(['student']);
    expect(accountSites({ product: 'coaching', roles: ['Parent'] })).toEqual(['parent']);
    expect(accountSites({ product: 'coaching', roles: ['Student', 'Parent'] })).toEqual(['student', 'parent']);
    // InstitutionAdmin is also a management role of the admin panel (MANAGEMENT_ROLES), as before.
    expect(accountSites({ product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] })).toEqual(['teacher', 'institution', 'admin']);
    expect(accountSites({ product: 'admin', roles: ['SystemAdmin'] })).toEqual(['admin']);
    expect(accountSites(null)).toEqual([]);
    expect(accountSites({ product: 'speed-reading', roles: ['Student'] })).toEqual([]);
  });

  it('links to another group page absolutely on the public addresses only (B1)', () => {
    expect(siteHref('parent', '/auth/register/parent', 'maxkoc.com')).toBe('https://veli.maxkoc.com/auth/register/parent');
    expect(siteHref('parent', '/auth/register/parent', 'localhost')).toBe('/auth/register/parent');
  });
});
