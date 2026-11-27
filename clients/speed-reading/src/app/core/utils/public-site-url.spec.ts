import { canonicalPageUrl } from './public-site-url';

describe('canonicalPageUrl', () => {
  const site = 'https://masterhizliokuma.com';

  it('folds the www host onto the canonical origin', () => {
    expect(canonicalPageUrl('https://www.masterhizliokuma.com/sss', site, 'www.masterhizliokuma.com'))
      .toBe('https://masterhizliokuma.com/sss');
  });

  it('drops the query string and fragment', () => {
    expect(canonicalPageUrl('https://masterhizliokuma.com/blog?page=2#top', site, 'masterhizliokuma.com'))
      .toBe('https://masterhizliokuma.com/blog');
  });

  it('keeps the root path for the home page', () => {
    expect(canonicalPageUrl('https://www.masterhizliokuma.com', site, 'www.masterhizliokuma.com'))
      .toBe('https://masterhizliokuma.com/');
  });

  it('resolves a path against the canonical origin', () => {
    expect(canonicalPageUrl('/legal/privacy', site, 'localhost')).toBe('https://masterhizliokuma.com/legal/privacy');
  });

  it('folds the address the page is open on, such as a local server', () => {
    expect(canonicalPageUrl('http://localhost:4200/iletisim', site, 'localhost')).toBe('https://masterhizliokuma.com/iletisim');
  });

  it('leaves addresses on other sites unchanged', () => {
    expect(canonicalPageUrl('https://example.org/yazi', site, 'masterhizliokuma.com')).toBe('https://example.org/yazi');
  });
});
