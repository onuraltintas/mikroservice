/**
 * Visual expansion answer boxes (reported 29.10.2026 on phones). Android keyboards deliver every letter as an IME
 * composition and may keep writing into a box after it is full; the browser's maxlength then cuts the extra letters
 * when the composition ends. Instead, the box limit is applied here: extra letters move on to the following boxes and
 * focus follows the first box that still needs input.
 */
export function distributeExpansionAnswer(
  values: readonly string[],
  index: number,
  raw: string,
  maxLengths: readonly number[]
): { values: string[]; focusIndex: number | null } {
  const count = maxLengths.length;
  const next = Array.from({ length: count }, (_, position) => values[position] ?? '');
  if (index < 0 || index >= count) return { values: next, focusIndex: null };
  let rest = raw.replace(/\s+/g, '');
  let last = index;
  for (let position = index; position < count; position++) {
    if (position > index && !rest) break;
    const limit = Math.max(1, maxLengths[position] || 1);
    next[position] = rest.slice(0, limit);
    rest = rest.slice(limit);
    last = position;
  }
  const lastFull = next[last].length >= Math.max(1, maxLengths[last] || 1);
  const focusIndex = lastFull ? (last + 1 < count ? last + 1 : null) : (last !== index ? last : null);
  return { values: next, focusIndex };
}
