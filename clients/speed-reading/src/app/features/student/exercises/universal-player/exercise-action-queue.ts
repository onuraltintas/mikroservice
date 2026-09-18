export async function finishAfterPendingActions(
  pendingActions: Promise<void>,
  onFinished: () => void,
  onFailure: (error: unknown) => void = () => undefined
): Promise<void> {
  try {
    await pendingActions;
    onFinished();
  } catch (error) {
    onFailure(error);
  }
}
