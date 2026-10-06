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
});
