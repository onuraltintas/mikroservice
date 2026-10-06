import { fakeAsync, tick } from '@angular/core/testing';
import { ExercisePlayerComponent } from './exercise-player.component';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';
import { Subject } from 'rxjs';

describe('regression player configuration', () => {
  it('does not normalize unmeasured preview answers into measured comprehension', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'regression_reduction' };
    player.questionAnswers = [];
    const result = { score: 0, accuracy: 0, details: { measurementStatus: 'NotMeasured',
      comprehensionScore: null, totalQuestions: 1, answers: [{ isCorrect: null }] } };
    expect(player.isMeasuredClientResult(result)).toBeFalse();
  });
  it('allows unmeasured preview answers and supports catalog question id aliases', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engineState = { isRunning: true, isPaused: false }; player.questionFeedback = null;
    player.engine = { getCurrentQuestion: () => ({ id: 'q1' }), getPhase: () => 'answering', handleInput: jasmine.createSpy('answer') };
    player.sessionId = 'preview-mode'; player.cdr = { detectChanges: () => undefined };
    player.showToast = jasmine.createSpy('toast');
    player.submitRegressionAnswer('A');
    expect(player.engine.handleInput).toHaveBeenCalledWith(jasmine.objectContaining({ previewOnly: true }));
  });
  it('leaves the question retryable on server rejection and hides assessment answer feedback', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    const question = { questionId: 'q1' };
    player.engineState = { isRunning: true, isPaused: false };
    player.engine = { getCurrentQuestion: () => question, getPhase: () => 'answering', handleInput: jasmine.createSpy('answer') };
    player.questionSubmissionPending = false; player.questionFeedback = null;
    player.questionAnswers = []; player.sessionId = 'session'; player.isAssessmentMode = true;
    player.cdr = { detectChanges: () => undefined }; player.showToast = jasmine.createSpy('toast');
    player.enqueueAction = (_action: any, apply: any) => Promise.resolve().then(() => apply({ isValid: false }));
    player.submitRegressionAnswer('A');
    await Promise.resolve(); await Promise.resolve(); await Promise.resolve();
    expect(player.questionSubmissionPending).toBeFalse();
    expect(player.engine.handleInput).not.toHaveBeenCalled();
    expect(player.questionFeedback).toBeNull();
    player.enqueueAction = (_action: any, apply: any) => Promise.resolve().then(() => apply({ isValid: true, isCorrect: true, correctAnswer: 'A', explanation: 'secret' }));
    player.submitRegressionAnswer('A');
    await Promise.resolve(); await Promise.resolve();
    expect(player.questionFeedback.isCorrect).toBeNull();
    expect(player.questionFeedback.correctAnswer).toBeUndefined();
    expect(player.questionFeedback.explanation).toBeUndefined();
  });
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
