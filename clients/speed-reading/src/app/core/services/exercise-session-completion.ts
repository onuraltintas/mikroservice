import { CompleteSessionRequest, QuestionAnswerDto } from '../models/exercise-session.model';

export interface LocalQuestionAnswer {
  questionId: string;
  selectedAnswer: string;
  isCorrect: boolean;
  timeSpent: number;
  bloomLevel?: number;
}

export function toCompleteSessionRequest(
  answers: readonly LocalQuestionAnswer[],
  customData: { [key: string]: any },
  isAssessmentMode: boolean
): CompleteSessionRequest {
  const questionAnswers: QuestionAnswerDto[] = answers.map(answer => ({
    questionId: answer.questionId,
    answer: answer.selectedAnswer,
    isCorrect: answer.isCorrect,
    timeSpentSeconds: Math.max(0, Math.round(answer.timeSpent)),
    bloomLevel: Math.max(0, answer.bloomLevel ?? 0)
  }));

  return {
    questionAnswers,
    customData,
    isAssessmentMode
  };
}
