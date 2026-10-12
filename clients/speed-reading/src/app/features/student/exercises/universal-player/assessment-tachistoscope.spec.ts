import { of, Subject } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('assessment stimulus versus reading content', () => {
  const ages = [
    { name: 'Çocuk', durationMs: 1500 },
    { name: 'Genç', durationMs: 1000 },
    { name: 'Genç yetişkin', durationMs: 900 },
    { name: 'Yetişkin', durationMs: 900 }
  ];

  function player(type = 'Tachistoscope', engineType = 'text_stream', paused = false): any {
    const component = Object.create(ExercisePlayerComponent.prototype) as any;
    component.exercise = { id: 'exercise', exerciseTypeName: type };
    component.parsedConfig = { engineType, engineConfig: { mode: 'tachistoscope' } };
    component.isAssessmentMode = true;
    component.assessmentAttemptId = 'attempt';
    component.destroy$ = new Subject<void>();
    component.comprehensionQuestions = [];
    component.error = '';
    component.isLoading = true;
    component.resetActionTracking = jasmine.createSpy();
    component.authService = { canPreviewExercises: () => false };
    component.cdr = { detectChanges: jasmine.createSpy() };
    component.initializeEngine = jasmine.createSpy();
    component.sessionService = { startSession: jasmine.createSpy().and.returnValue(of({
      sessionId: 'session', status: paused ? 2 : 1,
      configuration: component.parsedConfig,
      initialData: { tachistoscope: { count: 6, round: paused ? 2 : 0, displayDurationMs: 900 } }
    })) };
    return component;
  }

  for (const age of ages) {
    for (const paused of [false, true]) {
      it(`opens ${age.name} ${paused ? 'resumed' : 'new'} assessment without a reading text`, () => {
        const component = player('Tachistoscope', 'text_stream', paused);
        component.parsedConfig.engineConfig.timing = { durationMs: age.durationMs };
        component.startSession();
        expect(component.initializeEngine).toHaveBeenCalledTimes(1);
        expect(component.error).toBe('');
        expect(component.isLoading).toBeFalse();
        expect(component.restoredTachistoscopePaused).toBe(paused);
        expect(component.backendSessionConfig.readingTextContent).toBeUndefined();
        expect(component.sessionService.startSession).toHaveBeenCalledWith(jasmine.objectContaining({
          assessmentAttemptId: 'attempt', exerciseId: 'exercise'
        }));
        component.destroy$.complete();
      });
    }
  }

  for (const engineType of ['reading_comprehension', 'word_highlight', 'text_stream', 'rsvp',
    'text_fade', 'free_reading', 'regression_reduction', 'subvocalization_reduction', 'skimming', 'scanning']) {
    it(`still rejects missing text for the ${engineType} reading assessment`, () => {
      const component = player('RSVP', engineType);
      spyOn(console, 'error');
      component.startSession();
      expect(component.initializeEngine).not.toHaveBeenCalled();
      expect(component.error).toContain('Egzersiz hazırlanırken');
      expect(component.isLoading).toBeFalse();
      component.destroy$.complete();
    });
  }

  it('uses the server-pinned text for a real reading assessment', () => {
    const component = player('Comprehension', 'reading_comprehension');
    component.sessionService.startSession.and.returnValue(of({ sessionId: 'session',
      configuration: component.parsedConfig, initialData: { content: 'Sunucudan gelen gerçek metin', questions: [] } }));
    component.startSession();
    expect(component.initializeEngine).toHaveBeenCalledTimes(1);
    expect(component.backendSessionConfig.readingTextContent).toBe('Sunucudan gelen gerçek metin');
    expect(component.error).toBe('');
    component.destroy$.complete();
  });

  it('handles the case-insensitive exercise type without exempting other engines', () => {
    const component = player('TACHISTOSCOPE');
    expect(component.isReadingAssessmentEngine({ engineType: 'text_stream' })).toBeFalse();
    expect(component.isReadingAssessmentEngine({ engineType: 'reading_comprehension' })).toBeTrue();
    component.destroy$.complete();
  });
});
