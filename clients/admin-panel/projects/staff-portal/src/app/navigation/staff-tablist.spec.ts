import { moveTabFocus } from './staff-tablist';

// Roadmap D4: history category tabs move with arrow keys, Home and End and select the tab they land on.
describe('moveTabFocus', () => {
  it('focuses and clicks the neighbouring tab and ignores other keys', () => {
    const list = document.createElement('div'); list.setAttribute('role', 'tablist');
    const clicked: string[] = [];
    for (const name of ['a', 'b', 'c']) {
      const tab = document.createElement('button'); tab.setAttribute('role', 'tab'); tab.id = name;
      tab.setAttribute('aria-selected', String(name === 'a')); tab.addEventListener('click', () => clicked.push(name)); list.appendChild(tab);
    }
    document.body.appendChild(list); list.addEventListener('keydown', moveTabFocus);
    const press = (key: string) => list.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    press('ArrowLeft'); expect(document.activeElement?.id).toBe('c');
    press('Home'); press('ArrowRight'); press('End'); press('Enter');
    expect(clicked).toEqual(['c', 'a', 'b', 'c']);
    list.remove();
  });
});
