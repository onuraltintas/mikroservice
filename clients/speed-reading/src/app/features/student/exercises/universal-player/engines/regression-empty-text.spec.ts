import { RegressionReductionEngine } from './regression-reduction.engine';

describe('Regression text validation', () => {
  it('rejects missing preview text instead of an immediate completed session', () => {
    const engine = new RegressionReductionEngine();
    expect(() => engine.initialize({} as any, {} as any)).toThrowError(/metin/i);
  });
  it('rejects whitespace-only preview text', () => {
    const engine = new RegressionReductionEngine();
    expect(() => engine.initialize({ readingTextContent: '   ' } as any, {} as any)).toThrowError(/metin/i);
  });
});
