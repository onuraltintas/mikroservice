import { toCompleteSessionRequest } from './exercise-session-completion';

describe('toCompleteSessionRequest', () => {
  it('maps every local question answer to the backend completion contract', () => {
    const request = toCompleteSessionRequest([
      {
        questionId: 'question-1',
        selectedAnswer: 'B',
        isCorrect: true,
        timeSpent: 2.6,
        bloomLevel: 3
      },
      {
        questionId: 'question-2',
        selectedAnswer: '',
        isCorrect: false,
        timeSpent: -1
      }
    ], { engineType: 'reading_comprehension' }, false);

    expect(request.questionAnswers).toEqual([
      {
        questionId: 'question-1',
        answer: 'B',
        isCorrect: true,
        timeSpentSeconds: 3,
        bloomLevel: 3
      },
      {
        questionId: 'question-2',
        answer: '',
        isCorrect: false,
        timeSpentSeconds: 0,
        bloomLevel: 0
      }
    ]);
    expect(request.customData).toEqual({ engineType: 'reading_comprehension' });
    expect(request.isAssessmentMode).toBeFalse();
  });
});
