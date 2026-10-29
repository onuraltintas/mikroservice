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
