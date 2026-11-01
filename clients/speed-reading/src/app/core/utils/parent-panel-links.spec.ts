import { environment } from '../../../environments/environment';
import { parentPanelLinks } from './parent-panel-links';

describe('parentPanelLinks (V6b: veli paneline giden yol)', () => {
  it('derives the parent login and register pages from the configured coaching-site login URL', () => {
    const links = parentPanelLinks();
    expect(links.login).toBe(environment.centralAdminLoginUrl);
    expect(links.register).toBe(new URL('/auth/register/parent', environment.centralAdminLoginUrl).href);
  });

  it('follows another configured site instead of a hard-coded domain', () => {
    expect(parentPanelLinks('https://staging.example.test/auth/login')).toEqual({
      login: 'https://staging.example.test/auth/login',
      register: 'https://staging.example.test/auth/register/parent'
    });
  });
});
