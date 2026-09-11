import { fakeAsync, tick } from '@angular/core/testing';
import { AdaptiveFluencyEngine } from './adaptive-fluency.engine';

describe('AdaptiveFluencyEngine', () => {
  it('keeps the primary passage for repetitions and switches only for transfer', fakeAsync(() => {
    const engine = new AdaptiveFluencyEngine();
    engine.initialize({
      Content: 'birinci metin burada',
      WordCount: 3,
      AdaptiveTransferContent: 'yeni transfer metni burada',
      AdaptiveTransferWordCount: 4,
      AdaptivePrimaryQuestions: [{ questionId: 'primary' }],
      AdaptiveTransferQuestions: [{ questionId: 'transfer' }]
    }, {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: () => undefined,
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: () => undefined
    });

    expect(engine.getText()).toBe('birinci metin burada');
    expect(engine.getQuestions()[0].questionId).toBe('primary');

    engine.applyStage({ stage: 2, targetWpm: 240, purpose: 'Ayrıntıları bulun.' });
    expect(engine.getText()).toBe('birinci metin burada');
    expect(engine.getQuestions()).toEqual([]);
    expect(engine.getTargetWpm()).toBe(240);

    engine.applyStage({ stage: 3, targetWpm: 240 });
    expect(engine.getText()).toBe('yeni transfer metni burada');
    expect(engine.getQuestions()[0].questionId).toBe('transfer');

    engine.start();
    tick(250);
    engine.destroy();
  }));
});
