import { fakeAsync, tick } from '@angular/core/testing';
import { FocusEngine } from './focus.engine';

function callbacks() {
  return { onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
    onComplete: () => undefined, onError: () => undefined, onStateChange: () => undefined,
    onStepComplete: () => undefined, onAction: () => undefined };
}

describe('FocusEngine timing', () => {
  it('shows the first stimulus for exactly one configured interval', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 2, 3] } as any, callbacks());
    engine.start();
    expect(engine.currentPosition).toBe(1);
    tick(1000);
    expect(engine.currentPosition).toBe(2);
    engine.destroy();
  }));

  it('preserves the remaining assessment stimulus time across pauses', fakeAsync(() => {
    const engine = new FocusEngine(); const actions: any[] = [];
    engine.initialize({ Mode: 'position', SpeedMs: 1000, AssessmentMode: true, totalSteps: 3 } as any,
      { ...callbacks(), onAction: action => actions.push(action) });
    engine.start();
    engine.reconcileServerResponse({ action: 'focus_step' }, { isValid: true, feedbackData: { position: 1 } });
    tick(400); engine.pause(); tick(2000); engine.resume(); tick(600);
    expect(actions.filter(action => action.action === 'focus_step').length).toBe(2);
    engine.destroy();
  }));

  it('cancels delayed repeated-cell updates after stopping', fakeAsync(() => {
    const engine = new FocusEngine();
    engine.initialize({ Mode: 'position', SpeedMs: 1000, PositionSequence: [1, 1, 2] } as any, callbacks());
    engine.start(); tick(2000); engine.stop();
    const step = engine.state.currentStep; tick(200);
    expect(engine.state.currentStep).toBe(step);
    engine.destroy();
  }));
});
