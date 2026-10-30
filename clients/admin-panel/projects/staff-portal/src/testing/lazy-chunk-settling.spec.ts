// Regression check for the CI-only "EnvironmentTeardownError: Cannot load '/chunk-….js' … after the environment was
// torn down" failures (H20).
//
// Specs run with `isolate: false`. With more than one Vitest worker (CI runners have 4 vCPUs) every spec file is a
// separate run on a shared worker; when a run ends, Vitest rejects module fetches that are still pending (for example
// the static imports of a lazy chunk that has just arrived) and the failed chunk stays cached in the worker's module
// graph, so a later spec that imports it fails. A lazy chunk that a test starts loading (Playthrough `@defer` blocks,
// `import()` helpers) must therefore be loaded before the next test starts; test-setup.ts waits for that.
describe('lazy chunks started by a test', () => {
  let loaded = false;

  it('may leave a lazy chunk loading when the test body ends', () => {
    void import('./lazy-chunk.fixture').then(() => (loaded = true));
  });

  it('are loaded before the next test starts', () => {
    expect(loaded).toBe(true);
  });
});
