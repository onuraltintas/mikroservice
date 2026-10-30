import { beforeEach, vi } from 'vitest';

// jsdom has no IntersectionObserver. `@defer (on viewport)` blocks create one after render; without this stub the
// error surfaces as an unhandled exception and fails the run at random (seen in CI on the staff portal suite).
class TestIntersectionObserver {
  readonly root = null;
  readonly rootMargin = '';
  readonly thresholds: ReadonlyArray<number> = [];
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
  takeRecords(): IntersectionObserverEntry[] { return []; }
}

const scope = globalThis as unknown as { IntersectionObserver?: unknown };
scope.IntersectionObserver ??= TestIntersectionObserver;

// Specs run with `isolate: false`, so a Vitest worker keeps one module graph for all spec files it runs. With more than
// one worker (CI runners have 4 vCPUs, so Vitest starts 3) every spec file is a separate run on a shared worker. When a
// run ends, Vitest waits for the module fetches in flight and then rejects any fetch still pending, such as the static
// imports a just-arrived lazy chunk requests next. The chunk is then cached as failed in the shared graph, and a later
// spec on that worker that imports it fails with "EnvironmentTeardownError: Cannot load '/chunk-….js' … after the
// environment was torn down". Lazy chunks are started by Playthrough `@defer` blocks and `import()` helpers that tests
// do not await, so every test waits for its chunk loads once it has finished and TestBed has torn it down.
beforeEach(({ onTestFinished }) => onTestFinished(() => vi.dynamicImportSettled()));
