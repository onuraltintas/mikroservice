export const coachingGoalCategories: Readonly<Record<number, string>> = {
  1: 'ExamPreparation', 2: 'SubjectMastery', 3: 'GradeImprovement',
  4: 'StudyHabits', 5: 'TimeManagement', 99: 'Other'
};

// KM1 (01.11.2026): AGS (+ÖABT), YDS and YÖKDİL are appended; the stored values of the old types do not move.
export const coachingExamTypes: Readonly<Record<number, string>> = {
  1: 'Mock', 2: 'Weekly', 3: 'Monthly', 4: 'LGS',
  5: 'YKS', 6: 'MidTerm', 7: 'Final', 8: 'Quiz',
  9: 'AGS', 10: 'YDS', 11: 'YOKDIL'
};

/** The exam types staff can pick, with the standardized exams together. */
export const coachingExamTypeOptions: readonly { value: number; key: string; label: string }[] = [
  { value: 1, key: 'Mock', label: 'Deneme' },
  { value: 2, key: 'Weekly', label: 'Haftalık test' },
  { value: 3, key: 'Monthly', label: 'Aylık değerlendirme' },
  { value: 4, key: 'LGS', label: 'LGS' },
  { value: 5, key: 'YKS', label: 'YKS' },
  { value: 9, key: 'AGS', label: 'AGS / ÖABT' },
  { value: 10, key: 'YDS', label: 'YDS' },
  { value: 11, key: 'YOKDIL', label: 'YÖKDİL' },
  { value: 6, key: 'MidTerm', label: 'Ara sınav' },
  { value: 7, key: 'Final', label: 'Final' },
  { value: 8, key: 'Quiz', label: 'Kısa sınav' }
];
