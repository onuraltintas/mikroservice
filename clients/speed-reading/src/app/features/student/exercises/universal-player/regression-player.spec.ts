import { fakeAsync, tick } from '@angular/core/testing';
import { ExercisePlayerComponent } from './exercise-player.component';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';
import { Subject } from 'rxjs';

describe('regression player configuration', () => {
  it('waits for server validation, blocks duplicate submissions and records verified answers', async () => {
    const response = new Subject<any>();
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    const question = { questionId: 'q1', correctAnswer: 'B' };
    player.engineState = { isRunning: true, isPaused: false };
    player.engine = { getCurrentQuestion: () => question, getPhase: () => 'answering',
      handleInput: jasmine.createSpy('answer') };
    player.questionSubmissionPending = false; player.questionFeedback = null;
    player.questionAnswers = []; player.sessionId = 'session';
    player.cdr = { detectChanges: () => undefined };
    player.enqueueAction = jasmine.createSpy('queue').and.callFake((_action: any, apply: any) =>
      new Promise<void>((resolve, reject) => response.subscribe({ next: value => {
        try { apply(value); resolve(); } catch (error) { reject(error); }
      } })));
    player.submitRegressionAnswer('A');
    player.submitRegressionAnswer('A');
    expect(player.engine.handleInput).not.toHaveBeenCalled();
    expect(player.enqueueAction).toHaveBeenCalledTimes(1);
    response.next({ isValid: true, isCorrect: true, correctAnswer: 'A' });
    await Promise.resolve();
    expect(player.engine.handleInput).toHaveBeenCalledWith(jasmine.objectContaining({ serverValidated: true, isCorrect: true }));
    expect(player.questionAnswers[0].isCorrect).toBeTrue();
  });
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
