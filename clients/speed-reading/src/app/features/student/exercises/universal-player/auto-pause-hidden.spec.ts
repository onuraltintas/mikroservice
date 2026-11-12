import { fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { NEVER, of, throwError } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

/**
 * Decision 12.11.2026: a running exercise pauses itself when its page is hidden (tab switch, minimised window,
 * closed tab), so that time away is not recorded as study time. It never resumes by itself.
 */
describe('auto-pause when the page is hidden', () => {
  let visibility: jasmine.Spy;

  beforeEach(() => {
    visibility = spyOnProperty(document, 'visibilityState', 'get').and.returnValue('hidden');
  });

  function player(engineType = 'visual_expansion'): any {
    const value = Object.create(ExercisePlayerComponent.prototype) as any;
    value.sessionId = 'session';
    value.actionQueue = Promise.resolve();
    value.engineState = { isRunning: true, isPaused: false, isCompleted: false };
    value.engine = {
      engineType,
      state: { isCompleted: false },
      pause: jasmine.createSpy('enginePause').and.callFake(() => value.engineState = { ...value.engineState, isPaused: true }),
      resume: jasmine.createSpy('engineResume').and.callFake(() => value.engineState = { ...value.engineState, isPaused: false }),
      stop: jasmine.createSpy('engineStop'),
      isAwaitingServer: () => false,
      getPhase: () => 'reading'
    };
    value.exercisePhase = 'reading';
    value.isAssessmentMode = false;
    value.isPauseTransitionPending = false;
    value.result = null;
    value.error = null;
    value.cdr = { detectChanges: () => undefined };
    value.showToast = jasmine.createSpy('toast');
    value.isPreviewSession = () => false;
    value.isTachistoscopeMode = () => false;
    value.isFixationMode = () => false;
    value.isSaccadeMode = () => false;
    value.sessionService = {
      pauseSession: jasmine.createSpy('pauseSession').and.returnValue(of({})),
      resumeSession: jasmine.createSpy('resumeSession').and.returnValue(of({}))
    };
    return value;
  }

  async function settle(): Promise<void> {
    for (let turn = 0; turn < 20; turn++) await Promise.resolve();
  }

  it('pauses a running, pausable exercise on the server and in the engine', async () => {
    const value = player();
    value.onVisibilityChange();
    await settle();
    expect(value.engine.pause).toHaveBeenCalledTimes(1);
    expect(value.sessionService.pauseSession).toHaveBeenCalledOnceWith('session');
    expect(value.engineState.isPaused).toBeTrue();
    expect(value.isPauseTransitionPending).toBeFalse();
  });

  it('also pauses when the page is being closed or put in the back-forward cache', async () => {
    visibility.and.returnValue('visible');
    const value = player();
    value.onPageHide();
    await settle();
    expect(value.sessionService.pauseSession).toHaveBeenCalledOnceWith('session');
    expect(value.engineState.isPaused).toBeTrue();
  });

  it('pauses only once when both page events fire', async () => {
    const value = player();
    value.onVisibilityChange();
    value.onPageHide();
    value.onVisibilityChange();
    await settle();
    value.onPageHide();
    await settle();
    expect(value.engine.pause).toHaveBeenCalledTimes(1);
    expect(value.sessionService.pauseSession).toHaveBeenCalledTimes(1);
  });

  it('does nothing after a manual pause or while one is in progress', async () => {
    const manual = player();
    await manual.togglePause();
    manual.onVisibilityChange();
    await settle();
    expect(manual.sessionService.pauseSession).toHaveBeenCalledTimes(1);
    expect(manual.sessionService.resumeSession).not.toHaveBeenCalled();
    expect(manual.engineState.isPaused).toBeTrue();

    const pending = player();
    pending.isPauseTransitionPending = true;
    pending.onVisibilityChange();
    await settle();
    expect(pending.engine.pause).not.toHaveBeenCalled();
    expect(pending.sessionService.pauseSession).not.toHaveBeenCalled();
  });

  it('pauses engines without a server pause only locally', async () => {
    const value = player('grid_interaction');
    value.onVisibilityChange();
    await settle();
    expect(value.engine.pause).toHaveBeenCalledTimes(1);
    expect(value.sessionService.pauseSession).not.toHaveBeenCalled();
  });

  it('clears a half-typed Takistoskop answer like the manual pause does', async () => {
    const value = player('text_stream');
    value.isTachistoscopeMode = () => true;
    value.tachistoscopeAnswer = '12';
    value.tachistoscopeFeedback = { isCorrect: true };
    value.onVisibilityChange();
    await settle();
    expect(value.sessionService.pauseSession).toHaveBeenCalledOnceWith('session');
    expect(value.tachistoscopeAnswer).toBe('');
    expect(value.tachistoscopeFeedback).toBeNull();
  });

  const untouched: Array<[string, (value: any) => void]> = [
    ['the timed paragraph test, which cannot be paused', value => value.engine.engineType = 'exam_simulation'],
    ['an assessment', value => value.isAssessmentMode = true],
    ['an engine that has already finished', value => value.engine.state.isCompleted = true],
    ['the result screen', value => { value.engineState = { isRunning: false, isPaused: false, isCompleted: true }; value.result = { score: 1 }; }],
    ['the start card', value => value.engineState = { isRunning: false, isPaused: false, isCompleted: false }],
    ['the question phase, which has no pause control', value => { value.engine.engineType = 'reading_comprehension'; value.exercisePhase = 'questions'; }],
    ['the answering phase of a paced reading exercise', value => { value.engine.engineType = 'regression_reduction'; value.engine.getPhase = () => 'answering'; }],
    ['the vocabulary cards, which have no pause control', value => value.engine.engineType = 'vocabulary_builder'],
    ['the server-clocked attention stream', value => value.engine.engineType = 'focus'],
    ['a preview without a session', value => { value.sessionId = null; value.engine = null; }]
  ];
  for (const [name, arrange] of untouched) {
    it(`leaves ${name} alone`, async () => {
      const value = player();
      arrange(value);
      const engine = value.engine;
      value.onVisibilityChange();
      value.onPageHide();
      await settle();
      if (engine) expect(engine.pause).not.toHaveBeenCalled();
      expect(value.sessionService.pauseSession).not.toHaveBeenCalled();
      visibility.and.returnValue('visible');
      value.onVisibilityChange();
      expect(value.showToast).not.toHaveBeenCalled();
    });
  }

  it('keeps the exercise paused on return and tells the student why', async () => {
    const value = player();
    value.onVisibilityChange();
    await settle();
    expect(value.showToast).not.toHaveBeenCalled();

    visibility.and.returnValue('visible');
    value.onVisibilityChange();
    await settle();

    expect(value.engineState.isPaused).toBeTrue();
    expect(value.engine.resume).not.toHaveBeenCalled();
    expect(value.sessionService.resumeSession).not.toHaveBeenCalled();
    expect(value.showToast).toHaveBeenCalledOnceWith('Ekrandan ayrıldığınız için egzersiz duraklatıldı.', 'info', 6000);

    value.onVisibilityChange();
    expect(value.showToast).toHaveBeenCalledTimes(1);

    await value.togglePause();
    expect(value.sessionService.resumeSession).toHaveBeenCalledOnceWith('session');
    expect(value.engineState.isPaused).toBeFalse();
  });

  it('says nothing on return when the student had paused manually', async () => {
    const value = player();
    await value.togglePause();
    value.onVisibilityChange();
    visibility.and.returnValue('visible');
    value.onVisibilityChange();
    expect(value.showToast).not.toHaveBeenCalled();
  });

  it('stays paused without an error when the pause cannot reach the server, and reports it before resuming', async () => {
    const value = player();
    const calls: string[] = [];
    value.sessionService.pauseSession.and.returnValue(throwError(() => new Error('offline')));
    value.onVisibilityChange();
    await settle();

    expect(value.engineState.isPaused).toBeTrue();
    expect(value.engine.stop).not.toHaveBeenCalled();
    expect(value.error).toBeNull();
    expect(value.isPauseTransitionPending).toBeFalse();

    value.sessionService.pauseSession.and.callFake(() => { calls.push('pause'); return of({}); });
    value.sessionService.resumeSession.and.callFake(() => { calls.push('resume'); return of({}); });
    await value.togglePause();

    expect(calls).toEqual(['pause', 'resume']);
    expect(value.engineState.isPaused).toBeFalse();

    await value.togglePause();
    await value.togglePause();
    expect(calls).toEqual(['pause', 'resume', 'pause', 'resume']);
  });
  it('reports a missed Takistoskop pause before resuming, so the round is presented again', async () => {
    const value = player('text_stream');
    const calls: string[] = [];
    value.isTachistoscopeMode = () => true;
    value.sessionService.pauseSession.and.returnValue(throwError(() => new Error('offline')));
    value.onPageHide();
    await settle();
    expect(value.engineState.isPaused).toBeTrue();
    expect(value.engine.stop).not.toHaveBeenCalled();

    value.sessionService.pauseSession.and.callFake(() => { calls.push('pause'); return of({}); });
    value.sessionService.resumeSession.and.callFake(() => { calls.push('resume'); return of({}); });
    value.engine.resume.and.callFake(() => { calls.push('engine'); value.engineState = { ...value.engineState, isPaused: false }; });
    await value.togglePause();

    // The server pause discards the open presentation; only then does the engine ask for the next stimulus.
    expect(calls).toEqual(['pause', 'resume', 'engine']);
  });

  it('does not stay in a pause transition when the pause request never settles', fakeAsync(() => {
    const value = player();
    value.sessionService.pauseSession.and.returnValue(NEVER);
    value.onVisibilityChange();
    flushMicrotasks();
    expect(value.isPauseTransitionPending).toBeTrue();
    expect(value.engineState.isPaused).toBeTrue();

    tick(8000);
    flushMicrotasks();

    expect(value.isPauseTransitionPending).toBeFalse();
    expect(value.engineState.isPaused).toBeTrue();
    expect(value.engine.stop).not.toHaveBeenCalled();
    expect(value.error).toBeNull();

    const calls: string[] = [];
    value.sessionService.pauseSession.and.callFake(() => { calls.push('pause'); return of({}); });
    value.sessionService.resumeSession.and.callFake(() => { calls.push('resume'); return of({}); });
    void value.togglePause();
    flushMicrotasks();
    expect(calls).toEqual(['pause', 'resume']);
    expect(value.engineState.isPaused).toBeFalse();
  }));

  it('does not stay in a pause transition when a pending exercise step never settles', fakeAsync(() => {
    const value = player();
    value.actionQueue = new Promise<void>(() => undefined);
    value.onVisibilityChange();
    flushMicrotasks();
    tick(8000);
    flushMicrotasks();
    expect(value.isPauseTransitionPending).toBeFalse();
    expect(value.engineState.isPaused).toBeTrue();
    expect(value.sessionService.pauseSession).not.toHaveBeenCalled();
  }));

  it('reconciles a page restored from the back-forward cache: paused, unblocked and explained', fakeAsync(() => {
    visibility.and.returnValue('visible');
    const value = player();
    value.sessionService.pauseSession.and.returnValue(NEVER);
    value.onPageHide();
    flushMicrotasks();
    expect(value.isPauseTransitionPending).toBeTrue();

    value.onPageShow({ persisted: true } as PageTransitionEvent);

    expect(value.isPauseTransitionPending).toBeFalse();
    expect(value.engineState.isPaused).toBeTrue();
    expect(value.showToast).toHaveBeenCalledOnceWith('Ekrandan ayrıldığınız için egzersiz duraklatıldı.', 'info', 6000);

    // The abandoned request must not disturb a later transition, and the toast is not repeated.
    value.isPauseTransitionPending = true;
    tick(8000);
    flushMicrotasks();
    expect(value.isPauseTransitionPending).toBeTrue();
    value.isPauseTransitionPending = false;
    value.onVisibilityChange();
    expect(value.showToast).toHaveBeenCalledTimes(1);

    const calls: string[] = [];
    value.sessionService.pauseSession.and.callFake(() => { calls.push('pause'); return of({}); });
    value.sessionService.resumeSession.and.callFake(() => { calls.push('resume'); return of({}); });
    void value.togglePause();
    flushMicrotasks();
    expect(calls).toEqual(['pause', 'resume']);
  }));

  it('ignores an ordinary page load and a restore without an automatic pause', async () => {
    visibility.and.returnValue('visible');
    const value = player();
    value.onPageShow({ persisted: false } as PageTransitionEvent);
    value.onPageShow({ persisted: true } as PageTransitionEvent);
    await settle();
    expect(value.showToast).not.toHaveBeenCalled();
    expect(value.engine.pause).not.toHaveBeenCalled();
    expect(value.engineState.isPaused).toBeFalse();

    // A manual pause that was in flight when the page was frozen is given up on in the same way.
    const manual = player();
    manual.isPauseTransitionPending = true;
    manual.engineState = { ...manual.engineState, isPaused: true };
    manual.onPageShow({ persisted: true } as PageTransitionEvent);
    expect(manual.isPauseTransitionPending).toBeFalse();
    expect(manual.showToast).not.toHaveBeenCalled();
    await manual.togglePause();
    expect(manual.sessionService.pauseSession).toHaveBeenCalledBefore(manual.sessionService.resumeSession);
    expect(manual.engineState.isPaused).toBeFalse();
  });
});
