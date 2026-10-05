export interface CustomPreviewContext {
  roles: readonly string[];
  preview: boolean;
}

export const CUSTOM_PREVIEW_ENGINES = ['word_highlight', 'subvocalization_reduction', 'regression_reduction', 'text_fade'] as const;

/** Applies temporary numeric controls only; catalogue content and identifiers remain untouched. */
export function applyCustomPreviewSettings<T extends Record<string, unknown>>(
  configuration: T,
  values: Record<string, unknown>,
  context: CustomPreviewContext
): T {
  if (!context.preview || !context.roles.some(role => ['Admin', 'SystemAdmin', 'Teacher'].includes(role))) {
    return configuration;
  }
  const engine = configuration['engineType'];
  if (!CUSTOM_PREVIEW_ENGINES.some(type => type === engine)) return configuration;
  const validated: Record<string, number> = {};
  for (const [key, min, max] of [['speedWpm', 20, 1500], ['chunkSize', 1, 10]] as const) {
    if (values[key] === undefined) continue;
    if (key === 'chunkSize' && engine === 'text_fade') continue;
    const value = values[key];
    if (typeof value !== 'number' || !Number.isInteger(value) || value < min || value > max) {
      throw new Error(`${key} için geçerli bir değer girin (${min}–${max}).`);
    }
    validated[key] = value;
  }
  const result = structuredClone(configuration);
  const nested = result['engineConfig'];
  const settings: Record<string, unknown> = nested && typeof nested === 'object' && !Array.isArray(nested)
    ? nested as Record<string, unknown> : {};
  (result as Record<string, unknown>)['engineConfig'] = settings;
  if (validated['chunkSize'] !== undefined) settings['chunkSize'] = validated['chunkSize'];
  if (engine === 'word_highlight') {
    const existing = settings['pacer'];
    const pacer = existing && typeof existing === 'object' && !Array.isArray(existing)
      ? existing as Record<string, unknown> : {};
    settings['pacer'] = pacer;
    if (validated['speedWpm'] !== undefined) {
      settings['targetWpm'] = validated['speedWpm'];
      pacer['speedWpm'] = validated['speedWpm'];
    }
    if (validated['chunkSize'] !== undefined) pacer['chunkSize'] = validated['chunkSize'];
  } else if (validated['speedWpm'] !== undefined) {
    settings['targetWpm'] = validated['speedWpm'];
    settings['wpm'] = validated['speedWpm'];
    if (engine === 'subvocalization_reduction') settings['msPerWord'] = Math.round(60000 / validated['speedWpm']);
    if (engine === 'regression_reduction') settings['wordDelayMs'] = 0;
  }
  return result;
}
