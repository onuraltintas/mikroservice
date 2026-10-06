import { fakeAsync, tick } from '@angular/core/testing';
import { FocusEngine } from './focus.engine';

function callbacks() {
  return { onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
    onComplete: () => undefined, onError: () => undefined, onStateChange: () => undefined,
    onStepComplete: () => undefined, onAction: () => undefined };
}

describe('FocusEngine timing', () => {
  it('matches word casing like the server and permits retry after a rejected response', fakeAsync(() => {
    const engine = new FocusEngine(); const actions: any[] = [];
    engine.initialize({ Mode: 'word', SpeedMs: 1000, NLevel: 1, WordSequence: ['a', 'A', 'b'] } as any,
      { ...callbacks(), onAction: action => actions.push(action) });
    expect(engine.config.WordTargetIndices).toEqual([1]);
    engine.start(); tick(1000); engine.handleInput({ type: 'word_match' });
    engine.reconcileServerResponse({ action: 'word_match', index: 1 }, { isValid: false });
    engine.handleInput({ type: 'word_match' });
    expect(actions.filter(action => action.action === 'word_match').length).toBe(2); engine.destroy();
  }));
  it('routes legacy position matches only to position and bounds completed steps', fakeAsync(() => {
    const engine = new FocusEngine(); const actions: any[] = [];
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 2, 1] } as any,
      { ...callbacks(), onAction: action => actions.push(action) });
    engine.start(); engine.handleInput({ type: 'match' });
    expect(actions.filter(action => action.action === 'word_match').length).toBe(0);
    expect(actions.filter(action => action.action === 'position_match').length).toBe(1);
    tick(3000); expect(engine.getResult().completedSteps).toBe(3); engine.destroy();
  }));
  for (const configuration of [
    { Mode: 'unknown', PositionSequence: [1, 2, 1] },
    { Mode: 'dual', PositionSequence: [1, 2, 1], WordSequence: ['a'] },
    { Mode: 'position', GridSize: 3, PositionSequence: [1, 10, 1] },
    { Mode: 'word', WordSequence: ['a', '', 'a'] }
  ]) {
    it('rejects incompatible focus content before starting ' + JSON.stringify(configuration), () => {
      const engine = new FocusEngine(); let errors = 0;
      engine.initialize(configuration as any, { ...callbacks(), onError: () => errors++ });
      engine.start(); expect(errors).toBe(1); expect(engine.state.isRunning).toBeFalse(); engine.destroy();
    });
  }

  it('reads lowercase session aliases and resets stopped sessions before reinitialization', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ sessionData: { focusMode: 'word', focusNLevel: 2, focusSpeedMs: 1000, wordSequence: ['a', 'b', 'a'] } } as any, callbacks());
    expect(engine.mode).toBe('word'); expect(engine.nLevel).toBe(2);
    engine.start(); tick(3000);
    engine.initialize({ Mode: 'position', PositionSequence: [1, 2, 1] } as any, callbacks());
    expect(engine.state.isCompleted).toBeFalse(); expect(engine.hits).toBe(0);
    engine.reconcileServerResponse({ action: 'position_match', index: 0 }, { isValid: true, feedbackData: { hits: 5, misses: 0, falseAlarms: 0 } });
    expect(engine.hits).toBe(0); engine.destroy();
  }));

  it('does not accept a response before a repeated stimulus becomes visible', fakeAsync(() => {
    const engine = new FocusEngine(); const actions: any[] = [];
    engine.initialize({ Mode: 'position', PositionSequence: [1, 1, 2], SpeedMs: 1000 } as any, { ...callbacks(), onAction: action => actions.push(action) });
    engine.start(); tick(1000); engine.handleInput({ type: 'position_match' });
    expect(actions.some(action => action.action === 'position_match')).toBeFalse();
    engine.destroy();
  }));
  it('derives targets from the effective N-back rule and does not claim validated d-prime', () => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', NLevel: 2, PositionSequence: [1, 2, 1], PositionTargetIndices: [1] } as any, callbacks());
    expect(engine.config.PositionTargetIndices).toEqual([2]);
    expect(engine.getResult().details.dPrime).toBeUndefined();
    engine.destroy();
  });
  it('does not reveal a repeated stimulus while paused in its transition', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 1, 2] } as any, callbacks());
    engine.start(); tick(1000); tick(50); engine.pause(); tick(500);
    expect(engine.state.currentStep).toBe(0);
    engine.resume(); tick(100);
    expect(engine.state.currentStep).toBe(1);
    engine.destroy();
  }));
  it('ignores a second start while already running', fakeAsync(() => {
    const engine = new FocusEngine(); let starts = 0;
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 2, 3] } as any,
      { ...callbacks(), onStart: () => starts++ });
    engine.start(); engine.start();
    expect(starts).toBe(1);
    engine.destroy();
  }));

  it('reports the same accuracy in live state and final result', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', NLevel: 1, SpeedMs: 1000, PositionSequence: [1, 2, 2] } as any, callbacks());
    engine.start(); tick(1000);
    engine.handleInput({ type: 'position_match' });
    expect(engine.getResult().accuracy).toBe(engine.state.accuracy);
    engine.destroy();
  }));

  it('shows the first stimulus for exactly one configured interval', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 2, 3] } as any, callbacks());
    engine.start();
    expect(engine.currentPosition).toBe(1);
    tick(1000);
    expect(engine.currentPosition).toBe(2);
    engine.destroy();
  }));

  it('preserves the remaining assessment stimulus time across pauses', fakeAsync(() => {
    const engine = new FocusEngine(); const actions: any[] = [];
    engine.initialize({ Mode: 'position', SpeedMs: 1000, AssessmentMode: true, totalSteps: 3 } as any,
      { ...callbacks(), onAction: action => actions.push(action) });
    engine.start();
    engine.reconcileServerResponse({ action: 'focus_step' }, { isValid: true, feedbackData: { position: 1 } });
    tick(400); engine.pause(); tick(2000); engine.resume(); tick(600);
    expect(actions.filter(action => action.action === 'focus_step').length).toBe(2);
    engine.destroy();
  }));

  it('cancels delayed repeated-cell updates after stopping', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 1, 2] } as any, callbacks());
    engine.start(); tick(2000); engine.stop();
    const step = engine.state.currentStep; tick(200);
    expect(engine.state.currentStep).toBe(step);
    engine.destroy();
  }));
});
