

export interface Exercise {
  id: string;
  title: string;
  description?: string;
  exerciseTypeId: string;
  exerciseTypeName?: string;
  exerciseTypeDisplayName?: string;
  exerciseTypeIconName?: string;
  exerciseTypeColorCode?: string;
  difficultyLevel: number;
  targetAgeGroupId?: string;
  targetAgeGroupName?: string;
  configurationJson?: string;
  createdAt?: Date;
}

export interface ExerciseType {
  id: string;
  name: string;
  displayName: string;
  description?: string;
  iconName?: string;
  colorCode?: string;
  sortOrder: number;
  isActive: boolean;
  categoryId?: string;
  categoryName?: string;
  categoryDisplayName?: string;
  createdAt?: Date;
  updatedAt?: Date;
}

export interface ExerciseTypeCategory {
  id: string;
  name: string;
  displayName: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt?: Date;
  updatedAt?: Date;
}

export type DifficultyLevel = 'Easy' | 'Medium' | 'Hard';

export interface ExerciseConfig {
  // VisualExpansion
  gridSize?: { rows: number; cols: number };
  displayDuration?: number;
  symbolCount?: number;

  // SpeedReading
  targetWPM?: number;
  scrollSpeed?: number;

  // Comprehension
  readingTextId?: string;

  // Scanning
  targetWord?: string;
  distractorCount?: number;

  // Chunking
  chunkSize?: number;
  pauseDuration?: number;
}

export interface ReadingText {
  id: string;
  exerciseId?: string;
  title: string;
  content: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  questions?: ComprehensionQuestion[];
}

export interface ComprehensionQuestion {
  id: string;
  questionText: string;
  options: string[];
  correctAnswer: string;
  points: number;
}

export interface ExerciseResult {
  studentAssignmentId?: string;
  exerciseId: string;
  readingTextId?: string;
  timeSpentSeconds: number;
  wordsRead: number;
  rawWPM?: number;
  comprehensionScore?: number;
  weightedKDP?: number;
  questionAnswersJson: string; // JSON string for backend
  readingMovementsJson: string; // JSON string for backend
}

export interface QuestionAnswer {
  questionId: string;
  selectedAnswer: string;
  isCorrect: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}