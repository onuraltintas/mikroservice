const MIN_OFFSET_PERCENT = 5;
const MAX_OFFSET_PERCENT = 45;

/**
 * Maps the configured visual angle to a stable viewport percentage. Browser
 * layout uses CSS pixels, so physical-PPI estimates (especially when DPR is
 * above 1) would otherwise push even small starting angles to the edges.
 */
export function visualAngleToOffsetPercent(degrees: number, dimensionPx: number): number {
  if (!Number.isFinite(dimensionPx) || dimensionPx <= 0) {
    return MIN_OFFSET_PERCENT;
  }

  const boundedDegrees = Math.max(0, Math.min(60, Number.isFinite(degrees) ? degrees : 0));
  const proportionalOffset = boundedDegrees / 60 * MAX_OFFSET_PERCENT;
  return Math.max(MIN_OFFSET_PERCENT, Math.min(MAX_OFFSET_PERCENT, proportionalOffset));
}

export function visualAngleToAxisOffsetPercent(
  degrees: number,
  dimensionPx: number,
  radial: boolean
): number {
  const offset = visualAngleToOffsetPercent(degrees, dimensionPx);
  return radial ? offset / Math.SQRT2 : offset;
}
