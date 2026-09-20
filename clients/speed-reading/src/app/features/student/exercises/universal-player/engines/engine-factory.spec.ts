import { EngineFactory } from './engine-factory';

describe('EngineFactory', () => {
  it('creates the canonical word highlight engine for the legacy word_group alias', () => {
    expect(EngineFactory.isSupported('word_group')).toBeTrue();
    expect(EngineFactory.create('word_group')?.engineType).toBe('word_highlight');
  });

  it('lists every supported engine exactly once', () => {
    const supported = EngineFactory.getSupportedEngines();

    expect(new Set(supported).size).toBe(supported.length);
    supported.forEach(engineType => {
      expect(EngineFactory.isSupported(engineType)).toBeTrue();
      expect(EngineFactory.create(engineType)).not.toBeNull();
    });
  });
});
