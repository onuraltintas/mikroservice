import { fakeAsync, tick } from '@angular/core/testing';
import { TextFadeEngine } from './text-fade.engine';
import { EngineCallbacks } from './base-engine.interface';

describe('Text Fade real-content timing', () => {
  function create(config: any = {}) {
    const complete = jasmine.createSpy('complete');
    const callbacks: EngineCallbacks = {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: complete, onError: () => undefined, onStateChange: () => undefined,
      onStepComplete: () => undefined, onAction: () => undefined
    };
    const engine = new TextFadeEngine();
    engine.initialize({ readingTextContent: 'Bir iki üç dört.', fading: { speedWpm: 60, lagMs: 0 }, ...config }, callbacks);
    return { engine, complete };
  }

  it('excludes pauses during both countdown and fading and completes only once', fakeAsync(() => {
    const { engine, complete } = create({ fading: { speedWpm: 60, lagMs: 2000 } });
    engine.start(); engine.start();
    tick(1000);
    engine.pause(); engine.pause();
    tick(3000);
    expect(engine.getFadedIndex()).toBe(-1);
    engine.resume(); engine.resume();
    tick(1100);
    expect(engine.state.countdown).toBe(0);
    engine.pause(); tick(3000); engine.resume();
    tick(4000);
    expect(complete).toHaveBeenCalledTimes(1);
    const result = complete.calls.mostRecent().args[0];
    expect(result.totalTime).toBe(6000);
    expect(result.details.wpm).toBeNull();
    expect(result.details.completionPercent).toBe(100);
    engine.start(); tick(1000);
    expect(complete).toHaveBeenCalledTimes(1);
    engine.destroy();
  }));

  it('times out without claiming the whole text was displayed', fakeAsync(() => {
    const { engine, complete } = create({ timing: { timeLimitSec: 1 } });
    engine.start(); tick(1200);
    const result = complete.calls.mostRecent().args[0];
    expect(result.details.timedOut).toBeTrue();
    expect(result.details.completionPercent).toBeLessThan(100);
    engine.destroy();
  }));

  it('reset clears progress and allows a fresh run with the same real content', fakeAsync(() => {
    const { engine, complete } = create();
    engine.start(); tick(1100); engine.stop(); tick(1000);
    expect(engine.state.currentStep).toBe(1);
    engine.reset();
    expect(engine.getActiveIndex()).toBe(0);
    expect(engine.getFadedIndex()).toBe(-1);
    engine.start(); tick(4100);
    expect(complete).toHaveBeenCalledTimes(1);
    engine.destroy();
  }));

  for (const [font, css] of [['small','16px'], ['medium','20px'], ['large','24px'],
    ['14px','14px'], ['48px','48px'], ['99px','48px'], ['unknown','20px']]) {
    it(`renders bounded ${font} typography`, () => {
      const { engine } = create({ visuals: { fontSize: font } });
      expect(engine.getFontSizeCss()).toBe(css);
      engine.destroy();
    });
  }

  it('keeps nested legacy target pace and zero lag without a sample fallback', () => {
    const { engine } = create({ fading: undefined, engineConfig: { targetWpm: 300, lagMs: 0,
      visuals: { fontSize: 0 } } });
    expect(engine.getWpm()).toBe(300);
    expect(engine.getWords()).toEqual(['Bir', 'iki', 'üç', 'dört.']);
    expect(engine.getFontSizeCss()).toBe('20px');
    engine.destroy();
  });
});
