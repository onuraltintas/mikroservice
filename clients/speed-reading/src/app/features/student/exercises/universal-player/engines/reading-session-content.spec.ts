import { EngineCallbacks } from './base-engine.interface';
import { WordHighlightEngine } from './word-highlight.engine';
import { TextFadeEngine } from './text-fade.engine';
import { RegressionReductionEngine } from './regression-reduction.engine';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';
import { ReadingComprehensionEngine } from './reading-comprehension.engine';
import { ExamSimulationEngine } from './exam-simulation.engine';

const callbacks: EngineCallbacks = {
  onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
  onComplete: () => undefined, onError: () => undefined, onStateChange: () => undefined,
  onStepComplete: () => undefined, onAction: () => undefined
};

describe('server-owned reading content', () => {
  const factories = [
    () => new WordHighlightEngine(), () => new TextFadeEngine(),
    () => new RegressionReductionEngine(), () => new SubvocalizationReductionEngine(),
    () => new ReadingComprehensionEngine(), () => new ExamSimulationEngine()
  ];
  for (const create of factories) {
    const name = create().engineType;
    it(`${name} displays the server text instead of catalog text`, () => {
      const engine = create();
      engine.initialize({
        serverAuthoritative: true, content: 'sunucunun dogru metni',
        engineConfig: { readingTextContent: 'eski katalog metni', content: { text: 'eski' } }
      } as any, callbacks);
      expect(engine.getWords()).toEqual(['sunucunun', 'dogru', 'metni']);
      engine.destroy();
    });
    it(`${name} rejects a missing server text instead of manufacturing a result`, () => {
      expect(() => create().initialize({
        serverAuthoritative: true, content: '', engineConfig: { readingTextContent: 'eski metin' }
      } as any, callbacks)).toThrowError(/reading text/i);
    });
  }
});
