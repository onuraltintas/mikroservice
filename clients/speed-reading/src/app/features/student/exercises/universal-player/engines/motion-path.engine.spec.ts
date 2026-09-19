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
});
