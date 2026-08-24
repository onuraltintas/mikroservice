// Short reading text for RSVP practice
export interface ShortReadingText {
  id: string;
  title: string;
  content: string;
  wordCount: number;
  category?: string;
}

export enum QuestionType {
  Literal = 1,
  Inferential = 2,
  Evaluative = 3
}

// YENİ: Bloom's Taxonomy Seviyeleri
export enum BloomLevel {
  Remember = 1,      // Hatırlama
  Understand = 2,    // Anlama
  Apply = 3,         // Uygulama
  Analyze = 4,       // Analiz
  Evaluate = 5,      // Değerlendirme
  Create = 6         // Yaratma
}

export interface ReadingText {
  id: string;
  title: string;
  content: string;
  wordCount: number;
  difficultyLevel: number;
  category: string;
  language: string;
  isActive: boolean;
  createdAt: Date;
  updatedAt?: Date;
  questionCount: number;
  estimatedMinutes: number;
  readingQuestions?: ReadingQuestion[];

  // Client-side fields for tracking read status
  hasBeenRead?: boolean;
  lastReadDate?: Date;
}

export interface ReadingQuestion {
  id: string;
  readingTextId: string;
  questionText: string;
  type: QuestionType;
  typeDisplay: string;

  // YENİ: Bloom Taxonomy alanları
  bloomLevel: BloomLevel;
  bloomLevelDisplay: string;
  difficultyLevel: number;
  explanation?: string;

  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  orderIndex: number;
}

export interface ReadingSession {
  id: string;
  userId: string;
  readingTextId: string;
  readingTextTitle: string;
  category: string;
  readingTimeSeconds: number;
  calculatedWPM: number;
  correctAnswers: number;
  totalQuestions: number;
  comprehensionRate: number;
  efficiencyScore: number;
  completedAt: Date;
  performanceLevel: string;
}

export interface ReadingSessionResult {
  sessionId: string;
  readingTimeSeconds: number;
  calculatedWPM: number;
  correctAnswers: number;
  totalQuestions: number;
  comprehensionRate: number;
  efficiencyScore: number;
  performanceLevel: string;
  performanceFeedback: string;
  shouldRetry: boolean;
  questionResults: QuestionResult[];
}

export interface QuestionResult {
  questionId: string;
  questionText: string;
  selectedAnswer: string;
  correctAnswer: string;
  isCorrect: boolean;
}

export interface CreateReadingTextDto {
  title: string;
  content: string;
  difficultyLevel: number;
  category: string;
  language?: string;
  isActive?: boolean;
}

export interface UpdateReadingTextDto {
  title?: string;
  content?: string;
  difficultyLevel?: number;
  category?: string;
  language?: string;
  isActive?: boolean;
}

export interface CreateReadingQuestionDto {
  readingTextId: string;
  questionText: string;
  type: QuestionType;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  orderIndex: number;
}

export interface UpdateReadingQuestionDto {
  questionText?: string;
  type?: QuestionType;
  optionA?: string;
  optionB?: string;
  optionC?: string;
  optionD?: string;
  correctAnswer?: string;
  orderIndex?: number;
}

export interface StartReadingSessionDto {
  readingTextId: string;
}

export interface CompleteReadingSessionDto {
  readingTextId?: string; // Set in component, overridden in API
  readingTimeSeconds: number;
  answers: AnswerDto[];
}

export interface AnswerDto {
  questionId: string;
  selectedAnswer: string;
}

export interface ReadingStatistics {
  totalSessions: number;
  averageWPM: number;
  averageComprehension: number;
  averageEfficiency: number;
  textsCompleted: number;
  totalTimeMinutes: number;
  categoriesRead: string[];
  recentSessions: ReadingSession[];
}

export interface ImportReadingTextDto {
  title: string;
  content: string;
  difficultyLevel: number;
  category: string;
  language?: string;
  questions: ImportQuestionDto[];
}

export interface ImportQuestionDto {
  questionText: string;
  type: number;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
}

export const QUESTION_TYPE_LABELS: Record<QuestionType, string> = {
  [QuestionType.Literal]: 'Gerçek Anlam',
  [QuestionType.Inferential]: 'Çıkarım',
  [QuestionType.Evaluative]: 'Değerlendirme'
};

// YENİ: Bloom Taxonomy Labels
export const BLOOM_LEVEL_LABELS: Record<BloomLevel, string> = {
  [BloomLevel.Remember]: 'Hatırlama',
  [BloomLevel.Understand]: 'Anlama',
  [BloomLevel.Apply]: 'Uygulama',
  [BloomLevel.Analyze]: 'Analiz',
  [BloomLevel.Evaluate]: 'Değerlendirme',
  [BloomLevel.Create]: 'Yaratma'
};

export const BLOOM_LEVEL_COLORS: Record<BloomLevel, string> = {
  [BloomLevel.Remember]: '#E8F5E9',      // Açık yeşil
  [BloomLevel.Understand]: '#E3F2FD',    // Açık mavi
  [BloomLevel.Apply]: '#FFF3E0',         // Açık turuncu
  [BloomLevel.Analyze]: '#F3E5F5',       // Açık mor
  [BloomLevel.Evaluate]: '#FCE4EC',      // Açık pembe
  [BloomLevel.Create]: '#FFF9C4'         // Açık sarı
};

// YENİ: Bloom Performance Interfaces
export interface BloomPerformance {
  level: BloomLevel;
  levelName: string;
  levelNameEn: string;
  totalQuestions: number;
  correctAnswers: number;
  accuracyPercentage: number;
  performanceCategory: 'Mükemmel' | 'İyi' | 'Orta' | 'Geliştirilmeli';
}

export interface BloomPerformanceSummary {
  performanceByLevel: BloomPerformance[];
  weakestLevel?: BloomLevel;
  strongestLevel?: BloomLevel;
  recommendation: string;
  totalQuestions: number;
  totalCorrectAnswers: number;
  overallAccuracy: number;
}
