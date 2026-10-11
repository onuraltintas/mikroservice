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
  it('applies Chunking word bands even with custom speed/group size and drops practice questions', () => {
    const player = create();
    player.exercise.exerciseTypeName = 'Chunking';
    player.parsedConfig = { engineType: 'word_highlight', engineConfig: { mode: 'chunking',
      readingPurpose: 'practice', content: { minWordCount: 5, maxWordCount: 8 },
      pacer: { speedWpm: 700, chunkSize: 6 } } };
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
    const config = player.createPreviewReadingConfig({ id: 'real', content: 'Bir iki üç dört beş altı.',
      questions: [{ type: 1, correctAnswer: 'A' }] });
    expect(config.questions).toEqual([]);
    expect(config.Questions).toEqual([]);
    expect(player.parsedConfig.engineConfig.content).toEqual({ minWordCount: 5, maxWordCount: 8 });
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
  it('filters Speed Reading by configured length and checks actual words in pinned content', () => {
    const player = create();
    player.parsedConfig = { engineType: 'word_highlight', engineConfig: { mode: 'speed_reading',
      content: { minWordCount: 10, maxWordCount: 20 } } };
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
    player.parsedConfig.readingTextId = 'actual';
    player.exerciseService.getReadingText.and.returnValue(of({ id: 'actual', content: 'Çok kısa metin.',
      wordCount: 15, difficultyLevel: 3, isActive: true }));
    player.startPreviewSession();
    expect(player.initializePreviewEngine).not.toHaveBeenCalled();
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
