import { CompleteSessionRequest, QuestionAnswerDto } from '../models/exercise-session.model';

export interface LocalQuestionAnswer {
  questionId: string;
  selectedAnswer: string;
  isCorrect: boolean;
  timeSpent: number;
  bloomLevel?: number;
}

export function toCompleteSessionRequest(
  answers: readonly LocalQuestionAnswer[] | null | undefined,
  customData: { [key: string]: any },
  isAssessmentMode: boolean
): CompleteSessionRequest {
  // `undefined` means the engine has already persisted its answers through
  // action validation.  Leaving the field absent makes the server use that
  // authoritative session state instead of treating an empty list as an
  // incomplete answer submission (visualization engine).
  const questionAnswers: QuestionAnswerDto[] | undefined = answers?.map(answer => ({
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
