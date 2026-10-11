import { ExercisePlayerComponent } from './exercise-player.component';

describe('Reading preview age contract with real API field names', () => {
  const matches = (engine: string, exercise: Record<string, unknown>, age?: string): boolean =>
    (ExercisePlayerComponent.prototype as any).previewTextMatchesAge.call({
      parsedConfig: { engineType: engine }, exercise
    }, { targetAgeGroupConfigurationId: age });

  it('accepts prepared proofreading text for the canonical API exercise age', () => {
    expect(matches('error_analysis', { targetAgeGroupConfigurationId: 'teen' }, 'teen')).toBeTrue();
  });
  it('rejects proofreading text from another age and shared unscoped text', () => {
    expect(matches('error_analysis', { targetAgeGroupConfigurationId: 'teen' }, 'child')).toBeFalse();
    expect(matches('error_analysis', { targetAgeGroupConfigurationId: 'teen' })).toBeFalse();
  });
  for (const engine of ['free_reading', 'text_stream', 'reading_comprehension']) {
    it(`uses canonical exercise age for ${engine}, permitting shared but not other-age text`, () => {
      expect(matches(engine, { targetAgeGroupConfigurationId: 'adult' }, 'child')).toBeFalse();
      expect(matches(engine, { targetAgeGroupConfigurationId: 'adult' }, 'adult')).toBeTrue();
      expect(matches(engine, { targetAgeGroupConfigurationId: 'adult' })).toBeTrue();
    });
  }
  it('retains the legacy exercise age alias without overriding the canonical field', () => {
    expect(matches('error_analysis', { targetAgeGroupId: 'child' }, 'child')).toBeTrue();
    expect(matches('free_reading', { targetAgeGroupConfigurationId: 'adult', targetAgeGroupId: 'child' }, 'child')).toBeFalse();
    expect(matches('free_reading', { targetAgeGroupConfigurationId: 'adult', targetAgeGroupId: 'child' }, 'adult')).toBeTrue();
  });
});
