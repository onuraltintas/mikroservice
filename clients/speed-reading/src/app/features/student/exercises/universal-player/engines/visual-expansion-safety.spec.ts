import { fakeAsync, tick } from '@angular/core/testing';
import { VisualExpansionEngine } from './visual-expansion.engine';
import { getCustomPreviewControls } from '../custom-preview-settings';

describe('visual expansion configuration safety', () => {
  const callbacks = () => ({ onStart() {}, onPause() {}, onResume() {}, onComplete() {}, onError() {},
    onStateChange() {}, onStepComplete() {}, onAction() {} });
  for (const type of ['word', 'symbol']) it(`uses the ${type} pool in radial preview`, fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    engine.initialize({ rounds: 1, expansion: { pattern: 'radial', stimulusType: type },
      timing: { durationMs: 100, intervalMs: 1 } } as any, callbacks());
    engine.start(); tick(1);
    const pool = type === 'word' ? ['EV','SU','GÜN','YOL','KUŞ','AY','EL','DAĞ'] : ['★','●','▲','■','◆','+','×','÷'];
    expect(engine.getCurrentStimuli().length).toBe(4);
    expect(engine.getCurrentStimuli().every(stimulus => pool.includes(stimulus.content))).toBeTrue();
    engine.destroy();
  }));
  it('limits preview exposure to the same 100 ms minimum as the server', () => {
    expect(getCustomPreviewControls({ engineType: 'visual_expansion' }).find(item => item.key === 'displayDurationMs')!.min).toBe(100);
  });
  it('rejects a placement pattern without an implementation', () => {
    const engine = new VisualExpansionEngine();
    expect(() => engine.initialize({ expansion: { pattern: 'random' } } as any, callbacks())).toThrow();
  });
  it('reports relative screen distance and sanitizes configured stimulus size', () => {
    const engine = new VisualExpansionEngine();
    engine.initialize({ startDegrees: 30, visuals: { stimulusSize: '2rem' } } as any, callbacks());
    expect((engine as any).getRelativeDistancePercent()).toBe(50);
    expect((engine as any).getStimulusSizeCss()).toBe('2rem');
    engine.initialize({ visuals: { stimulusSize: 'url(javascript:bad)' } } as any, callbacks());
    expect((engine as any).getStimulusSizeCss()).toBe('2rem');
  });
  it('bounds numeric fonts and exposes center/duration helpers', () => {
    const engine = new VisualExpansionEngine();
    engine.initialize({ visuals: { centerPoint: 'dot', stimulusSize: '100px' } } as any, callbacks());
    expect(engine.getStimulusSizeCss()).toBe('64px');
    expect(engine.getCenterPointType()).toBe('dot');
    expect(engine.getCurrentDisplayDurationMs()).toBe(250);
    engine.initialize({ visuals: { stimulusSize: '0.1rem' } } as any, callbacks());
    expect(engine.getStimulusSizeCss()).toBe('1rem');
  });
  for (const pattern of ['horizontal', 'vertical']) it(`uses numeric ${pattern} previews and resets without retaining old difficulty`, fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    engine.initialize({ rounds: 2, startDegrees: 8, targetDegrees: 10,
      expansion: { pattern, stimulusType: 'number' }, timing: { durationMs: 200, intervalMs: 1 },
      getRenderBounds: () => ({ width: 350, height: 500 }) } as any, callbacks());
    engine.start(); tick(1);
    const shown = engine.getCurrentStimuli();
    expect(shown.every(item => /^[1-9]$/.test(item.content))).toBeTrue();
    expect(pattern === 'vertical' ? shown.every(item => item.x === 50) : shown.every(item => item.y === 50)).toBeTrue();
    tick(200); engine.handleInput({ answers: shown.map(item => item.content) });
    expect(engine.getCurrentDisplayDurationMs()).toBe(190);
    engine.reset();
    expect(engine.getCurrentDisplayDurationMs()).toBe(200);
    expect(engine.currentDegrees).toBe(8);
    expect(engine.getExpectedAnswerCount()).toBe(0);
    engine.destroy();
  }));
  it('recovers expired server rounds without accepting an unverified response', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    const actions: any[] = [], errors: string[] = [];
    engine.initialize({ serverAuthoritative: true, rounds: 2, timing: { durationMs: 100, intervalMs: 1 } } as any,
      { ...callbacks(), onAction: action => actions.push(action), onError: error => errors.push(error) });
    engine.start(); tick(1);
    engine.reconcileServerResponse({ action: 'visual_expansion_present' }, { isValid: true, feedbackData: { stimuli: ['A','B'], displayDurationMs: 100 } });
    tick(100); engine.handleInput({ answers: ['A','B'] });
    engine.reconcileServerResponse({ action: 'visual_expansion_answer' }, { isValid: false, message: 'outside its response window' });
    tick(1);
    expect(actions.at(-1).action).toBe('visual_expansion_present');
    expect(engine.state.currentStep).toBe(0);
    engine.reconcileServerResponse({ action: 'visual_expansion_present' }, { isValid: true, feedbackData: { stimuli: [] } });
    expect(errors.length).toBe(1);
    engine.destroy();
  }));
  it('does not show an in-flight server presentation until resume or request duplicate rounds', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    const actions: any[] = [];
    engine.initialize({ serverAuthoritative: true, rounds: 1, timing: { durationMs: 100, intervalMs: 1 } } as any,
      { ...callbacks(), onAction: action => actions.push(action) });
    engine.start(); tick(1); engine.pause(); tick(1000); engine.resume(); tick(1);
    expect(actions.length).toBe(1);
    engine.pause();
    engine.reconcileServerResponse({ action: 'visual_expansion_present' }, { isValid: true, feedbackData: { stimuli: ['A','B'], displayDurationMs: 100 } });
    tick(1000); expect(engine.getCurrentStimuli()).toEqual([]);
    engine.resume(); tick(100);
    expect(engine.isWaitingForInput).toBeTrue();
    engine.destroy();
  }));
  it('keeps a presentation hidden while paused and continues its remaining exposure without replay', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    engine.initialize({ rounds: 1, expansion: { pattern: 'horizontal', stimulusType: 'letter' },
      timing: { durationMs: 400, intervalMs: 1 } } as any, callbacks());
    engine.start(); tick(1); tick(100);
    const shown = engine.getCurrentStimuli().map(item => item.content);
    engine.pause(); tick(2000);
    expect(engine.getCurrentStimuli()).toEqual([]);
    engine.resume();
    expect(engine.getCurrentStimuli().map(item => item.content)).toEqual(shown);
    tick(300);
    expect(engine.isWaitingForInput).toBeTrue();
    engine.destroy();
  }));
  it('rejects paused answers and excludes paused time from response latency', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    let result: any;
    engine.initialize({ rounds: 1, expansion: { pattern: 'horizontal', stimulusType: 'letter' },
      timing: { durationMs: 100, intervalMs: 1 } } as any, { ...callbacks(), onComplete: value => result = value });
    engine.start(); tick(101);
    const answers = engine.getLastShownStimuli();
    tick(50); engine.pause(); tick(2000); engine.handleInput({ answers });
    expect(engine.state.currentStep).toBe(0);
    engine.resume(); tick(50); engine.handleInput({ answers });
    expect(result.details.averageResponseTimeMs).toBe(100);
    engine.destroy();
  }));
  it('tracks the greatest actually displayed server distance, not an unseen next round', fakeAsync(() => {
    const engine = new VisualExpansionEngine();
    let result: any;
    engine.initialize({ rounds: 1, serverAuthoritative: true, startDegrees: 4,
      expansion: { pattern: 'horizontal', stimulusType: 'letter' }, timing: { durationMs: 100, intervalMs: 1 } } as any,
      { ...callbacks(), onComplete: value => result = value });
    engine.start(); tick(1);
    engine.reconcileServerResponse({ action: 'visual_expansion_present' }, { isValid: true,
      feedbackData: { degrees: 20, stimuli: ['A','B'], displayDurationMs: 100 } });
    tick(100); engine.handleInput({ answers: ['A','B'] });
    engine.reconcileServerResponse({ action: 'visual_expansion_answer' }, { isValid: true, isCorrect: true,
      feedbackData: { responseTimeMs: 123 } });
    expect(result.details.maxDegreesReached).toBe(20);
    expect(result.details.averageResponseTimeMs).toBe(123);
    engine.destroy();
  }));
});
