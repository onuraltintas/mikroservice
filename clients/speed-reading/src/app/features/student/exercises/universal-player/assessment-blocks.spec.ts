import { fakeAsync, tick } from '@angular/core/testing';
import { TextStreamEngine } from './engines/text-stream.engine';
import { VisualExpansionEngine } from './engines/visual-expansion.engine';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('fixed assessment blocks', () => {
  it('previews unique three-digit numbers in fixed blocks and excludes practice errors', fakeAsync(() => {
    const engine = new TextStreamEngine();
    let result: any;
    engine.initialize({ mode: 'tachistoscope', content: { type: 'number', count: 21, length: 3 },
      timing: { durationMs: 1000, intervalMs: 750 }, assessmentProtocol: {
        version: 2, practiceRounds: 3, roundsPerBlock: 6, blockValues: [1000, 750, 500]
      }, adaptive: { enabled: false } } as any, { onStart() {}, onPause() {}, onResume() {}, onError() {},
        onStateChange() {}, onStepComplete() {}, onAction() {}, onComplete: value => result = value });
    engine.start();
    const seen = new Set<string>();
    for (let round = 0; round < 21; round++) {
      tick(300);
      expect(engine.getCurrentDuration()).toBe([1000, 750, 500][Math.max(0, Math.floor((round - 3) / 6))]);
      const stimulus = engine.getCurrentStimulus();
      expect(stimulus.length).toBe(3);
      expect(seen.has(stimulus)).toBeFalse(); seen.add(stimulus);
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer: round < 3 ? '?' : stimulus });
      tick(750);
    }
    expect(result.accuracy).toBe(100); expect(result.errors).toBe(0);
    engine.destroy();
  }));
  for (const perBlock of [6, 8, 10]) {
    it(`previews ${perBlock * 3} scored visual rounds at 200ms without adapting`, fakeAsync(() => {
      const engine = new VisualExpansionEngine();
      let result: any;
      engine.initialize({ rounds: 3 + perBlock * 3, startDegrees: 8, targetDegrees: 18,
        timing: { durationMs: 200, intervalMs: 1 }, assessmentProtocol: {
          version: 2, practiceRounds: 3, roundsPerBlock: perBlock, blockValues: [8, 13, 18]
        } } as any, { onStart() {}, onPause() {}, onResume() {}, onError() {},
          onStateChange() {}, onStepComplete() {}, onAction() {}, onComplete: value => result = value });
      engine.start();
      for (let round = 0; round < 3 + perBlock * 3; round++) {
        tick(1);
        expect(engine.currentDegrees).toBe([8, 13, 18][Math.max(0, Math.floor((round - 3) / perBlock))]);
        expect(engine.getCurrentDisplayDurationMs()).toBe(200);
        const answers = engine.getCurrentStimuli().map(item => item.content);
        tick(200);
        engine.handleInput({ answers: round < 3 ? ['?', '?'] : answers });
      }
      expect(result.accuracy).toBe(100);
      expect(result.errors).toBe(0);
      engine.destroy();
    }));
  }

  it('permits assessment text review without enabling learning feedback', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.isAssessmentMode = true;
    player.engine = { engineType: 'reading_comprehension' };
    player.backendSessionConfig = { assessmentTextReviewAllowed: true, comprehensionMode: 'evaluation' };
    expect(player.canReviewComprehensionText()).toBeTrue();
    expect(player.isComprehensionLearning()).toBeFalse();
    expect(player.getQuestionTimeLimit({ targetTimeSeconds: 60 })).toBe(0);
    player.backendSessionConfig.assessmentTextReviewAllowed = false;
    expect(player.canReviewComprehensionText()).toBeFalse();
    expect(player.getQuestionTimeLimit({ targetTimeSeconds: 60 })).toBe(60);
  });

  it('restores the server visual round instead of restarting practice', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    const actions: any[] = [];
    engine.initialize({ serverAuthoritative: true, isAssessmentMode: true, visualExpansionRound: 9,
      rounds: 21, timing: { durationMs: 200, intervalMs: 1 }, assessmentProtocol: {
        version: 2, practiceRounds: 3, roundsPerBlock: 6, blockValues: [8, 12, 16]
      } } as any, { onStart() {}, onPause() {}, onResume() {}, onError() {}, onComplete() {},
        onStateChange() {}, onStepComplete() {}, onAction: action => actions.push(action) });
    engine.start(); tick(1);
    expect(engine.state.currentStep).toBe(9);
    expect(actions[0].action).toBe('visual_expansion_present');
    engine.reconcileServerResponse(actions[0], { isValid: true, feedbackData: {
      stimuli: ['A', 'B'], degrees: 12, displayDurationMs: 200, isPractice: false
    } });
    tick(200); engine.handleInput({ answers: ['A', 'B'] });
    engine.reconcileServerResponse(actions[1], { isValid: true, nextStep: 10, isCorrect: null,
      feedbackData: { degrees: 12, displayDurationMs: 200 } });
    expect(engine.state.currentStep).toBe(10);
    expect(engine.state.errors).toBe(0);
    engine.destroy();
  }));
});
