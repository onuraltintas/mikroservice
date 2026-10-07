import { of, throwError, Subject } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

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
});
