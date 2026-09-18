import { ScreenHelper } from '../../../../../core/utils/screen-helper';

const MIN_OFFSET_PERCENT = 5;
const MAX_OFFSET_PERCENT = 45;

/**
 * Converts the full visual angle between two symmetric stimuli into the
 * percentage offset of one stimulus from the viewport centre.
 */
export function visualAngleToOffsetPercent(degrees: number, dimensionPx: number): number {
  if (!Number.isFinite(dimensionPx) || dimensionPx <= 0) {
    return MIN_OFFSET_PERCENT;
  }

  const boundedDegrees = Math.max(0, Math.min(60, Number.isFinite(degrees) ? degrees : 0));
  const fullSpacingPx = ScreenHelper.degreesToPixels(boundedDegrees);
  const oneSidedOffset = fullSpacingPx / dimensionPx * 50;
  return Math.max(MIN_OFFSET_PERCENT, Math.min(MAX_OFFSET_PERCENT, oneSidedOffset));
}

export function visualAngleToAxisOffsetPercent(
  degrees: number,
  dimensionPx: number,
  radial: boolean
): number {
  const offset = visualAngleToOffsetPercent(degrees, dimensionPx);
  return radial ? offset / Math.SQRT2 : offset;
}
