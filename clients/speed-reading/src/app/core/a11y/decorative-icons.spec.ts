import { hideDecorativeIcons, watchDecorativeIcons } from './decorative-icons';

describe('decorative icons (live test 19.11.2026)', () => {
  it('hides icon ligatures from screen readers so link names stay clean', () => {
    const root = document.createElement('div');
    root.innerHTML = '<a href="/"><span class="material-icons">dashboard</span>Ana Sayfa</a>';
    hideDecorativeIcons(root);
    expect(root.querySelector('.material-icons')!.getAttribute('aria-hidden')).toBe('true');
  });

  it('also hides icons added later', async () => {
    const observer = watchDecorativeIcons(document)!;
    const host = document.createElement('div');
    host.innerHTML = '<button aria-label="Duraklat"><span class="material-icons">pause</span></button>';
    document.body.appendChild(host);
    await new Promise(resolve => setTimeout(resolve));
    expect(host.querySelector('.material-icons')!.getAttribute('aria-hidden')).toBe('true');
    observer.disconnect();
    host.remove();
  });
});
