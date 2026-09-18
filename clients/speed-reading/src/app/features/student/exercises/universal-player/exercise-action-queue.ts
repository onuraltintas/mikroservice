export interface ActionFailureState {
  generation: number;
  hasFailure: boolean;
  error: unknown;
}

export function createActionFailureState(): ActionFailureState {
  return { generation: 0, hasFailure: false, error: undefined };
}

export function resetActionFailureState(state: ActionFailureState): void {
  state.generation++;
  state.hasFailure = false;
  state.error = undefined;
}

export function recordActionFailure(
  state: ActionFailureState,
  generation: number,
  error: unknown
): void {
  if (generation !== state.generation || state.hasFailure) return;
  state.hasFailure = true;
  state.error = error;
}

export function runForActionGeneration(
  state: ActionFailureState,
  generation: number,
  callback: () => void
): void {
  if (generation === state.generation) callback();
}

export async function finishAfterPendingActions(
  pendingActions: Promise<void>,
  onFinished: () => void,
  onFailure: (error: unknown) => void = () => undefined,
  getPriorFailure: () => Pick<ActionFailureState, 'hasFailure' | 'error'> = () => ({
    hasFailure: false,
    error: undefined
  })
): Promise<void> {
  try {
    await pendingActions;
    const priorFailure = getPriorFailure();
    if (priorFailure.hasFailure) {
      onFailure(priorFailure.error);
      return;
    }
    onFinished();
  } catch (error) {
    onFailure(error);
  }
}
