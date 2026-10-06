import { fakeAsync, tick } from '@angular/core/testing';
import { ExercisePlayerComponent } from './exercise-player.component';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';

describe('regression player configuration', () => {
  it('uses effective engine pacing and correctly marks a partial last chunk', fakeAsync(() => {
    const engine = new RegressionReductionEngine();
    engine.initialize({ readingTextContent: 'bir iki üç', wordDelayMs: 100, chunkSize: 2 } as any, {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: () => undefined, onError: () => undefined, onStateChange: () => undefined,
      onStepComplete: () => undefined, onAction: () => undefined
    });
    const player = Object.create(ExercisePlayerComponent.prototype) as ExercisePlayerComponent;
    player.engine = engine;
    engine.start();
    tick(200);
    expect(player.getRegressionWpm()).toBe(600);
    expect(player.getRegressionChunkSize()).toBe(2);
    expect(player.isWordInActiveChunk(1)).toBeFalse();
    expect(player.isWordInActiveChunk(2)).toBeTrue();
    expect(player.isWordInTrailingMask(1)).toBeTrue();
    engine.destroy();
  }));
});
