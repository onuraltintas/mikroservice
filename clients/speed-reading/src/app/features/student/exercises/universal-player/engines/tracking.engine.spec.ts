import { fakeAsync, tick } from '@angular/core/testing';
import { MotionPathEngine } from './motion-path.engine';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { applyCustomPreviewSettings } from '../custom-preview-settings';

describe('Tracking motion timing', () => {
  let result: EngineResult | undefined;
  const callbacks: EngineCallbacks = { onStart: () => undefined, onPause: () => undefined,
    onResume: () => undefined, onStateChange: () => undefined, onStepComplete: () => undefined,
    onAction: () => undefined, onError: () => undefined, onComplete: value => result = value };
  beforeEach(() => result = undefined);

  it('uses tracking for legacy EyeTracking configuration and cycle timing on horizontal paths', () => {
    spyOn(window, 'requestAnimationFrame').and.returnValue(1);
    let now = 1000; spyOn(Date, 'now').and.callFake(() => now);
    const engine = new MotionPathEngine();
    engine.initialize({ exerciseTypeName: 'EyeTracking', path: { type: 'horizontal' }, timing: { speedMs: 1000, durationSeconds: 60 } }, callbacks);
    expect(engine.getMode()).toBe('tracking');
    engine.start(); now += 250; (engine as any).animate();
    expect(engine.getTargetPosition().x).toBeCloseTo(50, 5); engine.destroy();
  });

  it('uses the chosen speed level when custom settings disable legacy cycle speed', () => {
    spyOn(window, 'requestAnimationFrame').and.returnValue(1);
    let now = 1000; spyOn(Date, 'now').and.callFake(() => now);
    const config = applyCustomPreviewSettings({ engineType: 'motion_path', mode: 'tracking', path: { type: 'circle' },
      timing: { speedMs: 1250, durationSeconds: 60 } }, { speedLevel: 4 }, { roles: ['Admin'], preview: true });
    const engine = new MotionPathEngine(); engine.initialize(config, callbacks); engine.start();
    now += 1000; (engine as any).animate();
    expect(engine.getTargetPosition().x).toBeCloseTo(50 + 35 * Math.cos(3.6), 5); engine.destroy();
  });

  it('uses ceiling consistently for millisecond duration and preserves remaining jump delay', fakeAsync(() => {
    const engine = new MotionPathEngine();
    engine.initialize({ mode: 'tracking', path: { type: 'two_point_jump' }, timing: { durationMs: 5100 }, movement: { jumpIntervalMs: 1000 } }, callbacks);
    engine.start(); tick(900); engine.pause(); tick(2000); engine.resume();
    tick(100); expect(engine.state.currentStep).toBe(2);
    tick(4100); expect(result).toBeUndefined();
    tick(930); expect(result).toBeDefined(); engine.destroy();
  }));

  it('keeps horizontal movement bounded after a long frame and restarts at a visible margin', () => {
    spyOn(window, 'requestAnimationFrame').and.returnValue(1);
    let now = 1000; spyOn(Date, 'now').and.callFake(() => now);
    const engine = new MotionPathEngine();
    engine.initialize({ mode: 'tracking', path: { type: 'horizontal' }, timing: { durationSeconds: 60 } }, callbacks);
    engine.start();
    expect(engine.getTargetPosition().x).toBeGreaterThanOrEqual(5);
    now += 3000; (engine as any).animate();
    expect(engine.getTargetPosition().x).toBeGreaterThanOrEqual(5);
    expect(engine.getTargetPosition().x).toBeLessThanOrEqual(95); engine.destroy();
  });

  it('moves the same distance for the same elapsed time at different frame rates', () => {
    spyOn(window, 'requestAnimationFrame').and.returnValue(1);
    let now = 1000; spyOn(Date, 'now').and.callFake(() => now);
    const position = (frames: number) => {
      now = 1000;
      const engine = new MotionPathEngine();
      engine.initialize({ mode: 'tracking', path: { type: 'circle' }, timing: { durationSeconds: 60 } }, callbacks);
      engine.start();
      for (let frame = 1; frame <= frames; frame++) { now = 1000 + frame * 1000 / frames; (engine as any).animate(); }
      const target = engine.getTargetPosition(); engine.destroy(); return target;
    };
    const slow = position(60), fast = position(144);
    expect(slow.x).toBeCloseTo(fast.x, 5); expect(slow.y).toBeCloseTo(fast.y, 5);
  });

  it('uses legacy speedMs as a complete circular cycle duration', () => {
    spyOn(window, 'requestAnimationFrame').and.returnValue(1);
    let now = 1000; spyOn(Date, 'now').and.callFake(() => now);
    const engine = new MotionPathEngine();
    engine.initialize({ mode: 'tracking', path: { type: 'circle' }, timing: { speedMs: 1000, durationSeconds: 60 } }, callbacks);
    engine.start(); now += 1000; (engine as any).animate();
    expect(engine.getTargetPosition().x).toBeCloseTo(85, 5);
    expect(engine.state.currentStep).toBe(1); engine.destroy();
  });

  it('does not invent tracking accuracy and resets the motion phase', fakeAsync(() => {
    const engine = new MotionPathEngine();
    engine.initialize({ mode: 'tracking', path: { type: 'two_point_jump' }, content: { points: 1 } }, callbacks);
    engine.start();
    expect(result?.accuracy).toBe(0); expect(result?.details.measurementStatus).toBe('NotMeasured');
    (engine as any).angle = 9; (engine as any).direction = -1;
    engine.reset(); expect((engine as any).angle).toBe(0); expect((engine as any).direction).toBe(1);
    tick(1); engine.destroy();
  }));
});
