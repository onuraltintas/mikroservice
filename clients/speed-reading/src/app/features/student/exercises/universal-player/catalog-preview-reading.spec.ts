import { of, throwError, Subject } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';
import { AdaptiveFluencyEngine } from './engines/adaptive-fluency.engine';

describe('Catalogue reading previews', () => {
  function create() {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    player.exercise = { id: 'exercise', difficultyLevel: 3, exerciseTypeName: 'SpeedReading' };
    player.parsedConfig = { engineType: 'word_highlight' };
    player.destroy$ = new Subject();
    player.initializePreviewEngine = jasmine.createSpy();
    player.finishLoading = jasmine.createSpy();
    player.exerciseService = { getReadingTexts: jasmine.createSpy().and.returnValue(of([
      { id: 'actual', difficultyLevel: 3, isActive: true, wordCount: 4 }
    ])), getReadingText: jasmine.createSpy().and.returnValue(of({ id: 'actual', content: 'Gerçek katalog metni burada.', difficultyLevel: 3, isActive: true, questions: [] })) };
    return player;
  }
  it('loads actual catalogue content when no text is pinned', () => {
    const player = create();
    player.startPreviewSession();
    expect(player.exerciseService.getReadingTexts).toHaveBeenCalled();
    expect(player.backendSessionConfig.readingTextContent).toBe('Gerçek katalog metni burada.');
    expect(player.initializePreviewEngine).toHaveBeenCalledTimes(1);
  });
  it('does not silently initialize a demo when a pinned text fails', () => {
    const player = create();
    player.parsedConfig.readingTextId = 'missing';
    player.exerciseService.getReadingText.and.returnValue(throwError(() => new Error('missing')));
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
    expect(player.error).toContain('metin');
  });
  it('rejects empty catalogue and never initializes fallback text', () => {
    const player = create();
    player.exerciseService.getReadingTexts.and.returnValue(of([]));
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
    expect(player.error).toContain('metin');
  });
  it('requires real text for the word-group alias and uppercase RSVP mode', () => {
    const player = create();
    player.parsedConfig = { engineType: 'word_group' };
    expect(player.previewRequiresReadingText()).toBeTrue();
    player.parsedConfig = { engineType: 'text_stream', mode: 'RSVP' };
    expect(player.previewRequiresReadingText()).toBeTrue();
  });
  it('rejects a pinned text belonging to another exercise', () => {
    const player = create();
    player.parsedConfig.readingTextId = 'actual';
    player.exerciseService.getReadingText.and.returnValue(of({ content: 'Başka egzersiz.', difficultyLevel: 3, exerciseId: 'other' }));
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
  });
  it('requires a distinct real transfer text for adaptive fluency', () => {
    const player = create();
    player.parsedConfig.engineType = 'adaptive_fluency';
    player.exerciseService.getReadingText.and.returnValue(of({ id: 'actual', content: 'Gerçek metin.', difficultyLevel: 3, questions: [{ type: 1 }] }));
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
    expect(player.error).toContain('metin');
  });
  it('keeps real adaptive text ahead of stale nested content', () => {
    const player = create();
    player.parsedConfig = { engineType: 'adaptive_fluency', readingTextId: 'actual', engineConfig: { content: 'Eski örnek' } };
    player.exerciseService.getReadingTexts.and.returnValue(of([{ id: 'transfer', difficultyLevel: 3 }]));
    player.exerciseService.getReadingText.and.callFake((id: string) => of({
      id, content: id === 'actual' ? 'Gerçek ana metin.' : 'Farklı aktarım metni.', difficultyLevel: 3, questions: [{ type: 1 }]
    }));
    player.startPreviewSession();
    const engine = new AdaptiveFluencyEngine();
    engine.initialize({ ...player.parsedConfig, ...player.backendSessionConfig }, {} as any);
    expect(engine.getText()).toBe('Gerçek ana metin.');
    expect(player.backendSessionConfig.adaptiveTransferContent).toBe('Farklı aktarım metni.');
    expect(player.initializePreviewEngine).toHaveBeenCalledTimes(1);
  });
});
