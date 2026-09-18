import { fakeAsync, tick } from '@angular/core/testing';
import { VisualExpansionEngine } from './visual-expansion.engine';

describe('VisualExpansionEngine server protocol', () => {
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
    expect(Math.abs(startPositions[0] - 50)).toBeLessThan(10);
    expect(Math.abs(startPositions[1] - 50)).toBeLessThan(10);

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
});
