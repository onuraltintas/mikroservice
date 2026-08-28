// Exercise Session Models - Frontend
// Matches backend DTOs from ExerciseSessionDtos.cs

export enum SessionStatus {
  Active = 1,
  Paused = 2,
  Completed = 3,
  Timeout = 4,
  Abandoned = 5
}

export interface StartSessionRequest {
  exerciseId: string;
  readingTextId?: string;
  studentAssignmentId?: string;
  assessmentAttemptId?: string;
  customData?: any;  // Allow exercises to pass custom configuration
}

export interface StartSessionResponse {
  sessionId: string;
  exerciseId: string;
  exerciseTypeName: string;
  status: SessionStatus;
  startTime: Date;
  totalSteps: number;
  initialData: any;
  configuration: any;
}

export interface ActionData {
  actionId?: string;
  // Common fields
  action?: string;  // For RSVP, SpeedReading (start_reading, finish_reading, answer_question, etc.)
  number?: number;  // For Schulte Table
  row?: number;
  col?: number;
  answer?: string;  // For Tachistoscope, SpeedReading
  questionId?: string;  // For SpeedReading
  responseTime?: number;  // Response time in milliseconds
  timestamp: Date;

  // Eye tracking data (optional)
  eyeX?: number;
  eyeY?: number;

  // Additional custom data
  customData?: { [key: string]: any };
}

export interface ValidationResponse {
  isValid: boolean;
  message: string;
  nextNumber?: number;  // For Schulte Table
  nextStep?: number;
  nextWord?: string;  // For RSVP, Tachistoscope
  isCompleted: boolean;
  isCorrect?: boolean;  // For question-based exercises
  correctAnswer?: string;  // For Tachistoscope, SpeedReading
  explanation?: string;  // For SpeedReading questions
  currentWPM?: number;  // For SpeedReading
  feedbackData?: any;
}

export interface QuestionAnswerDto {
  questionId: string;
  answer: string;
  isCorrect: boolean;
  timeSpentSeconds: number;
  bloomLevel: number;
}

export interface CompleteSessionRequest {
  questionAnswers?: QuestionAnswerDto[];
  customData?: { [key: string]: any };  // For adaptive system and other custom exercise data
  isAssessmentMode?: boolean;
}

export interface ExerciseResult {
  sessionId: string;
  studentId: string;
  exerciseId: string;

  // Performance metrics
  correctCount: number;
  incorrectCount: number;
  accuracy: number | null;
  timeSpentSeconds: number;
  score: number | null;
  measurementStatus: 'Measured' | 'NotMeasured';

  // Reading specific metrics
  wordsRead?: number;
  rawWPM?: number;
  comprehensionScore?: number;
  weightedKDP?: number;

  // Gamification
  xpGained: number;
  unlockedBadges: string[];
  leveledUp: boolean;
  newLevel?: number;

  // Additional data
  detailedResults: any;
  feedback: string;
  nextRecommendation?: string;
}

export interface SessionProgressDto {
  sessionId: string;
  status: SessionStatus;
  currentStep: number;
  totalSteps: number;
  progressPercentage: number;
  correctCount: number;
  incorrectCount: number;
  elapsedSeconds: number;
}

export interface ActiveSessionDto {
  id: string;
  exerciseId: string;
  exerciseName: string;
  exerciseType: string;
  status: SessionStatus;
  startTime: Date;
  currentStep: number;
  totalSteps: number;
  progressPercentage: number;
}

// Schulte Table Specific
export interface SchulteTableSessionData {
  grid: number[][];
  gridSize: number;
  currentNumber: number;
  totalNumbers: number;
  timeLimit: number;
  heatmap: number[][];
  clicks: ClickData[];
  startTimestamp: Date;
}

export interface ClickData {
  number: number;
  row: number;
  col: number;
  timestamp: Date;
  isCorrect: boolean;
}

// RSVP Specific
export interface RSVPSessionData {
  words: string[];
  currentWordIndex: number;
  totalWords: number;
  targetWPM: number;
  intervalMs: number;
  readingTextId: string;
  readingTextTitle: string;
  showFixationPoint: boolean;
  enablePauseResume: boolean;
  readingStartTime: Date;
  pausedTimes: PauseRecord[];
  actualWPM?: number;
  comprehensionScore?: number;
  weightedKDP?: number;
}

export interface PauseRecord {
  pausedAt: Date;
  resumedAt?: Date;
}

// Tachistoscope Specific
export interface TachistoscopeSessionData {
  stimuli: Stimulus[];
  currentStimulusIndex: number;
  totalStimuli: number;
  displayDurationMs: number;
  stimulusType: string;
  difficultyLevel: number;
  trials: TrialRecord[];
}

export interface Stimulus {
  text: string;
  type: string;
  difficultyLevel: number;
}

export interface TrialRecord {
  stimulusText: string;
  userAnswer: string;
  isCorrect: boolean;
  responseTimeMs: number;
  timestamp: Date;
}

// Speed Reading Specific
export interface SpeedReadingSessionData {
  readingTextId: string;
  readingTextTitle: string;
  content: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  targetAgeGroup?: number;
  questions: QuestionData[];
  displayMode: string;
  showTimer: boolean;
  enableHighlighting: boolean;
  readingStartTime?: Date;
  readingEndTime?: Date;
  questionAnswers: QuestionAnswerRecord[];
  finalWPM?: number;
  comprehensionScore?: number;
  weightedKDP?: number;
}

export interface QuestionData {
  questionId: string;
  questionText: string;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  bloomLevel: number;
  difficultyLevel: number;
  explanation?: string;
}

export interface QuestionAnswerRecord {
  questionId: string;
  userAnswer: string;
  correctAnswer: string;
  isCorrect: boolean;
  bloomLevel: number;
  responseTimeSeconds: number;
  timestamp: Date;
}
