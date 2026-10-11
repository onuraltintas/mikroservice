import { ExercisePlayerComponent } from './exercise-player.component';

describe('Skimming preview questions', () => {
  it('enforces the configured text length band in preview', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.parsedConfig = { engineType: 'skimming', engineConfig: { content: { minWordCount: 200, maxWordCount: 280 } } };
    expect(player.previewTextMatchesLength(199)).toBeFalse();
    expect(player.previewTextMatchesLength(200)).toBeTrue();
    expect(player.previewTextMatchesLength(280)).toBeTrue();
    expect(player.previewTextMatchesLength(281)).toBeFalse();
  });
  it('keeps only main-idea questions paired with the fetched text', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.parsedConfig = { engineType: 'skimming' };
    const config = player.createPreviewReadingConfig({ content: 'Metin', questions: [
      { id: 'main', type: 1 }, { id: 'detail', type: 3 }, { id: 'unknown' }
    ] });
    expect(config.questions.map((question: any) => question.id)).toEqual(['main']);
  });
});
