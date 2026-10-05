import { boundedInteger, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './engines/reading-pacer-safety';

export interface CustomPreviewContext {
  roles: readonly string[];
  preview: boolean;
}

export const CUSTOM_PREVIEW_ENGINES = ['word_highlight', 'subvocalization_reduction', 'regression_reduction', 'text_fade', 'text_stream', 'motion_path', 'scan_find', 'focus', 'vocabulary_builder', 'visual_expansion', 'reading_comprehension', 'free_reading', 'exam_simulation', 'grid_interaction', 'visualization'] as const;

export interface PreviewControl {
  key: string;
  label: string;
  min: number;
  max: number;
  value: number | string;
  options?: { value: string; label: string }[];
}

function overrideFields(source: unknown, values: Record<string, unknown>): Record<string, unknown> {
  const result = { ...recordOrEmpty(source) };
  for (const [name, value] of Object.entries(values)) {
    for (const key of Object.keys(result)) if (key.toLowerCase() === name.toLowerCase()) delete result[key];
    result[name] = value;
  }
  return result;
}

export function getCustomPreviewControls(configuration: Record<string, unknown>): PreviewControl[] {
  const settings = recordOrEmpty(configuration['engineConfig']);
  const timing = mergeCaseInsensitiveRecords(configuration, settings, 'timing');
  const movement = mergeCaseInsensitiveRecords(configuration, settings, 'movement');
  const pacer = mergeCaseInsensitiveRecords(configuration, settings, 'pacer');
  const fading = mergeCaseInsensitiveRecords(configuration, settings, 'fading');
  const session = ['focus', 'vocabulary_builder', 'subvocalization_reduction', 'regression_reduction', 'visualization'].includes(String(configuration['engineType']))
    ? recordOrEmpty(caseInsensitiveField(settings, 'sessionData') ?? caseInsensitiveField(configuration, 'sessionData')) : {};
  const difficulty = recordOrEmpty(caseInsensitiveField(session, 'difficultySettings') ?? caseInsensitiveField(settings, 'difficultySettings') ?? caseInsensitiveField(configuration, 'difficultySettings'));
  const read = (name: string) => caseInsensitiveField(session, name)
    ?? (configuration['engineType'] === 'subvocalization_reduction' ? caseInsensitiveField(difficulty, name) : undefined)
    ?? caseInsensitiveField(settings, name) ?? caseInsensitiveField(configuration, name);
  const control = (key: string, label: string, min: number, max: number, value: unknown): PreviewControl =>
    ({ key, label, min, max, value: Number(value) });
  switch (configuration['engineType']) {
    case 'visualization': {
      const scenes = read('scenes');
      if (!Array.isArray(scenes) || !scenes.length) return [];
      const guided = read('mode') === 'guided';
      const withSteps = scenes.filter(scene => {
        const steps = caseInsensitiveField(recordOrEmpty(scene), 'steps');
        return guided && Array.isArray(steps) && steps.length > 0;
      });
      const controls: PreviewControl[] = [];
      if (withSteps.length < scenes.length) controls.push(control('sceneDurationSec', 'Adımsız sahnelerin gösterim süresi (saniye)', 1, 3600,
        boundedInteger(caseInsensitiveField(recordOrEmpty(scenes.find(scene => !withSteps.includes(scene))), 'duration'), 5, 1, 3600)));
      if (withSteps.length) controls.push(control('stepDurationMs', 'Yönlendirmeli sahnelerde adım süresi (ms)', 100, 60000,
        boundedInteger(caseInsensitiveField(recordOrEmpty(withSteps[0]), 'stepDurationMs'), 3000, 100, 60000)));
      return controls;
    }
    case 'grid_interaction': return [control('gridSize', 'Tablo boyutu (satır ve sütun)', 3, 7,
      configuration['gridSize'] || recordOrEmpty(settings['grid'])['rows'] || 5)];
    case 'reading_comprehension':
    case 'free_reading':
    case 'exam_simulation': {
      const display = mergeCaseInsensitiveRecords(configuration, settings, 'display');
      return [{ key: 'fontSize', label: 'Metin boyutu', min: 0, max: 0, value: String(display['fontsize'] ?? 'medium').toLowerCase(),
        options: [{ value: 'small', label: 'Küçük' }, { value: 'medium', label: 'Orta' }, { value: 'large', label: 'Büyük' }] }];
    }
    case 'focus': return [control('speedMs', 'Uyaran süresi (ms)', 100, 10000, read('SpeedMs') ?? read('FocusSpeedMs') ?? 1500)];
    case 'vocabulary_builder': return read('mode') === 'quiz'
      ? [control('timeLimitPerWord', 'Kelime başına süre (saniye; 0: sınırsız)', 0, 3600, read('timeLimitPerWord') ?? 0)] : [];
    case 'visual_expansion': return [
      control('displayDurationMs', 'Gösterim süresi (ms)', 50, 5000, timing['durationms'] || read('VisualExpansionDisplayDurationMs') || 250),
      control('intervalMs', 'Gösterimler arası bekleme (ms)', 50, 10000, timing['intervalms'] || 1500)
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
  const fontControl = controls.find(control => control.key === 'fontSize');
  if (fontControl) {
    const value = values['fontSize'];
    if (value === undefined) return configuration;
    if (!fontControl.options?.some(option => option.value === value)) throw new Error('Geçerli bir metin boyutu seçin.');
    const result = structuredClone(configuration);
    const settings = recordOrEmpty(result['engineConfig']);
    (result as Record<string, unknown>)['engineConfig'] = settings;
    settings['display'] = overrideFields({ ...recordOrEmpty(caseInsensitiveField(configuration, 'display')), ...recordOrEmpty(caseInsensitiveField(settings, 'display')) }, { fontSize: value });
    return result;
  }
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
    const original = { ...recordOrEmpty(caseInsensitiveField(configuration, name)), ...recordOrEmpty(caseInsensitiveField(settings, name)) };
    settings[name] = overrideFields(original, entries);
  };
  const updateSessions = (entries: Record<string, number>) => {
    for (const container of [result as Record<string, unknown>, settings]) {
      for (const key of Object.keys(container)) {
        if (key.toLowerCase() === 'sessiondata') container[key] = overrideFields(container[key], entries);
      }
    }
  };
  if (engine === 'visualization') {
    const entries: Record<string, number> = {};
    if (validated['sceneDurationSec'] !== undefined) entries['duration'] = validated['sceneDurationSec'];
    if (validated['stepDurationMs'] !== undefined) entries['stepDurationMs'] = validated['stepDurationMs'];
    const containers = [result as Record<string, unknown>, settings];
    for (const container of [...containers]) {
      for (const key of Object.keys(container)) if (key.toLowerCase() === 'sessiondata') containers.push(recordOrEmpty(container[key]));
    }
    for (const container of containers) {
      for (const key of Object.keys(container)) {
        if (key.toLowerCase() === 'scenes' && Array.isArray(container[key])) {
          container[key] = (container[key] as unknown[]).map(scene => overrideFields(scene, entries));
        }
      }
    }
    return result;
  }
  if (engine === 'grid_interaction') {
    if (validated['gridSize'] !== undefined) {
      (result as Record<string, unknown>)['gridSize'] = validated['gridSize'];
      settings['gridSize'] = validated['gridSize'];
    }
    return result;
  }
  if (engine === 'focus' || engine === 'vocabulary_builder') {
    const key = engine === 'focus' ? 'SpeedMs' : 'timeLimitPerWord';
    const value = validated[engine === 'focus' ? 'speedMs' : key];
    if (value !== undefined) {
      settings[key] = value;
      updateSessions({ [key]: value });
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
    updateSessions(overrides);
  }
  return result;
}
