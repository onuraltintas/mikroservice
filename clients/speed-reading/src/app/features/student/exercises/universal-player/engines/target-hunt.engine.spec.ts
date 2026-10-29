import { TargetHuntEngine } from './target-hunt.engine';
import { EngineFactory } from './engine-factory';

function callbacks(overrides: Record<string, any> = {}) {
  return {
    onStart: () => undefined,
    onPause: () => undefined,
    onResume: () => undefined,
    onComplete: () => undefined,
    onError: () => undefined,
    onStateChange: () => undefined,
    onStepComplete: () => undefined,
    onAction: () => undefined,
    ...overrides
  };
}

const items = ['d', 'b', 'p', 'b', 'd', 'p', 'b', 'd', 'p', 'd', 'p', 'd'];
const serverConfig = {
  serverAuthoritative: true,
  engineConfig: { target: 'b', distractors: ['d', 'p'], rows: 3, columns: 4, targetCount: 3, timeLimit: 60 },
  sessionData: { targetHuntItems: items, targetHuntTarget: 'b', targetHuntColumns: 4, totalSteps: 3, timeLimitSeconds: 60 }
};

// Decision 29.10.2026: "Hedef Avı" replaces N-back in programs. The student taps every target among look-alikes.
describe('TargetHuntEngine', () => {
  it('is created by the engine factory for target_hunt', () => {
    expect(EngineFactory.create('target_hunt' as any) instanceof TargetHuntEngine).toBeTrue();
  });

  it('shows the server layout and sends each tap to the server once', () => {
    const actions: any[] = []; const engine = new TargetHuntEngine();
    engine.initialize(serverConfig, callbacks({ onAction: (action: any) => actions.push(action) }));
    expect(engine.getItems()).toEqual(items); expect(engine.getTarget()).toBe('b'); expect(engine.getColumns()).toBe(4);
    expect(engine.getTargetCount()).toBe(3);

    engine.start();
    expect(actions[0].action).toBe('target_hunt_start');
    engine.handleInput({ type: 'tap', index: 0 });
    expect(actions.length).toBe(1);
    engine.reconcileServerResponse(actions[0], { isValid: true, feedbackData: { hits: [], falseTaps: [], timeElapsed: 0 } });

    engine.handleInput({ type: 'tap', index: 0 });
    engine.handleInput({ type: 'tap', index: 1 });
    expect(actions.length).toBe(2);
    expect(actions[1]).toEqual(jasmine.objectContaining({ action: 'target_hunt_tap', index: 0 }));
    engine.reconcileServerResponse(actions[1], { isValid: true, isCorrect: false,
      feedbackData: { hits: [], falseTaps: [0], timeElapsed: 1500 } });
    expect(engine.isFalseTap(0)).toBeTrue(); expect(engine.getFalseTapCount()).toBe(1);
    expect(engine.getLastFeedback()).toEqual({ index: 0, correct: false });

    engine.handleInput({ type: 'tap', index: 0 });
    expect(actions.length).toBe(2);
    engine.handleInput({ type: 'tap', index: 1 });
    engine.reconcileServerResponse(actions[2], { isValid: true, isCorrect: true,
      feedbackData: { hits: [1], falseTaps: [0], timeElapsed: 2500 } });
    expect(engine.isHit(1)).toBeTrue(); expect(engine.getFoundCount()).toBe(1);
    expect(engine.getLastFeedback()).toEqual({ index: 1, correct: true });
    engine.destroy();
  });

  it('finishes with the server score when the round is complete', () => {
    const actions: any[] = []; let result: any; const engine = new TargetHuntEngine();
    engine.initialize(serverConfig, callbacks({ onAction: (action: any) => actions.push(action), onComplete: (value: any) => result = value }));
    engine.start();
    engine.reconcileServerResponse(actions[0], { isValid: true, feedbackData: { hits: [], falseTaps: [], timeElapsed: 0 } });
    engine.forceComplete();
    expect(actions[1].action).toBe('target_hunt_finish');
    engine.reconcileServerResponse(actions[1], { isValid: true, isCompleted: true,
      feedbackData: { hits: [1, 3], falseTaps: [0], timeElapsed: 30000, score: 61.67, accuracy: 50 } });
    expect(result.score).toBe(61.67); expect(result.details.found).toBe(2); expect(result.details.missed).toBe(1);
    expect(result.details.falseTaps).toBe(1); expect(engine.state.isCompleted).toBeTrue();
  });

  it('refuses a layout without targets instead of starting an impossible round', () => {
    let error = ''; const engine = new TargetHuntEngine();
    engine.initialize({ ...serverConfig, sessionData: { ...serverConfig.sessionData, targetHuntItems: ['d', 'p', 'd'] } },
      callbacks({ onError: (message: string) => error = message }));
    engine.start();
    expect(engine.state.isRunning).toBeFalse(); expect(error).toContain('Hedef');
  });

  it('builds its own layout in preview without a server session', () => {
    let result: any; const engine = new TargetHuntEngine();
    engine.initialize({ engineConfig: serverConfig.engineConfig }, callbacks({ onComplete: (value: any) => result = value }));
    const layout = engine.getItems();
    expect(layout.length).toBe(12); expect(layout.filter(item => item === 'b').length).toBe(3);
    engine.start();
    layout.forEach((item, index) => { if (item === 'b') engine.handleInput({ type: 'tap', index }); });
    expect(result.score).toBe(100); engine.destroy();
  });
});
