export function resolveVisualExpansionSettings(
  catalog: Record<string, any>, session: Record<string, any> = {}
): Record<string, number> {
  const configuration = catalog['engineConfig'] ?? {};
  const owned = session['EngineConfig'] ?? session['engineConfig'] ?? {};
  const result: Record<string, number> = {};
  for (const key of ['rounds', 'startDegrees', 'targetDegrees', 'displayDurationMs']) {
    const capitalized = key[0].toUpperCase() + key.slice(1);
    const sessionKey = key === 'rounds' ? key : `visualExpansion${capitalized}`;
    const nested = key === 'displayDurationMs' ? configuration.timing?.durationMs
      : configuration.expansion?.[key];
    const value = session[key] ?? session[capitalized] ?? session[sessionKey]
      ?? session[`VisualExpansion${capitalized}`] ?? owned[key]
      ?? nested ?? configuration[key] ?? catalog[key];
    if (value !== undefined) result[key] = Number(value);
  }
  return result;
}
