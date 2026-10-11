import { SpeedReadingVisualizationScenesComponent } from './speed-reading-visualization-scenes';

describe('visualization admin answer format', () => {
  it('sends the selected option as explicit text even when options are A-D letters', () => {
    const component = Object.create(SpeedReadingVisualizationScenesComponent.prototype) as any;
    const request = component.toRequest({ exerciseId: 'exercise', description: 'Sahne', duration: 30,
      displayOrder: 0, difficultyLevel: 1, mode: 'assessment', questions: [
        { id: 'question', questionText: 'Hangi harf?', options: ['B', 'A', 'X', 'Y'],
          correctAnswer: 'B', questionType: 'detail', displayOrder: 0 }
      ] });
    expect(request.questions[0].answerFormat).toBe('text');
    expect(component.emptyQuestion(0).answerFormat).toBe('text');
  });
});
