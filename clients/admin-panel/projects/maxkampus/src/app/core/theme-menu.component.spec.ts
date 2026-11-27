import { TestBed } from '@angular/core/testing';
import { ThemeMenuComponent } from './theme-menu.component';

describe('ThemeMenuComponent', () => {
  beforeEach(() => { localStorage.removeItem('theme'); document.documentElement.classList.remove('dark'); });
  afterEach(() => { localStorage.removeItem('theme'); document.documentElement.classList.remove('dark'); });

  it('opens light, offers light, dark and the device setting, and keeps the choice in this browser', () => {
    const fixture = TestBed.createComponent(ThemeMenuComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const buttons = [...element.querySelectorAll('.theme__menu button')] as HTMLButtonElement[];
    expect(buttons.map(button => button.textContent?.trim())).toEqual(['Açık', 'Koyu', 'Cihaz ayarına göre']);
    // 27.11.2026: without a saved choice the site opens light.
    expect(element.querySelector('summary')?.getAttribute('aria-label')).toBe('Görünüm: Açık');

    buttons[1].click();
    fixture.detectChanges();
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('theme')).toBe('dark');
    expect(buttons[1].getAttribute('aria-pressed')).toBe('true');
    expect((element.querySelector('details') as HTMLDetailsElement).open).toBe(false);

    buttons[0].click();
    fixture.detectChanges();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    expect(element.querySelector('summary')?.getAttribute('aria-label')).toBe('Görünüm: Açık');
  });
});
