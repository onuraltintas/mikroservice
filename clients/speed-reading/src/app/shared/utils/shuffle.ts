/**
 * Unbiased in-place Fisher–Yates shuffle. `sort(() => Math.random() - 0.5)` is biased
 * (the first element tends to stay first), so answer options must use this instead.
 */
export function shuffleInPlace<T>(items: T[], random: () => number = Math.random): T[] {
  for (let index = items.length - 1; index > 0; index--) {
    const swap = Math.floor(random() * (index + 1));
    [items[index], items[swap]] = [items[swap], items[index]];
  }
  return items;
}

/** Returns a shuffled copy; the input array is left untouched. */
export function shuffled<T>(items: readonly T[], random: () => number = Math.random): T[] {
  return shuffleInPlace([...items], random);
}
