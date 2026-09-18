import { finishAfterPendingActions } from './exercise-action-queue';

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
});
