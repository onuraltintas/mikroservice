import { caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './engines/reading-pacer-safety';

export interface CustomPreviewContext {
  roles: readonly string[];
  preview: boolean;
}

export const CUSTOM_PREVIEW_ENGINES = ['word_highlight', 'subvocalization_reduction', 'regression_reduction', 'text_fade', 'text_stream', 'motion_path', 'scan_find', 'focus', 'vocabulary_builder', 'visual_expansion'] as const;

export interface PreviewControl {
  key: string;
  label: string;
  min: number;
  max: number;
  value: number;
}

export function getCustomPreviewControls(configuration: Record<string, unknown>): PreviewControl[] {
  const settings = recordOrEmpty(configuration['engineConfig']);
  const timing = mergeCaseInsensitiveRecords(configuration, settings, 'timing');
  const movement = mergeCaseInsensitiveRecords(configuration, settings, 'movement');
  const pacer = mergeCaseInsensitiveRecords(configuration, settings, 'pacer');
  const fading = mergeCaseInsensitiveRecords(configuration, settings, 'fading');
  const session = recordOrEmpty(caseInsensitiveField(configuration, 'sessionData'));
  const difficulty = recordOrEmpty(caseInsensitiveField(session, 'difficultySettings') ?? caseInsensitiveField(settings, 'difficultySettings') ?? caseInsensitiveField(configuration, 'difficultySettings'));
  const read = (name: string) => caseInsensitiveField(session, name)
    ?? (configuration['engineType'] === 'subvocalization_reduction' ? caseInsensitiveField(difficulty, name) : undefined)
    ?? caseInsensitiveField(settings, name) ?? caseInsensitiveField(configuration, name);
  const control = (key: string, label: string, min: number, max: number, value: unknown): PreviewControl =>
    ({ key, label, min, max, value: Number(value) });
  switch (configuration['engineType']) {
    case 'focus': return [control('speedMs', 'Uyaran süresi (ms)', 100, 10000, read('SpeedMs') ?? read('FocusSpeedMs') ?? 1500)];
    case 'vocabulary_builder': return read('mode') === 'quiz'
      ? [control('timeLimitPerWord', 'Kelime başına süre (saniye; 0: sınırsız)', 0, 3600, read('timeLimitPerWord') ?? 0)] : [];
    case 'visual_expansion': return [
      control('displayDurationMs', 'Gösterim süresi (ms)', 50, 5000, timing['durationms'] ?? read('VisualExpansionDisplayDurationMs') ?? 250),
      control('intervalMs', 'Gösterimler arası bekleme (ms)', 50, 10000, timing['intervalms'] ?? 1500)
    ];
    case 'text_stream': return [
      control('displayDurationMs', 'Gösterim süresi (ms)', 50, 5000, read('displayDurationMs') ?? read('intervalMs') ?? timing['durationms'] ?? 500),
      control('intervalMs', 'Gösterimler arası bekleme (ms)', 0, 10000, timing['intervalms'] ?? 0)
    ];
    case 'motion_path': {
      const mode = String(read('mode') ?? 'fixation').toLowerCase();
      if (mode === 'tracking') return [control('speedLevel', 'Hareket hızı seviyesi', 1, 5, movement['speedlevel'] ?? 1)];
      if (mode === 'saccade') return [control('jumpIntervalMs', 'Hedef geçiş aralığı (ms)', 50, 10000, movement['jumpintervalms'] ?? 1000)];
      return [control('holdMs', 'Odaklanma süresi (ms)', 50, 10000, timing['holdms'] ?? movement['fixationtimems'] ?? 2000)];
    }
    case 'scan_find': return [control('timeLimitSec', 'Süre sınırı (saniye)', 1, 3600, read('timeLimitSeconds') ?? read('timeLimit') ?? timing['timelimitsec'] ?? 3600)];
    default: {
      if (!CUSTOM_PREVIEW_ENGINES.some(type => type === configuration['engineType'])) return [];
      const controls = [control('speedWpm', 'Okuma hızı (kelime/dakika)', 20, 1500, read('targetWpm') ?? read('wpm') ?? pacer['speedwpm'] ?? fading['speedwpm'] ?? 200)];
      if (configuration['engineType'] !== 'text_fade') controls.push(control('chunkSize', 'Kelime grubu', 1, 10, read('chunkSize') ?? pacer['chunksize'] ?? 1));
      return controls;
    }
  }
}

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
  const controls = getCustomPreviewControls(configuration);
  if (!controls.length) return configuration;
  const validated: Record<string, number> = {};
  for (const { key, min, max, label } of controls) {
    if (values[key] === undefined) continue;
    if (key === 'chunkSize' && engine === 'text_fade') continue;
    const value = values[key];
    if (typeof value !== 'number' || !Number.isInteger(value) || value < min || value > max) {
      throw new Error(`${label} için geçerli bir değer girin (${min}–${max}).`);
    }
    validated[key] = value;
  }
  if (!Object.keys(validated).length) return configuration;
  const result = structuredClone(configuration);
  const nested = result['engineConfig'];
  const settings: Record<string, unknown> = nested && typeof nested === 'object' && !Array.isArray(nested)
    ? nested as Record<string, unknown> : {};
  (result as Record<string, unknown>)['engineConfig'] = settings;
  const merge = (name: string, entries: Record<string, number>) => {
    const original = settings[name] ?? configuration[name];
    settings[name] = { ...(original && typeof original === 'object' && !Array.isArray(original) ? original as Record<string, unknown> : {}), ...entries };
  };
  if (engine === 'focus' || engine === 'vocabulary_builder') {
    const key = engine === 'focus' ? 'SpeedMs' : 'timeLimitPerWord';
    const value = validated[engine === 'focus' ? 'speedMs' : key];
    if (value !== undefined) {
      settings[key] = value;
      const session = recordOrEmpty(caseInsensitiveField(result, 'sessionData'));
      if (Object.keys(session).length) {
        const field = engine === 'focus' ? 'SessionData' : 'sessionData';
        (result as Record<string, unknown>)[field] = { ...session, [key]: value };
      }
    }
    return result;
  }
  if (engine === 'visual_expansion') {
    if (validated['displayDurationMs'] !== undefined) merge('timing', { durationMs: validated['displayDurationMs'] });
    if (validated['intervalMs'] !== undefined) merge('timing', { intervalMs: validated['intervalMs'] });
    return result;
  }
  if (engine === 'text_stream') {
    if (validated['displayDurationMs'] !== undefined) settings['displayDurationMs'] = validated['displayDurationMs'];
    if (validated['intervalMs'] !== undefined) merge('timing', { intervalMs: validated['intervalMs'] });
    return result;
  }
  if (engine === 'motion_path') {
    if (validated['holdMs'] !== undefined) merge('timing', { holdMs: validated['holdMs'] });
    if (validated['speedLevel'] !== undefined) merge('movement', { speedLevel: validated['speedLevel'] });
    if (validated['jumpIntervalMs'] !== undefined) merge('movement', { jumpIntervalMs: validated['jumpIntervalMs'] });
    return result;
  }
  if (engine === 'scan_find') {
    if (validated['timeLimitSec'] !== undefined) settings['timeLimitSeconds'] = validated['timeLimitSec'];
    return result;
  }
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
  if (engine === 'subvocalization_reduction' || engine === 'regression_reduction') {
    const overrides: Record<string, number> = {};
    if (validated['speedWpm'] !== undefined) {
      overrides['targetWpm'] = validated['speedWpm'];
      overrides['wpm'] = validated['speedWpm'];
      if (engine === 'subvocalization_reduction') overrides['msPerWord'] = Math.round(60000 / validated['speedWpm']);
      else overrides['wordDelayMs'] = 0;
    }
    if (validated['chunkSize'] !== undefined) overrides['chunkSize'] = validated['chunkSize'];
    if (engine === 'subvocalization_reduction') merge('difficultySettings', overrides);
    const session = recordOrEmpty(caseInsensitiveField(result, 'sessionData'));
    if (Object.keys(session).length) (result as Record<string, unknown>)['sessionData'] = { ...session, ...overrides };
  }
  return result;
}
