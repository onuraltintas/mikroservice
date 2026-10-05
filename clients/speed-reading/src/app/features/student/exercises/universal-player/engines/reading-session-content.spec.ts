import { EngineCallbacks } from './base-engine.interface';
import { WordHighlightEngine } from './word-highlight.engine';
import { TextFadeEngine } from './text-fade.engine';
import { RegressionReductionEngine } from './regression-reduction.engine';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';
import { ReadingComprehensionEngine } from './reading-comprehension.engine';
import { ExamSimulationEngine } from './exam-simulation.engine';
import { ExercisePlayerComponent } from '../exercise-player.component';

const callbacks: EngineCallbacks = {
  onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
  onComplete: () => undefined, onError: () => undefined, onStateChange: () => undefined,
  onStepComplete: () => undefined, onAction: () => undefined
};

describe('server-owned reading content', () => {
  it('preserves snapshot text through the actual player configuration merge', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.isAssessmentMode = false;
    const config = { engineConfig: { content: { text: 'eski katalog' }, readingTextContent: 'eski' } };
    const session = player.normalizeSessionConfiguration(config, { content: 'dogru sunucu metni', wordCount: 3, questions: [] });
    const engine = new WordHighlightEngine();
    engine.initialize({ ...config, ...config.engineConfig, ...session, ...session.engineConfig, serverAuthoritative: true }, callbacks);
    expect(engine.getWords()).toEqual(['dogru', 'sunucu', 'metni']);
  });
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
    it(`${name} rejects an absent snapshot even when catalog content exists`, () => {
      expect(() => create().initialize({
        serverAuthoritative: true, engineConfig: { readingTextContent: 'eski metin' }
      } as any, callbacks)).toThrowError(/reading text/i);
    });
  }
  for (const create of [() => new ReadingComprehensionEngine(), () => new ExamSimulationEngine()]) {
    it(`${create().engineType} counts the displayed text, not stale catalog words`, () => {
      const engine = create();
      engine.initialize({ serverAuthoritative: true, content: 'bir iki uc', wordCount: 3,
        engineConfig: { wordCount: 999, content: { wordCount: 999 } } } as any, callbacks);
      expect(engine.state.totalSteps).toBe(3);
    });
  }
});
