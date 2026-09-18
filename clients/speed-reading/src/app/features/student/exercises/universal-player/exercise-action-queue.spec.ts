import {
  createActionFailureState,
  finishAfterPendingActions,
  recordActionFailure,
  resetActionFailureState,
  runForActionGeneration
} from './exercise-action-queue';

describe('exercise action completion queue', () => {
  it('continues completion after all actions succeed', async () => {
    let completed = false;
    await finishAfterPendingActions(Promise.resolve(), () => { completed = true; });
    expect(completed).toBeTrue();
  });

  it('blocks completion when a required action permanently fails', async () => {
    let completed = false;
    let failed = false;
    await finishAfterPendingActions(
      Promise.reject(new Error('answer was not persisted')),
      () => { completed = true; },
      () => { failed = true; });
    expect(completed).toBeFalse();
    expect(failed).toBeTrue();
  });

  it('keeps an earlier action failure authoritative after a later action succeeds', async () => {
    let completed = false;
    let failed = false;
    await finishAfterPendingActions(
      Promise.resolve(),
      () => { completed = true; },
      () => { failed = true; },
      () => ({ hasFailure: true, error: new Error('an earlier answer was lost') }));
    expect(completed).toBeFalse();
    expect(failed).toBeTrue();
  });

  it('treats a falsy rejection reason as a recorded failure', async () => {
    let completed = false;
    let failed = false;
    await finishAfterPendingActions(
      Promise.resolve(),
      () => { completed = true; },
      () => { failed = true; },
      () => ({ hasFailure: true, error: undefined }));
    expect(completed).toBeFalse();
    expect(failed).toBeTrue();
  });

  it('ignores a delayed failure from an earlier session generation', () => {
    const state = createActionFailureState();
    const oldGeneration = state.generation;
    resetActionFailureState(state);

    recordActionFailure(state, oldGeneration, new Error('old session failed late'));

    expect(state.hasFailure).toBeFalse();
  });

  it('does not run a completion callback from an earlier session generation', () => {
    const state = createActionFailureState();
    const oldGeneration = state.generation;
    let completed = false;
    resetActionFailureState(state);

    runForActionGeneration(state, oldGeneration, () => { completed = true; });

    expect(completed).toBeFalse();
  });
});
