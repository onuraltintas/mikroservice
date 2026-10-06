import { ExercisePlayerComponent } from './exercise-player.component';

describe('Skimming preview questions', () => {
  it('keeps only main-idea questions paired with the fetched text', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.parsedConfig = { engineType: 'skimming' };
    const config = player.createPreviewReadingConfig({ content: 'Metin', questions: [
      { id: 'main', type: 1 }, { id: 'detail', type: 3 }, { id: 'unknown' }
    ] });
    expect(config.questions.map((question: any) => question.id)).toEqual(['main']);
  });
});
