// Schulte sessions currently support numeric, ordered server-validated targets.
export interface SchulteSettings {
  gridSize: number;
  sequenceType: 'numeric';
  timeLimit: number | undefined;
  showHints: boolean;
  showFixationPoint: boolean;
  highlightOnClick: boolean;
}

function object(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown> : {};
}

export function resolveSchulteSettings(templateValue: unknown, sessionValue: unknown): SchulteSettings {
  const template = object(templateValue);
  const session = object(sessionValue);
  const sources = [object(session['engineConfig']), object(session['EngineConfig']), session,
    object(template['engineConfig']), template];
  const first = (read: (source: Record<string, unknown>) => unknown) => sources
    .map(read)
    .find(value => value !== undefined && value !== null);
  const seconds = first(source => {
    const timing = object(source['timing']);
    const ms = timing['maxReadingTimeMs'];
    return source['timeLimitSeconds'] ?? source['TimeLimitSeconds'] ?? source['timeLimit']
      ?? object(source['rules'])['timeLimit'] ?? timing['timeLimitSec']
      ?? (typeof ms === 'number' ? ms / 1000 : undefined);
  });
  const size = first(source => source['gridSize'] ?? object(source['grid'])['rows']);
  return {
    gridSize: typeof size === 'number' && Number.isInteger(size) && size >= 3 && size <= 7 ? size : 5,
    sequenceType: 'numeric',
    timeLimit: typeof seconds === 'number' && Number.isFinite(seconds) && seconds > 0 ? seconds : undefined,
    showHints: first(source => source['showHints']) !== false,
    showFixationPoint: first(source => source['showFixationPoint']) !== false,
    highlightOnClick: first(source => source['highlightOnClick'] ?? object(source['visuals'])['highlightCorrect']) !== false
  };
}
