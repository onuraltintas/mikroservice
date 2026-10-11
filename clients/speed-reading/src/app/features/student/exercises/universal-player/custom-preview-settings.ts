import { boundedInteger, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './engines/reading-pacer-safety';
import { resolveSkimmingTiming } from './engines/skimming.engine';
import { resolveTrackingCycleMs } from './engines/motion-path.engine';

export interface CustomPreviewContext {
  roles: readonly string[];
  preview: boolean;
}

export const CUSTOM_PREVIEW_ENGINES = ['word_highlight', 'subvocalization_reduction', 'regression_reduction', 'text_fade', 'text_stream', 'motion_path', 'scan_find', 'scanning', 'skimming', 'focus', 'vocabulary_builder', 'visual_expansion', 'reading_comprehension', 'free_reading', 'exam_simulation', 'grid_interaction', 'visualization', 'adaptive_fluency', 'error_analysis'] as const;

export interface PreviewControl {
  key: string;
  label: string;
  min: number;
  max: number;
  value: number | string;
  step?: number;
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
  const session = ['focus', 'vocabulary_builder', 'subvocalization_reduction', 'regression_reduction', 'visualization', 'adaptive_fluency'].includes(String(configuration['engineType']))
    ? recordOrEmpty(caseInsensitiveField(settings, 'sessionData') ?? caseInsensitiveField(configuration, 'sessionData')) : {};
  const difficulty = recordOrEmpty(caseInsensitiveField(session, 'difficultySettings') ?? caseInsensitiveField(settings, 'difficultySettings') ?? caseInsensitiveField(configuration, 'difficultySettings'));
  const read = (name: string) => caseInsensitiveField(session, name)
    ?? (configuration['engineType'] === 'subvocalization_reduction' ? caseInsensitiveField(difficulty, name) : undefined)
    ?? caseInsensitiveField(settings, name) ?? caseInsensitiveField(configuration, name);
  const control = (key: string, label: string, min: number, max: number, value: unknown): PreviewControl =>
    ({ key, label, min, max, value: Number(value) });
  switch (configuration['engineType']) {
    case 'text_fade': {
      const visuals = mergeCaseInsensitiveRecords(configuration, settings, 'visuals');
      const size = String(visuals['fontsize'] ?? 'medium').toLowerCase();
      const named: Record<string, number> = { small: 16, medium: 20, large: 24 };
      const pixels = /^\d+px$/.test(size) ? boundedInteger(Number.parseInt(size, 10), 20, 14, 48)
        : Object.hasOwn(named, size) ? named[size] : 20;
      return [
        control('speedWpm', 'Gösterim temposu (kelime/dakika)', 20, 1500, read('targetWpm') ?? fading['speedwpm'] ?? 200),
        control('lagMs', 'Başlangıç beklemesi (ms)', 0, 10000, read('lagMs') ?? fading['lagms'] ?? 3000),
        control('timeLimitSec', 'Süre sınırı (saniye; 0: sınırsız)', 0, 3600, timing['timelimitsec'] ?? 0),
        control('fontSizePx', 'Metin boyutu (px)', 14, 48, pixels)
      ];
    }
    case 'adaptive_fluency': return [control('adaptiveTargetWpm', 'Önizleme hedef hızı (kelime/dakika)', 20, 1500, read('adaptiveTargetWpm') ?? 200)];
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
    case 'error_analysis':
    case 'reading_comprehension':
    case 'free_reading':
    case 'exam_simulation': {
      const display = mergeCaseInsensitiveRecords(configuration, settings, 'display');
      const controls: PreviewControl[] = [{ key: 'fontSize', label: 'Metin boyutu', min: 0, max: 0, value: String(display['fontsize'] ?? 'medium').toLowerCase(),
        options: [{ value: 'small', label: 'Küçük' }, { value: 'medium', label: 'Orta' }, { value: 'large', label: 'Büyük' }] }];
      if (configuration['engineType'] === 'error_analysis') {
        controls.push(control('timeLimitSeconds', 'Çalışma süresi (saniye)', 15, 3600,
          read('timeLimitSeconds') ?? read('timeLimit') ?? timing['timelimitsec'] ?? 180));
        const map = recordOrEmpty(caseInsensitiveField(settings, 'errorsByTextId'));
        const counts = Object.values(map).filter(Array.isArray).map(entries => entries.length);
        if (counts.length) controls.push(control('errorCount', 'Bulunacak hata sayısı', 1, Math.min(...counts),
          caseInsensitiveField(settings, 'errorCount') ?? Math.min(...counts)));
      }
      if (['reading_comprehension', 'free_reading'].includes(String(configuration['engineType']))) controls.push(
        { ...control('minReadingTimeSec', 'Minimum okuma süresi (saniye)', 0, 3600, Number(timing['minreadingtimems'] ?? 0) / 1000), step: 0.001 },
        { ...control('maxReadingTimeSec', 'Maksimum okuma süresi (saniye; 0: sınırsız)', 0, 3600, Number(timing['maxreadingtimems'] ?? 0) / 1000), step: 0.001 },
        control('lineHeightPercent', 'Satır aralığı (%)', 100, 300, Math.round(Number(display['lineheight'] ?? 1.8) * 100))
      );
      if (configuration['engineType'] === 'exam_simulation') controls.push(
        control('maxReadingTimeSec', 'İlk okuma süresi sınırı (saniye)', 1, 3600, Number(timing['maxreadingtimems'] ?? 180000) / 1000),
        control('questionTimeSeconds', 'Soru başına süre (saniye)', 1, 3600, timing['questiontimeseconds'] ?? 60),
        control('lineHeightPercent', 'Satır aralığı (%)', 100, 300, Math.round(Number(display['lineheight'] ?? 1.8) * 100))
      );
      return controls;
    }
    case 'focus': return [
      control('speedMs', 'Uyaran süresi (ms)', 100, 10000, read('SpeedMs') ?? read('FocusSpeedMs') ?? 1500),
      control('nLevel', 'N-back adım sayısı', 1, 5, read('NLevel') ?? read('FocusNLevel') ?? 1),
      control('gridSize', 'Tablo boyutu', 3, 7, read('GridSize') ?? 3),
      control('totalSteps', 'Uyaran sayısı', 2, 500, read('TotalSteps') ?? Math.max(
        Array.isArray(read('PositionSequence')) ? (read('PositionSequence') as unknown[]).length : 0,
        Array.isArray(read('WordSequence')) ? (read('WordSequence') as unknown[]).length : 0, 20)),
      { key: 'mode', label: 'Çalışma modu', min: 0, max: 0, value: String(read('Mode') ?? read('FocusMode') ?? 'position'),
        options: [{ value: 'position', label: 'Konum' }, { value: 'word', label: 'Kelime' }, { value: 'dual', label: 'Konum ve kelime' }] }
    ];
    case 'vocabulary_builder': return [
      { key: 'mode', label: 'Çalışma modu', min: 0, max: 0, value: read('mode') === 'quiz' ? 'quiz' : 'learning',
        options: [{ value: 'learning', label: 'Öğren ve aynı kelimelerle test ol' }, { value: 'quiz', label: 'Doğrudan test' }] },
      control('wordCount', 'Kelime sayısı', 4, 50, recordOrEmpty(read('vocabulary'))['count'] ?? 10),
      { key: 'quizType', label: 'Test sorularının yönü', min: 0, max: 0, value: String(read('quizType') ?? 'mixed'),
        options: [{ value: 'word_to_definition', label: 'Kelime → anlam' }, { value: 'definition_to_word', label: 'Anlam → kelime' }, { value: 'mixed', label: 'Karışık' }] },
      control('timeLimitPerWord', 'Testte kelime başına süre (saniye; 0: sınırsız)', 0, 3600, read('timeLimitPerWord') ?? 0)
    ];
    case 'visual_expansion': return [
      control('rounds', 'Tur sayısı', 1, 100, read('rounds') ?? 20),
      control('displayDurationMs', 'Gösterim süresi (ms)', 100, 5000, read('displayDurationMs') || timing['durationms'] || read('VisualExpansionDisplayDurationMs') || 250),
      control('intervalMs', 'Gösterimler arası bekleme (ms)', 50, 10000, timing['intervalms'] || 1500)
    ];
    case 'text_stream': {
      const isRsvp = String(read('mode')).toLowerCase() === 'rsvp';
      const content = mergeCaseInsensitiveRecords(configuration, settings, 'content');
      const adaptive = mergeCaseInsensitiveRecords(configuration, settings, 'adaptive');
      const adapts = !isRsvp && adaptive['enabled'] !== false;
      return [
      control('displayDurationMs', 'Başlangıç gösterim süresi (ms)',
        adapts ? Number(adaptive['mindurationms'] ?? 50) : 50,
        adapts ? Number(adaptive['maxdurationms'] ?? 5000) : 5000,
        read('displayDurationMs') ?? read('intervalMs') ?? timing['durationms'] ?? 500),
      control('intervalMs', 'Gösterimler arası bekleme (ms)', 0, 10000, timing['intervalms'] ?? 0),
      ...(isRsvp ? [] : [control('stimulusCount', 'Uyaran sayısı', 1, 500,
        read('totalStimuli') ?? caseInsensitiveField(recordOrEmpty(read('content')), 'count') ?? 20)]),
      ...(!isRsvp && content['type'] === 'number' && content['source'] !== 'custom'
        ? [control('stimulusLength', 'Başlangıç basamak sayısı', Number(adaptive['minlength'] ?? 1),
          Number(adaptive['maxlength'] ?? 12), content['length'] ?? boundedInteger(configuration['difficultyLevel'], 1, 1, 5) + 2)] : [])
    ];
    }
    case 'motion_path': {
      const mode = String(read('mode') ?? (String(read('exerciseTypeName')).toLowerCase() === 'eyetracking' ? 'tracking' : 'fixation')).toLowerCase();
      if (mode === 'tracking') {
        const path = mergeCaseInsensitiveRecords(configuration, settings, 'path');
        const content = mergeCaseInsensitiveRecords(configuration, settings, 'content');
        const durationMs = boundedInteger(timing['durationms'], 0, 0, 3600000);
        return [control('cycleDurationMs', 'Hareket döngüsü / geçiş aralığı (ms)', 50, 60000, resolveTrackingCycleMs(configuration)),
          control('durationSeconds', 'Takip süresi (saniye)', 5, 3600, durationMs > 0
            ? Math.ceil(Math.max(5000, durationMs) / 1000) : boundedInteger(timing['totaldurationseconds'] ?? timing['durationseconds'], 60, 5, 3600)),
          { key: 'pathType', label: 'Hareket yolu', min: 0, max: 0, value: String(path['type'] ?? 'horizontal').toLowerCase(),
            options: [{ value: 'horizontal', label: 'Yatay' }, { value: 'vertical', label: 'Dikey' },
              { value: 'circle', label: 'Daire' }, { value: 'infinity8', label: 'Sekiz' },
              { value: 'random_point', label: 'Rastgele noktalar (sıçramalı)' }, { value: 'two_point_jump', label: 'İki nokta (sıçramalı)' }] },
          control('pointSize', 'Nokta boyutu (px)', 8, 200, content['pointsize'] ?? 36)];
      }
      if (mode === 'saccade') return [control('jumpIntervalMs', 'Hedef geçiş aralığı (ms)', 50, 10000,
        movement['jumpintervalms'] ?? timing['holdms'] ?? movement['fixationtimems'] ?? 1000)];
      const content = mergeCaseInsensitiveRecords(configuration, settings, 'content');
      const fixation = mergeCaseInsensitiveRecords(configuration, settings, 'fixation');
      const hold = control('holdMs', 'Gösterim süresi (ms)', 50, 10000, timing['holdms'] ?? movement['fixationtimems'] ?? 2000);
      if (Number(content['peripheralcount'] ?? fixation['peripheralcount'] ?? 0) === 0) return [hold];
      const maxLetters = boundedInteger(content['maxperipheralcount'] ?? fixation['maxperipheralcount'], 4, 1, 4);
      return [control('points', 'Tur sayısı', 1, 100, content['points'] ?? fixation['points'] ?? 10),
        control('peripheralCount', 'Her turdaki harf sayısı', 1, maxLetters, content['peripheralcount'] ?? fixation['peripheralcount']), hold];
    }
    case 'skimming': return [
      control('timeLimitSec', 'İnceleme süresi sınırı (saniye)', Math.floor(resolveSkimmingTiming(configuration).minimumMs / 1000) + 1, 3600,
        resolveSkimmingTiming(configuration).maximumMs / 1000),
      control('fontSizePx', 'Metin boyutu (px)', 14, 48, Number.parseInt(String(caseInsensitiveField(recordOrEmpty(read('visuals')), 'fontSize') ?? '20'), 10) || 20)
    ];
    case 'scanning':
    case 'scan_find': return [
      control('timeLimitSec', 'Süre sınırı (saniye)', 1, 3600, read('timeLimitSeconds') ?? read('timeLimit') ?? timing['timelimitsec'] ?? 90),
      control('targetCount', 'Hedef sözcük sayısı', 1, 100, read('targetCount') ?? 3),
      control('fontSizePx', 'Metin boyutu (px)', 14, 48, Number.parseInt(String(caseInsensitiveField(recordOrEmpty(read('visuals')), 'fontSize') ?? '20'), 10) || 20)
    ];
    default: {
      if (!CUSTOM_PREVIEW_ENGINES.some(type => type === configuration['engineType'])) return [];
      const grouping = configuration['engineType'] === 'word_highlight';
      const content = mergeCaseInsensitiveRecords(configuration, settings, 'content');
      const groupSize = read('chunkSize') ?? pacer['chunksize'] ?? (grouping ? content['chunksize'] : undefined) ?? 1;
      const duration = boundedInteger(timing['durationms'], 0, 0, 10000);
      const legacyTempo = grouping && duration > 0
        ? Math.max(20, Math.min(1500, Math.round(60000 * boundedInteger(groupSize, 1, 1, 10) /
          (duration + boundedInteger(timing['delayms'], 0, 0, 10000)))))
        : configuration['engineType'] === 'subvocalization_reduction'
          ? Math.round(60000 / boundedInteger(read('msPerWord'), 300, 40, 3000)) : 200;
      const pace = grouping ? read('targetWpm') ?? pacer['speedwpm'] ?? legacyTempo
        : read('targetWpm') ?? read('wpm') ?? pacer['speedwpm'] ?? fading['speedwpm'] ?? legacyTempo;
      const controls = [control('speedWpm', grouping || ['subvocalization_reduction', 'regression_reduction'].includes(String(configuration['engineType']))
        ? 'Gösterim temposu (kelime/dakika)' : 'Okuma hızı (kelime/dakika)', 20, 1500, pace)];
      const speedReading = grouping && ['guided', 'speed_reading'].includes(String(read('mode')));
      if (configuration['engineType'] !== 'text_fade' && !speedReading) controls.push(control('chunkSize', 'Kelime grubu', 1, 10, groupSize));
      if (configuration['engineType'] === 'regression_reduction') controls.push(
        control('visibleHistoryWords', 'Okunabilir geçmiş kelime sayısı', 0, 2, read('visibleHistoryWords') ?? 0));
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
  if (engine === 'vocabulary_builder') {
    const overrides: Record<string, unknown> = {};
    for (const control of controls) {
      const value = values[control.key];
      if (value === undefined) continue;
      if (control.options ? !control.options.some(option => option.value === value)
        : typeof value !== 'number' || !Number.isInteger(value) || value < control.min || value > control.max)
        throw new Error(`${control.label} için geçerli bir değer seçin.`);
      overrides[control.key] = value;
    }
    if (!Object.keys(overrides).length) return configuration;
    const result = structuredClone(configuration);
    const settings = recordOrEmpty(result['engineConfig']);
    const mode = overrides['mode'] ?? caseInsensitiveField(settings, 'mode') ?? caseInsensitiveField(result, 'mode') ?? 'learning';
    delete overrides['wordCount'];
    overrides['mode'] = mode;
    overrides['learnThenQuiz'] = mode === 'learning';
    overrides['vocabulary'] = overrideFields(caseInsensitiveField(settings, 'vocabulary') ?? caseInsensitiveField(result, 'vocabulary'),
      { ...(values['wordCount'] === undefined ? {} : { count: values['wordCount'] }), requireFullPool: true });
    // Preview uses a freshly fetched pool, not a persisted session snapshot or pinned word list.
    for (const container of [result as Record<string, unknown>, settings]) {
      for (const key of Object.keys(container)) if (['sessiondata', 'vocabularyitemids'].includes(key.toLowerCase())) delete container[key];
    }
    overrides['vocabulary'] = overrideFields(overrides['vocabulary'], { itemIds: [] });
    (result as Record<string, unknown>)['engineConfig'] = overrideFields(settings, overrides);
    return result;
  }
  const fontControl = controls.find(control => control.key === 'fontSize');
  if (fontControl) {
    const value = values['fontSize'];
    const numeric: Record<string, number> = {};
    for (const { key, min, max, label, step } of controls.filter(control => !control.options)) {
      const input = values[key];
      if (input === undefined) continue;
      const scaled = typeof input === 'number' ? input * (step === 0.001 ? 1000 : 1) : NaN;
      if (typeof input !== 'number' || !Number.isFinite(input) || Math.abs(scaled - Math.round(scaled)) > 1e-7 || input < min || input > max)
        throw new Error(`${label} için geçerli bir değer girin (${min}-${max}).`);
      numeric[key] = input;
    }
    if (value === undefined && !Object.keys(numeric).length) return configuration;
    if (value !== undefined && !fontControl.options?.some(option => option.value === value)) throw new Error('Geçerli bir metin boyutu seçin.');
    const result = structuredClone(configuration);
    const settings = recordOrEmpty(result['engineConfig']);
    (result as Record<string, unknown>)['engineConfig'] = settings;
    if (engine === 'error_analysis') {
      if (numeric['timeLimitSeconds'] !== undefined) {
        Object.assign(settings, overrideFields(settings, { timeLimit: numeric['timeLimitSeconds'], timeLimitSeconds: numeric['timeLimitSeconds'] }));
        const session = caseInsensitiveField(result, 'sessionData');
        if (session && typeof session === 'object') (result as Record<string, unknown>)['sessionData'] = overrideFields(session, { timeLimitSeconds: numeric['timeLimitSeconds'] });
      }
      if (numeric['errorCount'] !== undefined) {
        settings['errorCount'] = numeric['errorCount'];
        const map = recordOrEmpty(settings['errorsByTextId']);
        settings['errorsByTextId'] = Object.fromEntries(Object.entries(map).map(([id, entries]) =>
          [id, Array.isArray(entries) ? entries.slice(0, numeric['errorCount']) : entries]));
      }
    }
    const displayOverrides: Record<string, unknown> = {};
    if (value !== undefined) displayOverrides['fontSize'] = value;
    if (numeric['lineHeightPercent'] !== undefined) displayOverrides['lineHeight'] = numeric['lineHeightPercent'] / 100;
    if (numeric['questionTimeSeconds'] !== undefined) settings['timing'] = overrideFields(
      { ...recordOrEmpty(caseInsensitiveField(configuration, 'timing')), ...recordOrEmpty(caseInsensitiveField(settings, 'timing')) },
      { questionTimeSeconds: numeric['questionTimeSeconds'] });
    if (Object.keys(displayOverrides).length) settings['display'] = overrideFields({ ...recordOrEmpty(caseInsensitiveField(configuration, 'display')), ...recordOrEmpty(caseInsensitiveField(settings, 'display')) }, displayOverrides);
    if (numeric['minReadingTimeSec'] !== undefined || numeric['maxReadingTimeSec'] !== undefined) {
      const timing = mergeCaseInsensitiveRecords(configuration, settings, 'timing');
      const minimum = numeric['minReadingTimeSec'] === undefined ? Number(timing['minreadingtimems'] ?? 0) : Math.round(numeric['minReadingTimeSec'] * 1000);
      const maximum = numeric['maxReadingTimeSec'] === undefined ? Number(timing['maxreadingtimems'] ?? 0) : Math.round(numeric['maxReadingTimeSec'] * 1000);
      if (engine === 'exam_simulation' && maximum <= minimum) throw new Error('İlk okuma süresi minimum süreden uzun olmalıdır.');
      if (maximum > 0 && maximum < minimum) throw new Error('Maksimum okuma süresi minimum süreden kısa olamaz.');
      settings['timing'] = overrideFields({ ...recordOrEmpty(caseInsensitiveField(configuration, 'timing')), ...recordOrEmpty(caseInsensitiveField(settings, 'timing')) }, { minReadingTimeMs: minimum, maxReadingTimeMs: maximum });
    }
    return result;
  }
  const validated: Record<string, number> = {};
  for (const { key, min, max, label } of controls) {
    if (engine === 'motion_path' && key === 'pathType') continue;
    if (engine === 'focus' && key === 'mode') continue;
    if (values[key] === undefined) continue;
    if (key === 'chunkSize' && engine === 'text_fade') continue;
    const value = values[key];
    if (typeof value !== 'number' || !Number.isInteger(value) || value < min || value > max) {
      throw new Error(`${label} için geçerli bir değer girin (${min}–${max}).`);
    }
    validated[key] = value;
  }
  const modeControl = engine === 'focus' ? controls.find(control => control.key === 'mode') : undefined;
  if (modeControl && values['mode'] !== undefined && !modeControl.options?.some(option => option.value === values['mode']))
    throw new Error('Geçerli bir çalışma modu seçin.');
  if (!Object.keys(validated).length && values['mode'] === undefined && !(engine === 'motion_path' && values['pathType'] !== undefined)) return configuration;
  const result = structuredClone(configuration);
  const nested = result['engineConfig'];
  const settings: Record<string, unknown> = nested && typeof nested === 'object' && !Array.isArray(nested)
    ? nested as Record<string, unknown> : {};
  (result as Record<string, unknown>)['engineConfig'] = settings;
  const merge = (name: string, entries: Record<string, number>) => {
    const original = { ...recordOrEmpty(caseInsensitiveField(configuration, name)), ...recordOrEmpty(caseInsensitiveField(settings, name)) };
    settings[name] = overrideFields(original, entries);
  };
  const updateSessions = (entries: Record<string, unknown>) => {
    for (const container of [result as Record<string, unknown>, settings]) {
      for (const key of Object.keys(container)) {
        if (key.toLowerCase() === 'sessiondata') container[key] = overrideFields(container[key], entries);
      }
    }
  };
  if (engine === 'text_fade') {
    if (validated['speedWpm'] !== undefined) {
      settings['targetWpm'] = validated['speedWpm'];
      merge('fading', { speedWpm: validated['speedWpm'] });
    }
    if (validated['lagMs'] !== undefined) {
      settings['lagMs'] = validated['lagMs'];
      merge('fading', { lagMs: validated['lagMs'] });
    }
    if (validated['timeLimitSec'] !== undefined) merge('timing', { timeLimitSec: validated['timeLimitSec'] });
    if (validated['fontSizePx'] !== undefined) settings['visuals'] = overrideFields(
      { ...recordOrEmpty(caseInsensitiveField(configuration, 'visuals')), ...recordOrEmpty(caseInsensitiveField(settings, 'visuals')) },
      { fontSize: `${validated['fontSizePx']}px` });
    return result;
  }
  if (engine === 'adaptive_fluency') {
    if (validated['adaptiveTargetWpm'] !== undefined) {
      settings['adaptiveTargetWpm'] = validated['adaptiveTargetWpm'];
      updateSessions({ adaptiveTargetWpm: validated['adaptiveTargetWpm'] });
    }
    return result;
  }
  if (engine === 'visualization') {
    const session = recordOrEmpty(caseInsensitiveField(settings, 'sessionData') ?? caseInsensitiveField(result, 'sessionData'));
    const guided = (caseInsensitiveField(session, 'mode') ?? caseInsensitiveField(settings, 'mode') ?? caseInsensitiveField(result, 'mode')) === 'guided';
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
          container[key] = (container[key] as unknown[]).map(scene => {
            const steps = caseInsensitiveField(recordOrEmpty(scene), 'steps');
            const field = guided && Array.isArray(steps) && steps.length ? 'stepDurationMs' : 'duration';
            return overrideFields(scene, entries[field] === undefined ? {} : { [field]: entries[field] });
          });
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
    if (engine === 'focus' && (values['mode'] !== undefined || ['nLevel', 'gridSize', 'totalSteps'].some(key => validated[key] !== undefined))) {
      const originalSession = recordOrEmpty(caseInsensitiveField(settings, 'sessionData') ?? caseInsensitiveField(result, 'sessionData'));
      const read = (name: string) => caseInsensitiveField(originalSession, name) ?? caseInsensitiveField(settings, name) ?? caseInsensitiveField(result, name);
      const mode = String(values['mode'] ?? modeControl?.value ?? 'position');
      const nLevel = validated['nLevel'] ?? Number(read('NLevel') ?? read('FocusNLevel') ?? 1);
      const gridSize = validated['gridSize'] ?? Number(read('GridSize') ?? 3);
      const count = validated['totalSteps'] ?? Math.max(nLevel + 1, Number(controls.find(control => control.key === 'totalSteps')?.value ?? 20));
      for (const [value, minimum, maximum] of [[nLevel, 1, 5], [gridSize, 3, 7], [count, 2, 500]]) {
        if (!Number.isInteger(value) || value < minimum || value > maximum)
          throw new Error('Odaklanma ayarları geçerli sınırlar içinde olmalıdır.');
      }
      if (count <= nLevel) throw new Error('Uyaran sayısı N-back adım sayısından büyük olmalıdır.');
      const configuredWords = read('WordSequence');
      const wordPool = Array.isArray(configuredWords) ? [...new Set(configuredWords.filter((word): word is string => typeof word === 'string' && word.trim().length > 0))] : [];
      // Preview stimuli only; normal sessions continue to use server-owned sequences.
      const pool = wordPool.length > 1 ? wordPool : ['kitap', 'kalem', 'masa', 'bulut', 'deniz', 'orman'];
      const create = <T>(choices: T[], limitPositionRepeats = false): T[] => {
        const sequence: T[] = [];
        for (let index = 0; index < count; index++) {
          const allows = (choice: T) => !limitPositionRepeats || index === 0 || choice !== sequence[index - 1]
            || (nLevel === 1 && (index < 2 || sequence[index - 1] !== sequence[index - 2]));
          if (index >= nLevel && (index === nLevel || Math.random() < 0.25) && allows(sequence[index - nLevel])) sequence.push(sequence[index - nLevel]);
          else {
            const candidates = choices.filter(choice => allows(choice) && (index < nLevel || choice !== sequence[index - nLevel])
              && (!limitPositionRepeats || nLevel === 1 || index !== nLevel - 1 || choice !== sequence[0]));
            sequence.push(candidates[Math.floor(Math.random() * candidates.length)]);
          }
        }
        return sequence;
      };
      const entries = { Mode: mode, FocusMode: mode, NLevel: nLevel, FocusNLevel: nLevel, GridSize: gridSize,
        TotalSteps: count, PositionSequence: mode === 'word' ? [] : create(Array.from({ length: gridSize * gridSize }, (_, index) => index + 1), true),
        WordSequence: mode === 'position' ? [] : create(pool), WordTargetIndices: [], PositionTargetIndices: [] };
      Object.assign(settings, overrideFields(settings, entries));
      updateSessions(entries);
      // Lowercase duplicates must not override effective preview values in the player.
      for (const name of Object.keys(entries)) {
        for (const key of Object.keys(settings)) if (key !== name && key.toLowerCase() === name.toLowerCase()) delete settings[key];
      }
    }
    const key = engine === 'focus' ? 'SpeedMs' : 'timeLimitPerWord';
    const value = validated[engine === 'focus' ? 'speedMs' : key];
    if (value !== undefined) {
      settings[key] = value;
      updateSessions({ [key]: value });
    }
    return result;
  }
  if (engine === 'visual_expansion') {
    if (validated['rounds'] !== undefined) settings['rounds'] = validated['rounds'];
    if (validated['displayDurationMs'] !== undefined) {
      merge('timing', { durationMs: validated['displayDurationMs'] });
      settings['displayDurationMs'] = validated['displayDurationMs'];
      Object.assign(result, { displayDurationMs: validated['displayDurationMs'] });
    }
    if (validated['intervalMs'] !== undefined) merge('timing', { intervalMs: validated['intervalMs'] });
    return result;
  }
  if (engine === 'text_stream') {
    if (validated['displayDurationMs'] !== undefined) {
      for (const key of Object.keys(settings))
        if (key.toLowerCase() === 'displaydurationms') delete settings[key];
      settings['displayDurationMs'] = validated['displayDurationMs'];
      if (mergeCaseInsensitiveRecords(configuration, settings, 'timing')['durationms'] !== undefined)
        merge('timing', { durationMs: validated['displayDurationMs'] });
    }
    if (validated['stimulusLength'] !== undefined) merge('content', { length: validated['stimulusLength'] });
    if (validated['intervalMs'] !== undefined) merge('timing', { intervalMs: validated['intervalMs'] });
    if (validated['stimulusCount'] !== undefined) {
      settings['totalStimuli'] = validated['stimulusCount'];
      merge('content', { count: validated['stimulusCount'] });
    }
    return result;
  }
  if (engine === 'motion_path') {
    if (values['pathType'] !== undefined && !controls.find(control => control.key === 'pathType')?.options?.some(option => option.value === values['pathType']))
      throw new Error('Geçerli bir hareket yolu seçin.');
    if (validated['durationSeconds'] !== undefined) {
      merge('timing', { durationSeconds: validated['durationSeconds'] });
      const timingConfig = recordOrEmpty(settings['timing']);
      for (const key of Object.keys(timingConfig)) if (['durationms', 'totaldurationseconds'].includes(key.toLowerCase())) delete timingConfig[key];
      if (caseInsensitiveField(configuration, 'timing') !== undefined) {
        const inherited = recordOrEmpty(caseInsensitiveField(configuration, 'timing'));
        if (caseInsensitiveField(inherited, 'durationMs') !== undefined) timingConfig['durationMs'] = validated['durationSeconds'] * 1000;
        if (caseInsensitiveField(inherited, 'totalDurationSeconds') !== undefined) timingConfig['totalDurationSeconds'] = validated['durationSeconds'];
      }
    }
    if (values['pathType'] !== undefined) settings['path'] = overrideFields(
      { ...recordOrEmpty(caseInsensitiveField(configuration, 'path')), ...recordOrEmpty(caseInsensitiveField(settings, 'path')) }, { type: values['pathType'] });
    if (validated['pointSize'] !== undefined) merge('content', { pointSize: validated['pointSize'] });
    if (validated['holdMs'] !== undefined) merge('timing', { holdMs: validated['holdMs'] });
    if (validated['points'] !== undefined) merge('content', { points: validated['points'] });
    if (validated['peripheralCount'] !== undefined) merge('content', { peripheralCount: validated['peripheralCount'] });
    if (validated['cycleDurationMs'] !== undefined) merge('timing', { speedMs: validated['cycleDurationMs'] });
    if (validated['jumpIntervalMs'] !== undefined) merge('movement', { jumpIntervalMs: validated['jumpIntervalMs'] });
    return result;
  }
  if (engine === 'skimming') {
    if (validated['timeLimitSec'] !== undefined) {
      const minimum = mergeCaseInsensitiveRecords(configuration, settings, 'timing')['minreadingtimems'] ?? 3000;
      if (validated['timeLimitSec'] * 1000 <= Number(minimum)) throw new Error('İnceleme süresi minimum süreden uzun olmalıdır.');
      merge('timing', { maxReadingTimeMs: validated['timeLimitSec'] * 1000 });
      settings['timeLimitSeconds'] = validated['timeLimitSec'];
    }
    if (validated['fontSizePx'] !== undefined) settings['visuals'] = overrideFields(recordOrEmpty(caseInsensitiveField(settings, 'visuals') ?? caseInsensitiveField(configuration, 'visuals')), { fontSize: `${validated['fontSizePx']}px` });
    return result;
  }
  if (engine === 'scan_find' || engine === 'scanning') {
    if (validated['timeLimitSec'] !== undefined) settings['timeLimitSeconds'] = validated['timeLimitSec'];
    if (validated['targetCount'] !== undefined) {
      settings['targetCount'] = validated['targetCount'];
      settings['targets'] = overrideFields(recordOrEmpty(caseInsensitiveField(settings, 'targets') ?? caseInsensitiveField(configuration, 'targets')), { words: [] });
      // Custom target count intentionally replaces the temporary preview targets, never the catalogue.
      for (const container of [result as Record<string, unknown>, settings]) {
        for (const key of Object.keys(container)) if (key.toLowerCase() === 'scanningrounds' && Array.isArray(container[key]))
          container[key] = (container[key] as unknown[]).map(round => overrideFields(round, { targets: [], foundTargets: [] }));
      }
    }
    if (validated['fontSizePx'] !== undefined) settings['visuals'] = overrideFields(recordOrEmpty(caseInsensitiveField(settings, 'visuals') ?? caseInsensitiveField(configuration, 'visuals')), { fontSize: `${validated['fontSizePx']}px` });
    return result;
  }
  if (validated['chunkSize'] !== undefined) settings['chunkSize'] = validated['chunkSize'];
  if (engine === 'regression_reduction' && validated['visibleHistoryWords'] !== undefined)
    settings['visibleHistoryWords'] = validated['visibleHistoryWords'];
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
    if (engine === 'regression_reduction' && validated['visibleHistoryWords'] !== undefined)
      overrides['visibleHistoryWords'] = validated['visibleHistoryWords'];
    if (engine === 'subvocalization_reduction') merge('difficultySettings', overrides);
    updateSessions(overrides);
  }
  return result;
}
