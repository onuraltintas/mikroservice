import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AttentionPreviewComponent } from './attention-preview.component';

describe('Attention prototype UI', () => {
  beforeEach(() => TestBed.configureTestingModule({
    imports: [AttentionPreviewComponent], providers: [provideRouter([])]
  }));

  it('shows the simple rule and a non-persistence notice before starting', () => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Hedefi Yakala');
    expect(fixture.nativeElement.textContent).toContain('Sonuçlar kaydedilmez');
    expect(fixture.nativeElement.querySelectorAll('.shape-button').length).toBe(0);
    fixture.destroy();
  });

  it('runs three untimed practice rounds before the scored trial', fakeAsync(() => {
    spyOn(performance, 'now').and.callFake(() => Date.now());
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const c = fixture.componentInstance;
    c.selectPreset(1); c.startTutorial(); tick(10000);
    expect(c.game.status).toBe('playing'); expect(c.game.shapes.length).toBe(6);
    for (let i = 0; i < 3; i++) { c.choose(c.game.shapes.indexOf('star')); tick(800); }
    expect(c.game.status).toBe('ready'); expect(c.game.hits).toBe(0);
    c.startSession(); expect(c.game.shapes.length).toBe(8);
    c.choose(c.game.shapes.indexOf('star')); expect(c.game.hits).toBe(1);
    c.reset(); expect(c.game.status).toBe('intro'); expect(c.game.hits).toBe(0);
    fixture.destroy(); tick(10000);
  }));

  it('does not bypass the practice stage, and ignores invalid preset choices', () => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); const c = fixture.componentInstance;
    c.startSession(); expect(c.game.status).toBe('intro');
    for (const index of [-1, 20, 0.5, NaN]) c.selectPreset(index);
    expect(c.selectedPresetIndex).toBe(0);
    c.startTutorial(); c.selectPreset(2); expect(c.selectedPresetIndex).toBe(0);
    fixture.destroy();
  });

  it('pauses a hidden page without resuming it automatically', fakeAsync(() => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const c = fixture.componentInstance; c.startTutorial();
    spyOnProperty(document, 'hidden', 'get').and.returnValue(true);
    document.dispatchEvent(new Event('visibilitychange')); tick(20000);
    expect(c.game.status).toBe('paused');
    c.togglePause(); expect(c.game.status).toBe('playing');
    c.togglePause(); expect(c.game.status).toBe('paused');
    fixture.destroy();
  }));

  it('keeps keyboard focus on the board between tutorial rounds', fakeAsync(() => {
    spyOn(performance, 'now').and.callFake(() => Date.now());
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const c = fixture.componentInstance; c.startTutorial(); fixture.detectChanges();
    const index = c.game.shapes.indexOf('star');
    const button = fixture.nativeElement.querySelectorAll('.shape-button')[index] as HTMLButtonElement;
    button.focus(); button.click(); fixture.detectChanges(); tick(800); fixture.detectChanges();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelectorAll('.shape-button')[index]);
    fixture.destroy();
  }));

  it('moves focus from the removed start control to the first shape', () => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const start = fixture.nativeElement.querySelector('.primary') as HTMLButtonElement;
    start.focus(); start.click(); fixture.detectChanges();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.shape-button'));
    fixture.destroy();
  });

  it('offers four age groups and five levels with the approved defaults', () => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelectorAll('.age-option').length).toBe(4);
    expect(root.querySelectorAll('.level-option').length).toBe(5);
    expect(root.querySelector('.settings-summary')?.textContent).toContain('24 tur');
    expect(root.querySelector('.settings-summary')?.textContent).toContain('6 saniye');
    fixture.destroy();
  });

  it('switches every age and level through the controls and preserves both on reset', () => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const c = fixture.componentInstance;
    const root = fixture.nativeElement as HTMLElement;
    for (let age = 0; age < 4; age++) {
      root.querySelectorAll<HTMLButtonElement>('.age-option')[age].click(); fixture.detectChanges();
      for (let level = 1; level <= 5; level++) {
        root.querySelectorAll<HTMLButtonElement>('.level-option')[level - 1].click(); fixture.detectChanges();
        expect(c.selectedAgeGroupIndex).toBe(age);
        expect(c.selectedLevel).toBe(level);
        expect(c.game.preset).toBe(c.presets[age * 5 + level - 1]);
        expect(root.querySelectorAll('.age-option[aria-pressed="true"]').length).toBe(1);
        expect(root.querySelectorAll('.level-option[aria-pressed="true"]').length).toBe(1);
      }
    }
    c.startTutorial(); c.reset(); fixture.detectChanges();
    expect(c.selectedAgeGroupIndex).toBe(3); expect(c.selectedLevel).toBe(5);
    expect(c.game.preset.rounds).toBe(48);
    fixture.destroy();
  });

  it('rejects invalid age or level values and locks settings after starting', () => {
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const c = fixture.componentInstance;
    for (const age of [-1, 4, .5, NaN]) c.selectAgeGroup(age);
    for (const level of [-1, 0, 6, .5, NaN]) c.selectLevel(level);
    expect(c.selectedPresetIndex).toBe(0);
    c.selectLevel(5); c.selectAgeGroup(2);
    expect(c.selectedPresetIndex).toBe(14);
    c.startTutorial(); c.selectAgeGroup(0); c.selectLevel(1);
    expect(c.selectedPresetIndex).toBe(14);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.age-option')).toBeNull();
    fixture.destroy();
  });

  it('shows the target as a non-interactive SVG above the board throughout play and pause', fakeAsync(() => {
    spyOn(performance, 'now').and.callFake(() => Date.now());
    const fixture = TestBed.createComponent(AttentionPreviewComponent); fixture.detectChanges();
    const c = fixture.componentInstance; c.startTutorial(); fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const target = root.querySelector('.target-cue');
    expect(target?.querySelector('svg[role="img"]')?.getAttribute('aria-label')).toBe('Aranacak sembol: yıldız');
    expect(target?.querySelector('polygon')?.getAttribute('points')).toBe(root.querySelector('.shape-button[aria-label^="Yıldız"] polygon')?.getAttribute('points'));
    expect(target?.querySelector('button')).toBeNull();
    expect(target && target.compareDocumentPosition(root.querySelector('.board-wrap')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    c.choose(c.game.shapes.indexOf('star')); fixture.detectChanges();
    expect(root.querySelector('.target-cue svg')).not.toBeNull();
    c.togglePause(); fixture.detectChanges();
    expect(root.querySelector('.target-cue svg')).not.toBeNull();
    expect(root.querySelector('.target-reminder')).toBeNull();
    fixture.destroy();
  }));
});
