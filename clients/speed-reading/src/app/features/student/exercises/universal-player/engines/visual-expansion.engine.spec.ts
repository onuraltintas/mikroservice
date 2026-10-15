import { fakeAsync, tick } from '@angular/core/testing';
import { VisualExpansionEngine } from './visual-expansion.engine';
import { visualAngleToAxisOffsetPercent, visualAngleToOffsetPercent } from './visual-expansion-position';

describe('VisualExpansionEngine server protocol', () => {
  it('increases distance after each correct answer and holds it after a wrong answer', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    engine.initialize({
      rounds: 3,
      startDegrees: 4,
      targetDegrees: 30,
      expansion: { level: 1, pattern: 'horizontal', stimulusType: 'letter', symmetry: true },
      timing: { durationMs: 400, intervalMs: 1 },
      visuals: { centerPoint: 'cross', stimulusSize: '2rem' }
    } as any, {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: () => undefined,
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: () => undefined
    });

    engine.start();
    tick(1);
    const answers = engine.getCurrentStimuli().map(item => item.content);
    tick(400);
    engine.handleInput({ answers });

    expect(engine.currentDegrees).toBe(5);
    expect(engine.getCurrentDisplayDurationMs()).toBe(400);

    tick(1);
    tick(400);
    engine.handleInput({ answers: ['?', '?'] });

    expect(engine.currentDegrees).toBe(5);
    expect(engine.getCurrentDisplayDurationMs()).toBe(400);
    expect(engine.state.errors).toBe(1);
    tick(1);
    const nextAnswers = engine.getCurrentStimuli().map(item => item.content);
    tick(400);
    engine.handleInput({ answers: nextAnswers });
    expect(engine.currentDegrees).toBe(6);
    expect(engine.state.accuracy).toBe(67);
    engine.destroy();
  }));

  it('expands before accelerating, avoids consecutive repeats and reports only presented targets', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    let result: any;
    engine.initialize({ rounds: 5, startDegrees: 8, targetDegrees: 10,
      timing: { durationMs: 400, intervalMs: 1 } } as any, {
      onStart() {}, onPause() {}, onResume() {}, onComplete: value => result = value,
      onError() {}, onStateChange() {}, onStepComplete() {}, onAction() {}
    });
    engine.start();
    let previous: string[] = [];
    for (let round = 0; round < 5; round++) {
      tick(1);
      expect(engine.currentDegrees).toBe(Math.min(10, 8 + round));
      expect(engine.getCurrentDisplayDurationMs()).toBe(400);
      const answers = engine.getCurrentStimuli().map(item => item.content);
      expect(answers.some(value => previous.includes(value))).toBeFalse();
      previous = answers;
      tick(engine.getCurrentDisplayDurationMs());
      engine.handleInput({ answers });
    }
    expect(result.details.targetSummary).toEqual({ targetDistance: 10, targetReached: true,
      targetRounds: 3, targetCorrectRounds: 3, targetAccuracy: 100, shortestCorrectDisplayDurationMs: 400 });
    engine.destroy();
  }));

  it('does not claim a target reached by the final answer but never displayed', fakeAsync(() => {
    const engine = new VisualExpansionEngine(); let result: any;
    engine.initialize({ rounds: 2, startDegrees: 8, targetDegrees: 10,
      timing: { durationMs: 100, intervalMs: 1 } } as any, {
      onStart() {}, onPause() {}, onResume() {}, onComplete: value => result = value,
      onError() {}, onStateChange() {}, onStepComplete() {}, onAction() {}
    });
    engine.start();
    for (let round = 0; round < 2; round++) {
      tick(1); const answers = engine.getCurrentStimuli().map(item => item.content);
      tick(100); engine.handleInput({ answers });
    }
    expect(result.details.targetSummary.targetReached).toBeFalse();
    expect(result.details.targetSummary.targetAccuracy).toBeNull();
    expect(result.details.maxDegreesReached).toBe(9);
    engine.destroy();
  }));

  it('requests a server stimulus and submits answers without scoring locally', fakeAsync(() => {
    const actions: any[] = [];
    const engine = new VisualExpansionEngine();
    engine.initialize({
      serverAuthoritative: true,
      rounds: 1,
      expansion: { level: 1, pattern: 'horizontal', stimulusType: 'letter', symmetry: true },
      timing: { durationMs: 250, intervalMs: 10 },
      visuals: { centerPoint: 'cross', stimulusSize: '2rem' }
    }, {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: () => undefined,
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: action => actions.push(action)
    });

    engine.start();
    tick(10);
    expect(actions.at(-1)?.action).toBe('visual_expansion_present');

    engine.reconcileServerResponse(actions.at(-1), {
      isValid: true,
      feedbackData: { round: 0, stimuli: ['A', '7'], displayDurationMs: 250 }
    });
    tick(250);
    engine.handleInput({ answers: ['A', '7'] });

    expect(actions.at(-1)).toEqual(jasmine.objectContaining({
      action: 'visual_expansion_answer',
      answers: ['A', '7']
    }));
    expect(engine.state.currentStep).toBe(0);

    // Assessment mode deliberately returns a null correctness flag so the
    // answer key is not exposed. The client should still show the submitted
    // answer as correct using the already displayed stimuli.
    engine.reconcileServerResponse(actions.at(-1), {
      isValid: true,
      isCorrect: null
    });
    expect(engine.state.currentStep).toBe(1);
    expect(engine.state.accuracy).toBe(100);
    expect(engine.state.errors).toBe(0);
  }));

  it('uses the server-owned angle progression from the center toward the edges', fakeAsync(() => {
    const actions: any[] = [];
    const engine = new VisualExpansionEngine();
    engine.initialize({
      serverAuthoritative: true,
      rounds: 3,
      VisualExpansionStartDegrees: 4,
      VisualExpansionTargetDegrees: 40,
      expansion: { level: 1, pattern: 'horizontal', stimulusType: 'letter', symmetry: true },
      timing: { durationMs: 10, intervalMs: 1 },
      visuals: { centerPoint: 'cross', stimulusSize: '2rem' }
    } as any, {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: () => undefined,
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: action => actions.push(action)
    });

    engine.start();
    tick(1);
    const present = actions.at(-1);
    engine.reconcileServerResponse(present, {
      isValid: true,
      feedbackData: { round: 0, degrees: 4, stimuli: ['A', 'B'], displayDurationMs: 10 }
    });
    const startPositions = engine.getCurrentStimuli().map(item => item.x);
    const expectedStartOffset = visualAngleToOffsetPercent(4, window.innerWidth);
    expect(Math.abs(startPositions[0] - 50)).toBeCloseTo(expectedStartOffset, 5);
    expect(Math.abs(startPositions[1] - 50)).toBeCloseTo(expectedStartOffset, 5);

    tick(10);
    engine.handleInput({ answers: ['A', 'B'] });
    engine.reconcileServerResponse(actions.at(-1), { isValid: true, isCorrect: null });
    tick(1);
    engine.reconcileServerResponse(actions.at(-1), {
      isValid: true,
      feedbackData: { round: 1, degrees: 22, stimuli: ['C', 'D'], displayDurationMs: 10 }
    });
    const middlePositions = engine.getCurrentStimuli().map(item => item.x);
    expect(Math.abs(middlePositions[0] - 50)).toBeGreaterThan(Math.abs(startPositions[0] - 50));
    expect(Math.abs(middlePositions[1] - 50)).toBeGreaterThan(Math.abs(startPositions[1] - 50));
  }));

  it('keeps angle offsets symmetric, monotonic and inside the safe viewport area', () => {
    const startOffset = visualAngleToOffsetPercent(4, 1280);
    const middleOffset = visualAngleToOffsetPercent(22, 1280);
    const maximumOffset = visualAngleToOffsetPercent(60, 1280);

    expect(startOffset).toBeGreaterThan(0);
    expect(middleOffset).toBeGreaterThan(startOffset);
    expect(maximumOffset).toBe(45);
  });

  it('starts a configured sixteen-degree exercise near the centre instead of at the viewport edge', () => {
    expect(visualAngleToOffsetPercent(16, 950)).toBeLessThan(20);
  });

  it('uses the exercise container dimensions and preserves radial distance', () => {
    const horizontal = visualAngleToAxisOffsetPercent(12, 950, false);
    const radial = visualAngleToAxisOffsetPercent(12, 950, true);

    expect(radial).toBeCloseTo(horizontal / Math.SQRT2, 8);
    expect(visualAngleToOffsetPercent(12, 950))
      .toBe(visualAngleToOffsetPercent(12, 1440));
  });
});
