import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { TextStreamEngine } from './text-stream.engine';

describe('Tachistoscope runtime', () => {
  it('prefers unused equally sized preview words', () => {
    const { engine } = create({ Words: ['bir', 'iki', 'ses'], adaptive: { enabled: false } });
    const runtime = engine as any;
    runtime.currentStimulus = 'iki';
    runtime.trials = [{ stimulus: 'bir' }, { stimulus: 'iki' }];
    spyOn(Math, 'random').and.returnValue(0);
    expect(runtime.selectPreviewStimulus()).toBe('ses');
    engine.destroy();
  });
  function create(config: Record<string, unknown>) {
    const actions: any[] = [];
    let result: EngineResult | undefined;
    const engine = new TextStreamEngine();
    const callbacks: EngineCallbacks = {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined,
      onAction: action => actions.push(action), onError: () => undefined,
      onComplete: value => result = value
    };
    engine.initialize({ mode: 'flash', visuals: { showFixation: false },
      adaptive: { enabled: false }, ...config }, callbacks);
    return { engine, actions, result: () => result };
  }

  it('uses the configured gap and count without replaying a paused stimulus', fakeAsync(() => {
    const { engine } = create({ Words: ['bir', 'iki', 'üç', 'dört'],
      timing: { durationMs: 100, intervalMs: 75 }, content: { count: 2 } });
    expect(engine.state.totalSteps).toBe(2);
    engine.start();
    tick(100);
    engine.handleInput({ answer: engine.getCurrentStimulus() });
    tick(599);
    expect(engine.isShowingContent()).toBeFalse();
    tick(1);
    expect(engine.isShowingContent()).toBeTrue();
    engine.pause();
    expect(engine.isShowingContent()).toBeFalse();
    engine.handleInput({ answer: engine.getCurrentStimulus() });
    expect(engine.state.currentStep).toBe(1);
    engine.destroy();
  }));

  it('preserves the RSVP automatic playback score', fakeAsync(() => {
    const { engine, result } = create({ mode: 'rsvp', Words: ['bir'], DisplayDurationMs: 50 });
    engine.start(); tick(550);
    expect(result()?.accuracy).toBe(100);
    expect(result()?.score).toBe(100);
    engine.destroy();
  }));

  it('does not submit an answer while paused waiting for input', fakeAsync(() => {
    const { engine } = create({ Words: ['bir'], DisplayDurationMs: 50 });
    engine.start(); tick(50); engine.pause();
    engine.handleInput({ answer: 'bir' });
    expect(engine.state.currentStep).toBe(0);
    engine.destroy();
  }));

  it('generates letters rather than words for a letter exercise', () => {
    const { engine } = create({ content: { type: 'letter', count: 2 }, difficultyLevel: 1 });
    engine.start();
    expect(engine.getCurrentStimulus()).toMatch(/^[ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ]$/);
    engine.destroy();
  });

  it('increases target word length after consecutive preview successes', fakeAsync(() => {
    const { engine } = create({ Words: ['bir', 'iki', 'masa', 'kapı', 'kalem'],
      DisplayDurationMs: 500, content: { count: 3 }, adaptive: { enabled: true } });
    engine.start();
    for (let round = 0; round < 2; round++) {
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer: engine.getCurrentStimulus() });
      tick(600);
    }
    expect(engine.getCurrentDuration()).toBe(450);
    expect(engine.getCurrentStimulus().length).toBe(4);
    engine.destroy();
  }));

  it('restores verified counters when an authoritative session is resumed', fakeAsync(() => {
    const { engine, actions, result } = create({ serverAuthoritative: true,
      tachistoscope: { count: 3, round: 2, correctCount: 1, incorrectCount: 1, displayDurationMs: 50 } });
    engine.start();
    expect(engine.state.currentStep).toBe(2);
    expect(engine.getCorrectCount()).toBe(1);
    expect(engine.state.errors).toBe(1);
    engine.reconcileServerResponse(actions[0], { isValid: true,
      feedbackData: { round: 2, stimulus: 'bir', displayDurationMs: 50 } });
    tick(50); engine.handleInput({ answer: 'bir' });
    engine.reconcileServerResponse(actions[1], { isValid: true, isCorrect: true,
      feedbackData: { round: 3 } });
    expect(engine.getCorrectCount()).toBe(2);
    tick(600);
    expect(result()?.accuracy).toBe(67);
    expect(result()?.errors).toBe(1);
    engine.destroy();
  }));

  it('uses explicitly configured custom numbers without generating replacements', () => {
    const { engine } = create({ content: { type: 'number', source: 'custom', items: ['2468'], count: 1 } });
    engine.start();
    expect(engine.getCurrentStimulus()).toBe('2468');
    engine.destroy();
  });

  it('waits for server validation and never reports a reading WPM', fakeAsync(() => {
    const { engine, actions, result } = create({ serverAuthoritative: true,
      tachistoscope: { count: 1, round: 0, displayDurationMs: 50 } });
    engine.start();
    expect(actions[0]?.action).toBe('tachistoscope_present');
    engine.reconcileServerResponse(actions[0], { isValid: true,
      feedbackData: { round: 0, stimulus: 'İZ', displayDurationMs: 50, targetLength: 2 } });
    tick(50);
    engine.handleInput({ answer: 'iz' });
    expect(engine.state.currentStep).toBe(0);
    const answer = actions[1];
    expect(answer.action).toBe('tachistoscope_answer');
    engine.reconcileServerResponse(answer, { isValid: true, isCorrect: true, isCompleted: true,
      feedbackData: { round: 1, displayDurationMs: 50, targetLength: 2,
        trial: { stimulus: 'İZ', userAnswer: 'iz', isCorrect: true, responseTimeMs: 100, displayDurationMs: 50 } } });
    tick(600);
    expect(result()?.accuracy).toBe(100);
    expect(result()?.details.wpm).toBeNull();
    engine.destroy();
  }));

  it('does not reconstruct withheld assessment correctness', fakeAsync(() => {
    const { engine, actions } = create({ serverAuthoritative: true, isAssessmentMode: true,
      tachistoscope: { count: 1, round: 0, displayDurationMs: 50 } });
    engine.start();
    engine.reconcileServerResponse(actions[0], { isValid: true,
      feedbackData: { round: 0, stimulus: 'bir', displayDurationMs: 50 } });
    tick(50); engine.handleInput({ answer: 'bir' });
    engine.reconcileServerResponse(actions[1], { isValid: true, isCorrect: null,
      feedbackData: { round: 1 } });
    expect(engine.getLastTrialResult()).toBeNull();
    expect(engine.state.currentStep).toBe(1);
    engine.destroy();
  }));
});
