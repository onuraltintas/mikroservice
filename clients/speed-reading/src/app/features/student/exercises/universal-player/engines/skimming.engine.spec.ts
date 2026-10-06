import { fakeAsync, tick } from '@angular/core/testing';
import { EngineFactory } from './engine-factory';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { ExercisePlayerComponent } from '../exercise-player.component';

describe('Skimming inspection', () => {
  let result: EngineResult | undefined;
  let errors: string[];
  const callbacks: EngineCallbacks = {
    onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
    onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined,
    onComplete: value => result = value, onError: value => errors.push(value)
  };
  beforeEach(() => { result = undefined; errors = []; });

  it('exposes inspection presentation and safely resets a completed run', fakeAsync(() => {
    const engine = EngineFactory.create('skimming')! as any;
    engine.initialize({ content: 'Bir kısa metin', readingTextTitle: 'Başlık',
      timing: { minReadingTimeMs: 0, maxReadingTimeMs: 1000 }, visuals: { fontSize: '28px' } }, callbacks);
    expect(engine.getTitle()).toBe('Başlık'); expect(engine.getWordCount()).toBe(3);
    expect(engine.getFontSize()).toBe('medium'); expect(engine.getFontSizePx()).toBe(28);
    expect(engine.getLineHeight()).toBe(1.8); expect(engine.getMaximumMs()).toBe(1000);
    engine.pause(); engine.resume(); expect(engine.canComplete()).toBeFalse();
    engine.start(); engine.start(); expect(engine.canComplete()).toBeTrue();
    engine.handleInput({ action: 'ignored' }); expect(result).toBeUndefined();
    engine.handleInput({ action: 'complete_reading' }); expect(result?.details.timedOut).toBeFalse();
    engine.start(); expect(engine.state.isRunning).toBeFalse();
    engine.reset(); expect(engine.state.isCompleted).toBeFalse();
    engine.start(); tick(100); engine.stop(); tick(1000); expect(engine.state.timeElapsed).toBe(100);
    engine.destroy();
  }));

  it('rejects inconsistent timing and handles empty presentation', () => {
    const engine = EngineFactory.create('skimming')! as any;
    engine.initialize({ content: '', timing: { minReadingTimeMs: 2000, maxReadingTimeMs: 1000 } }, callbacks);
    expect(engine.getWordCount()).toBe(0); engine.start(); engine.completeReading();
    expect(errors.length).toBe(1); expect(result).toBeUndefined(); engine.destroy();
  });

  it('uses an inspection engine rather than keyword search', () => {
    const engine = EngineFactory.create('skimming')!;
    engine.initialize({ content: { text: 'Metin' } }, callbacks);
    expect(engine.engineType).toBe('skimming');
    engine.destroy();
  });
  it('uses server text and timing rather than stale catalog defaults', fakeAsync(() => {
    const engine = EngineFactory.create('skimming')! as any;
    engine.initialize({ serverAuthoritative: true, content: 'Sunucu metni', readingTextTitle: 'Başlık',
      readingMinimumMs: 500, readingMaximumMs: 2000, skimmingProtocolVersion: 1,
      engineConfig: { content: { text: 'Yanlış' }, timing: { minReadingTimeMs: 0, maxReadingTimeMs: 100 }, targets: { words: ['Yanlış'] } }
    }, callbacks);
    expect(engine.getText()).toBe('Sunucu metni');
    engine.start(); tick(400); engine.completeReading(); expect(result).toBeUndefined();
    tick(100); engine.completeReading();
    expect(result?.details.wpm).toBeNull();
    expect(result?.details.inspectionTimeMs).toBe(500);
    engine.destroy();
  }));
  it('does not replace missing owned text with local example text', () => {
    const engine = EngineFactory.create('skimming')!;
    engine.initialize({ serverAuthoritative: true, engineConfig: { content: { text: 'Katalog' } } }, callbacks);
    engine.start(); expect(errors.length).toBe(1); expect(engine.state.isRunning).toBeFalse();
    engine.destroy();
  });
  it('accepts the player-normalized owned text object without falling back to catalog content', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    const normalized = player.normalizeSessionConfiguration({ engineConfig: { content: { text: 'Katalog' } } },
      { content: 'Doğrulanmış metin', skimmingProtocolVersion: 1, readingMinimumMs: 0, readingMaximumMs: 1000 });
    const engine = EngineFactory.create('skimming')! as any;
    engine.initialize({ ...normalized, ...normalized.engineConfig, serverAuthoritative: true }, callbacks);
    expect(engine.getText()).toBe('Doğrulanmış metin');
    engine.destroy();
  });
  it('keeps fetched preview text aligned with its questions rather than the catalog example', () => {
    const engine = EngineFactory.create('skimming')! as any;
    engine.initialize({ readingTextContent: 'Getirilen metin', content: 'Getirilen metin',
      engineConfig: { content: { text: 'Katalog' } } }, callbacks);
    expect(engine.getText()).toBe('Getirilen metin');
    engine.destroy();
  });
  it('excludes paused time and moves to questions when the inspection deadline expires', fakeAsync(() => {
    const engine = EngineFactory.create('skimming')! as any;
    engine.initialize({ content: { text: 'Metin' }, timing: { minReadingTimeMs: 100, maxReadingTimeMs: 1000 } }, callbacks);
    engine.start(); tick(400); engine.pause(); tick(2000); engine.completeReading(); expect(result).toBeUndefined();
    engine.resume(); tick(600);
    expect(result?.details.timedOut).toBeTrue(); expect(result?.totalTime).toBe(1000);
    engine.destroy();
  }));
});
