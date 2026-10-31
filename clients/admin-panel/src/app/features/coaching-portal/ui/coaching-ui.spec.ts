import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CsBottomSheetComponent, CsCardComponent, CsEmptyStateComponent, CsListRowComponent, CsPageHeaderComponent,
  CsProgressBarComponent, CsProgressRingComponent, CsSegmentedTabsComponent, CsStatusChipComponent
} from './index';

// T1 (31.10.2026): small, token-styled building blocks for the redesigned student pages.
@Component({
  standalone: true,
  imports: [CsCardComponent, CsListRowComponent, CsStatusChipComponent, CsProgressRingComponent, CsProgressBarComponent,
    CsBottomSheetComponent, CsEmptyStateComponent, CsPageHeaderComponent, CsSegmentedTabsComponent],
  template: `
    <cs-page-header title="Ödevlerim" subtitle="Bu hafta 3 ödevin var"><button actions>Yeni</button></cs-page-header>
    <cs-card tone="raised"><p>İçerik</p></cs-card>
    <cs-list-row title="TYT Matematik" meta="Bugün 16:00"><cs-status-chip trailing tone="danger" label="Gecikti" /></cs-list-row>
    <cs-progress-ring [value]="3" [max]="4" label="Haftalık görevler" />
    <cs-progress-bar [value]="60" [max]="100" label="Hedefe ilerleme" />
    <cs-empty-state title="Henüz deneme yok" text="İlk denemeni ekle."><button>Deneme ekle</button></cs-empty-state>
    <cs-segmented-tabs label="Ödev filtresi" [options]="options" [(value)]="tab" />
    <cs-bottom-sheet title="Görev ayrıntısı" [(open)]="sheet"><button class="first">Tamamla</button></cs-bottom-sheet>`
})
class HostComponent {
  options = [{ value: 'todo', label: 'Yapılacak', count: 2 }, { value: 'done', label: 'Teslim edilen' }, { value: 'all', label: 'Tümü' }];
  tab = signal('todo');
  sheet = signal(false);
}

describe('student coaching UI kit', () => {
  const render = async () => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges(); await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  };

  it('renders headings, rows and chips with their text', async () => {
    const { el } = await render();
    expect(el.querySelector('cs-page-header h1')?.textContent).toContain('Ödevlerim');
    expect(el.querySelector('cs-page-header [actions]')).not.toBeNull();
    expect(el.querySelector('cs-card')?.getAttribute('data-tone')).toBe('raised');
    expect(el.querySelector('cs-list-row')?.textContent).toContain('TYT Matematik');
    const chip = el.querySelector('cs-status-chip') as HTMLElement;
    expect(chip.getAttribute('data-tone')).toBe('danger');
    expect(chip.textContent?.trim()).toBe('Gecikti');
    expect(el.querySelector('cs-empty-state')?.textContent).toContain('Henüz deneme yok');
  });

  it('exposes progress to assistive technology', async () => {
    const { el } = await render();
    const ring = el.querySelector('cs-progress-ring [role=progressbar]') as HTMLElement;
    expect(ring.getAttribute('aria-valuenow')).toBe('3');
    expect(ring.getAttribute('aria-valuemax')).toBe('4');
    expect(ring.getAttribute('aria-label')).toBe('Haftalık görevler');
    const bar = el.querySelector('cs-progress-bar [role=progressbar]') as HTMLElement;
    expect(bar.getAttribute('aria-valuenow')).toBe('60');
  });

  it('segmented tabs select with click and arrow keys', async () => {
    const { fixture, el } = await render();
    const tabs = [...el.querySelectorAll('cs-segmented-tabs [role=tab]')] as HTMLButtonElement[];
    expect(el.querySelector('cs-segmented-tabs [role=tablist]')?.getAttribute('aria-label')).toBe('Ödev filtresi');
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    expect(tabs[0].textContent).toContain('2');
    tabs[1].click(); fixture.detectChanges();
    expect(fixture.componentInstance.tab()).toBe('done');
    tabs[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true })); fixture.detectChanges();
    expect(fixture.componentInstance.tab()).toBe('all');
    expect(document.activeElement).toBe(tabs[2]);
  });

  it('bottom sheet opens as a dialog, focuses its close button and closes with Escape', async () => {
    const { fixture, el } = await render();
    expect(el.querySelector('cs-bottom-sheet [role=dialog]')).toBeNull();
    fixture.componentInstance.sheet.set(true); fixture.detectChanges(); await fixture.whenStable();
    const dialog = el.querySelector('cs-bottom-sheet [role=dialog]') as HTMLElement;
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-label')).toBe('Görev ayrıntısı');
    // T5: focus lands on the close button, not on the first field, so the keyboard does not pop up on a phone.
    expect(document.activeElement?.classList.contains('close')).toBe(true);
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); fixture.detectChanges();
    expect(fixture.componentInstance.sheet()).toBe(false);
  });
});
