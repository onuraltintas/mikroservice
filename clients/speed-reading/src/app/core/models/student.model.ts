export interface Student {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  institutionId?: string;
  institutionName?: string;
  currentLevel: number;
  targetWPM: number;
  targetComprehension: number;
  dailyGoalMinutes: number;
  learningStyle: string;
  lastLoginAt?: Date;
  isActive: boolean;
  createdAt: Date;
  teacherId?: string;
  teacherName?: string;
}

export interface CreateStudentRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  institutionId?: string;
  teacherId?: string;
  currentLevel: number;
  targetWPM: number;
  targetComprehension: number;
  dailyGoalMinutes: number;
  learningStyle: string;
}

export interface UpdateStudentRequest {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  institutionId?: string;
  teacherId?: string;
  currentLevel: number;
  targetWPM: number;
  targetComprehension: number;
  dailyGoalMinutes: number;
  learningStyle: string;
  isActive: boolean;
}

export interface StudentExerciseResult {
  id: string;
  studentId: string;
  exerciseId: string;
  exerciseTitle: string;
  exerciseType: string;
  readingTextId?: string;
  readingTextTitle?: string;
  wordsRead: number;
  timeSpentSeconds: number;
  wordsPerMinute: number;
  comprehensionScore: number;
  weightedKDP: number;
  completedAt: string;
  isCompleted: boolean;
  difficultyLevel: number;
}

// Dashboard Models
export interface StudentDashboard {
  profile: StudentProfile;
  stats: DashboardStats;
  goals: DashboardGoals;
  recommendedTexts: ReadingText[];
  recommendedExercises: Exercise[];
  // recommendedSeries: REMOVED - Legacy TrainingSeries system
  learningPathProgress?: LearningPathProgressDto;
}

// Learning Path Progress (imported from backend)
export interface LearningPathProgressDto {
  totalItems: number;
  completedItems: number;
  remainingItems: number;
  completionPercentage: number;
  currentIndex: number;
  nextItem: LearningPathNextItemDto | null;
}

export interface LearningPathNextItemDto {
  id: string;
  contentType: string;
  contentId: string;
  contentTitle: string;
  difficultyLevel: number;
  estimatedDurationMinutes: number;
  recommendationReason: string | null;
}

export interface StudentProfile {
  firstName: string;
  lastName: string;
  ageGroup: string;
  currentLevel: number;
  targetWPM: number;
  currentWPM: number;
  targetComprehension: number;
  currentComprehension: number;
  progressPercentage: number;
}

export interface DashboardStats {
  totalReadingTimeMinutes: number;
  averageWPM: number;
  averageComprehension: number;
  completedExercises: number;
  completedReadings: number;
  currentStreak: number;
}

export interface DashboardGoals {
  dailyGoalMinutes: number;
  dailyProgressMinutes: number;
  dailyProgressPercentage: number;
  weeklyGoalMinutes: number;
  weeklyProgressMinutes: number;
  weeklyProgressPercentage: number;
}

export interface ReadingText {
  id: string;
  title: string;
  content: string;
  category: string;
  difficultyLevel: number;
  wordCount: number;
}

export interface Exercise {
  id: string;
  title: string;
  description: string;
  exerciseTypeId: string;
  exerciseTypeName: string;
  difficultyLevel: number;
  targetAgeGroupId?: string;
  targetAgeGroupName?: string;
  configurationJson?: string;
}

// REMOVED: TrainingSeries interface - Legacy system replaced by ExerciseProgramTemplate

export interface LevelUpResult {
  canLevelUp: boolean;
  currentLevel: number;
  newLevel?: number;
  reason: string;
  stats?: LevelUpStats;
}

export interface LevelUpStats {
  requiredWPM: number;
  requiredComprehension: number;
  requiredReadings: number;
  requiredExercises: number;
  completedReadings: number;
  completedExercises: number;
  averageWPM?: number;
  averageComprehension?: number;
}
