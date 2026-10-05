export function boundedInteger(value: unknown, fallback: number, minimum: number, maximum: number): number {
  const numeric = typeof value === 'number' && Number.isFinite(value) ? Math.trunc(value) : fallback;
  return Math.min(maximum, Math.max(minimum, numeric));
}

export function boundedText(value: unknown, fallback: string, maximumLength = 100_000): string {
  return typeof value === 'string' ? value.slice(0, maximumLength) : fallback;
}

export function resolveReadingText(config: Record<string, unknown>, fallback: string): string {
  const nested = recordOrEmpty(caseInsensitiveField(config, 'engineConfig'));
  const rootContent = caseInsensitiveField(config, 'content');
  const nestedContent = caseInsensitiveField(nested, 'content');
  const serverOwned = config['serverAuthoritative'] === true || config['isAssessmentMode'] === true;
  const snapshotText = typeof rootContent === 'string'
    ? rootContent
    : caseInsensitiveField(config, 'readingTextContent')
      ?? caseInsensitiveField(recordOrEmpty(rootContent), 'text');
  const raw = serverOwned ? snapshotText
    : snapshotText
      ?? caseInsensitiveField(nested, 'readingTextContent')
      ?? (typeof nestedContent === 'string' ? nestedContent : caseInsensitiveField(recordOrEmpty(nestedContent), 'text'))
      ?? caseInsensitiveField(config, 'text');
  if (serverOwned && (typeof raw !== 'string' || !raw.trim() || raw.length > 100_000)) {
    throw new Error('Server reading text is missing or invalid.');
  }
  return boundedText(raw, fallback);
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

export function caseInsensitiveField(record: Record<string, any>, name: string): any {
  const key = Object.keys(record).reverse()
    .find(candidate => candidate.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : record[key];
}

export function mergeCaseInsensitiveRecords(
  root: Record<string, any>,
  nested: Record<string, any>,
  name: string
): Record<string, any> {
  const merged: Record<string, any> = {};
  for (const source of [recordOrEmpty(caseInsensitiveField(root, name)), recordOrEmpty(caseInsensitiveField(nested, name))]) {
    for (const [key, value] of Object.entries(source)) merged[key.toLowerCase()] = value;
  }
  return merged;
}
