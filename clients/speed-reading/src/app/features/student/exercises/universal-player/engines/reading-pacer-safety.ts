export function boundedInteger(value: unknown, fallback: number, minimum: number, maximum: number): number {
  const numeric = typeof value === 'number' && Number.isFinite(value) ? Math.trunc(value) : fallback;
  return Math.min(maximum, Math.max(minimum, numeric));
}

export function boundedText(value: unknown, fallback: string, maximumLength = 100_000): string {
  return typeof value === 'string' ? value.slice(0, maximumLength) : fallback;
}

export function boundedStringArray(value: unknown, maximumItems = 500, maximumItemLength = 1_000): string[] {
  if (!Array.isArray(value)) return [];
  return value
    .filter((item): item is string => typeof item === 'string')
    .slice(0, maximumItems)
    .map(item => item.slice(0, maximumItemLength));
}

export function recordOrEmpty(value: unknown): Record<string, any> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, any>
    : {};
}
