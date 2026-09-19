import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { MotionPathEngine } from './motion-path.engine';

function callbacks(onComplete: (result: EngineResult) => void): EngineCallbacks {
  return {
    onStart: () => undefined,
    onPause: () => undefined,
    onResume: () => undefined,
    onComplete,
    onError: () => undefined,
    onStateChange: () => undefined,
    onStepComplete: () => undefined,
    onAction: () => undefined
  };
}

describe('MotionPathEngine', () => {
  it('accepts a saccade click with flattened engine configuration', () => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'saccade',
      Targets: [{ X: 20, Y: 50, Size: 30, Value: 'A' }],
      timing: { holdMs: 10000 }
    }, callbacks(value => result = value));

    engine.start();
    engine.handleInput({ type: 'click' });

    expect(result).toEqual(jasmine.objectContaining({ totalSteps: 1, completedSteps: 1 }));
  });

  it('reports actual peripheral accuracy instead of unconditional success', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { points: 1, peripheralCount: 1 },
      timing: { holdMs: 50 }
    }, callbacks(value => result = value));

    engine.start();
    tick(200);
    const expected = engine.getPeripheralChars()[0]?.char;
    const wrong = expected === 'A' ? 'B' : 'A';
    tick(50);
    engine.handleInput({ type: 'keypress', key: wrong });
    tick(1200);

    expect(result).toEqual(jasmine.objectContaining({ score: 0, accuracy: 0, errors: 1 }));
  }));

  it('preserves configured total steps after reset', () => {
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { points: 3, peripheralCount: 0 },
      timing: { holdMs: 100 }
    }, callbacks(() => undefined));

    engine.reset();

    expect(engine.state.totalSteps).toBe(3);
  });

  it('normalizes malformed and excessive legacy timing values', () => {
    const engine = new MotionPathEngine();

    expect(() => engine.initialize({
      mode: 'fixation',
      timing: 'invalid',
      content: 'invalid',
      movement: 'invalid',
      path: 'invalid'
    } as any, callbacks(() => undefined))).not.toThrow();
    expect(engine.getFixationDuration()).toBeGreaterThanOrEqual(50);
    expect(engine.state.totalSteps).toBeGreaterThan(0);
  });

  it('consumes at most one saccade target for rapid repeated input', () => {
    let actions = 0;
    const engine = new MotionPathEngine();
    const engineCallbacks = callbacks(() => undefined);
    engineCallbacks.onAction = () => actions++;
    engine.initialize({
      mode: 'saccade',
      Targets: [
        { X: 20, Y: 50, Size: 30, Value: 'A' },
        { X: 80, Y: 50, Size: 30, Value: 'B' }
      ],
      timing: { holdMs: 10000 }
    }, engineCallbacks);

    engine.start();
    engine.handleInput({ type: 'click' });
    engine.handleInput({ type: 'click' });

    expect(actions).toBe(1);
    expect(engine.state.currentStep).toBe(1);
  });

  it('does not count the same peripheral character twice', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { points: 1, peripheralCount: 2 },
      timing: { holdMs: 50 }
    }, callbacks(value => result = value));

    engine.start();
    tick(199);
    const first = engine.getPeripheralChars()[0].char;
    tick(1);
    engine.handleInput({ type: 'keypress', key: first });
    engine.handleInput({ type: 'keypress', key: first });
    tick(1200);

    expect(result?.accuracy).toBe(50);
    expect(engine.getFixationResults()[0].accuracy).toBe(50);
  }));

  it('ignores repeated start and completes only once', fakeAsync(() => {
    let starts = 0;
    let completions = 0;
    const engine = new MotionPathEngine();
    const engineCallbacks = callbacks(() => completions++);
    engineCallbacks.onStart = () => starts++;
    engine.initialize({
      mode: 'fixation',
      content: { points: 1, peripheralCount: 0 },
      timing: { holdMs: 50 }
    }, engineCallbacks);

    engine.start();
    engine.start();
    tick(250);

    expect(starts).toBe(1);
    expect(completions).toBe(1);
  }));

  it('does not mutate state from feedback after reset', fakeAsync(() => {
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { points: 1, peripheralCount: 1 },
      timing: { holdMs: 50 }
    }, callbacks(() => undefined));

    engine.start();
    tick(250);
    engine.handleInput({ type: 'keypress', key: 'A' });
    engine.reset();
    tick(1200);

    expect(engine.state.currentStep).toBe(0);
    expect(engine.state.isRunning).toBeFalse();
  }));

  it('completes point-based jumping tracking and resumes the jumping flow', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'tracking',
      path: { type: 'random_point' },
      content: { points: 3 },
      movement: { jumpIntervalMs: 50 }
    }, callbacks(value => result = value));

    engine.start();
    engine.pause();
    engine.resume();
    tick(120);

    expect(result?.completedSteps).toBe(3);
  }));

  it('keeps the configured wall-clock limit while awaiting peripheral input', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { peripheralCount: 1 },
      timing: { durationSeconds: 5, holdMs: 50 }
    }, callbacks(value => result = value));

    engine.start();
    tick(5100);

    expect(result).toEqual(jasmine.objectContaining({ accuracy: 0, score: 0, errors: 1 }));
  }));

  it('resumes a paused fixation transition before starting its hold timer', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { points: 1, peripheralCount: 0 },
      timing: { holdMs: 50 }
    }, callbacks(value => result = value));

    engine.start();
    engine.pause();
    engine.resume();
    tick(60);
    expect(result).toBeUndefined();
    tick(140);
    expect(result?.completedSteps).toBe(1);
  }));

  it('does not count a jumping step merely by resuming', fakeAsync(() => {
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'tracking',
      path: { type: 'random_point' },
      content: { points: 5 },
      movement: { jumpIntervalMs: 50 }
    }, callbacks(() => undefined));

    engine.start();
    expect(engine.state.currentStep).toBe(1);
    engine.pause();
    engine.resume();
    expect(engine.state.currentStep).toBe(1);
    tick(49);
    expect(engine.state.currentStep).toBe(1);
    tick(1);
    expect(engine.state.currentStep).toBe(2);
  }));

  it('suspends peripheral feedback progression while paused', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new MotionPathEngine();
    engine.initialize({
      mode: 'fixation',
      content: { points: 1, peripheralCount: 1 },
      timing: { holdMs: 50 }
    }, callbacks(value => result = value));

    engine.start();
    tick(199);
    const answer = engine.getPeripheralChars()[0].char;
    tick(1);
    engine.handleInput({ type: 'keypress', key: answer });
    engine.pause();
    tick(1200);
    expect(engine.state.currentStep).toBe(0);
    expect(result).toBeUndefined();
    engine.resume();
    tick(1200);
    expect(result?.completedSteps).toBe(1);
  }));

  it('normalizes PascalCase legacy motion configuration', () => {
    const engine = new MotionPathEngine();
    engine.initialize({
      Mode: 'tracking',
      Path: { Type: 'two_point_jump' },
      Target: { Type: 'arrow', Size: 'large' },
      Movement: { SpeedLevel: 3, JumpIntervalMs: 250, FixationTimeMs: 750 },
      Fixation: { Points: 2, PeripheralCount: 0, PointSize: 48 }
    } as any, callbacks(() => undefined));
    engine.reset();

    expect(engine.state.totalSteps).toBe(2);
    expect(engine.getTargetPosition().x).toBe(20);
    expect(engine.getTargetConfig()).toEqual(jasmine.objectContaining({ type: 'arrow', size: 'large' }));
    expect(engine.getFixationDuration()).toBe(750);
    expect(engine.getPointSize()).toBe(48);
  });

  it('normalizes arbitrary casing accepted by backend validation', () => {
    const engine = new MotionPathEngine();
    engine.initialize({
      ENGINECONFIG: {
        MODE: 'tracking',
        PATH: { TYPE: 'two_point_jump' },
        MOVEMENT: { FIXATIONTIMEMS: 600 },
        FIXATION: { POINTS: 4 }
      }
    } as any, callbacks(() => undefined));
    engine.reset();

    expect(engine.state.totalSteps).toBe(4);
    expect(engine.getTargetPosition().x).toBe(20);
    expect(engine.getFixationDuration()).toBe(600);
  });
});
