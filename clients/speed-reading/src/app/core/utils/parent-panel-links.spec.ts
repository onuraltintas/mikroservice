import { parentPanelLinks } from './parent-panel-links';

describe('parentPanelLinks', () => {
  it('points parents at the coaching site, not the management panel', () => {
    const links = parentPanelLinks();
    expect(links.login).toBe('https://onuraltintas.net/auth/login');
    expect(links.register).toBe('https://onuraltintas.net/auth/register/parent');
    expect(links.login).not.toContain('eduivme.com');
  });

  it('builds both links from the given origin', () => {
    expect(parentPanelLinks('https://staging.example.test/')).toEqual({
      login: 'https://staging.example.test/auth/login', register: 'https://staging.example.test/auth/register/parent'
    });
  });
});
