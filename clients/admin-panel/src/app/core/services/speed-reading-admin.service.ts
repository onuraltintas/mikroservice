import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SpeedReadingCapabilities {
  mode: 'Standalone' | 'Platform' | string;
  coachingIntegrationEnabled: boolean;
  notificationIntegrationEnabled: boolean;
  subscriptionIntegrationEnabled: boolean;
}

export interface SpeedReadingPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface AdminAnalyticsChartSeries {
  name: string;
  value: number;
}

export interface AdminAnalyticsChartData {
  name: string;
  series: AdminAnalyticsChartSeries[];
}

export interface AdminPlatformPopularContent {
  title: string;
  type: string;
  usageCount: number;
}

export interface AdminPlatformUsageAnalytics {
  dateFrom: string;
  dateTo: string;
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  newUserDataAvailable: boolean;
  totalActivities: number;
  totalReadingSessions: number;
  averageSessionDuration: number;
  userGrowthRate: number;
  userGrowthRateDataAvailable: boolean;
  engagementRate: number;
  retentionRate: number;
  userGrowth: AdminAnalyticsChartData[];
  dailyActiveUsers: AdminAnalyticsChartData[];
  activityVolume: AdminAnalyticsChartData[];
  hourlyActivity: AdminAnalyticsChartData[];
  popularContent: AdminPlatformPopularContent[];
  topInstitutions: { name: string; activeUserCount: number; totalActionCount: number }[];
  featureUsageStats: Record<string, number>;
}

export interface AdminContentAnalysisAnalytics {
  dateFrom: string;
  dateTo: string;
  totalExercises: number;
  totalReadingTexts: number;
  totalTrainingSeries: number;
  totalProgramTemplates: number;
  totalAssignments: number;
  assignmentDataAvailable: boolean;
  mostUsedContent: { contentId: string; contentType: string; title: string; usageCount: number; averageScore: number; averageComprehension: number }[];
  leastUsedContent: { contentId: string; contentType: string; title: string; usageCount: number; averageScore: number; averageComprehension: number }[];
  performanceByContentType: AdminAnalyticsChartData[];
  engagementByContentType: AdminAnalyticsChartData[];
  contentGaps: string[];
  popularTopics: string[];
  readingAnalysis: { difficultyLevel: number; totalReads: number; averageWpm: number; averageComprehension: number }[];
  exerciseAnalysis: { exerciseTypeName: string; totalCompletions: number; activeStudents: number; averageScore: number; performanceLevel: string }[];
  readingPerformanceChart: AdminAnalyticsChartData[];
  exerciseFrequencyChart: AdminAnalyticsChartData[];
}

export interface AdminSystemHealthAnalytics {
  dateFrom: string;
  dateTo: string;
  overallHealthScore: number;
  overallHealthDataAvailable: boolean;
  healthStatus: string;
  averagePlatformWpm: number;
  averagePlatformComprehension: number;
  userSatisfactionScore: number;
  userSatisfactionDataAvailable: boolean;
  totalExercisesCompleted: number;
  totalQuestionsAnswered: number;
  successRate: number;
  errorRate: number;
  errorRateDataAvailable: boolean;
  healthTrend: AdminAnalyticsChartData[];
  performanceTrend: AdminAnalyticsChartData[];
  systemAlerts: { severity: string; alertType: string; message: string; detectedAt: string }[];
  systemAlertsDataAvailable: boolean;
}

export interface AdminInstitutionAnalytics {
  dateFrom: string;
  dateTo: string;
  totalInstitutions: number;
  activeInstitutions: number;
  totalUsers: number;
  totalStudents: number;
  totalTeachers: number;
  institutionComparison: {
    institutionId: string;
    institutionName: string;
    totalUsers: number;
    activeUsers: number;
    totalStudents: number;
    totalTeachers: number;
    totalActivities: number;
    averageWpm: number;
    averageWpmDataAvailable: boolean;
    averageComprehension: number;
    averageComprehensionDataAvailable: boolean;
    averagePerformance: number;
    engagementRate: number;
  }[];
  institutionComparisonChart: AdminAnalyticsChartData;
  usersByInstitution: AdminAnalyticsChartData[];
  activityByInstitution: AdminAnalyticsChartData[];
  performanceByInstitution: AdminAnalyticsChartData[];
  topInstitutions: { institutionName: string; averageWpm: number; averageWpmDataAvailable: boolean; averageComprehension: number; averageComprehensionDataAvailable: boolean; activeStudents: number; activeStudentsDataAvailable: boolean; totalActivities: number }[];
}

export interface SpeedReadingProgramAnalytics {
  platformStats: { totalActiveStudents: number; averageSuccessRate: number; averageCurrentStreak: number; totalCompletedExercises: number };
  programDistribution: { programName: string; studentCount: number; percentage: number }[];
  weeklyProgress: { weekNumber: number; averageProgress: number; completionRate: number }[];
  recentStudentProgress: { studentName: string; studentEmail: string; programName: string; currentWeek: number; currentDay: number; currentStreak: number; longestStreak: number; successRate: number; difficultyLevel: number; lastActivityDate: string }[];
}

export interface SpeedReadingTeacherStudentPerformance {
  studentIdentifier: string;
  averageWpm: number;
  averageComprehension: number;
  activitiesCompleted: number;
  totalMinutes: number;
  performanceLevel: string;
}

export interface SpeedReadingTeacherClassOverviewAnalytics {
  dateFrom: string;
  dateTo: string;
  totalStudents: number;
  activeStudents: number;
  activeStudentsDataAvailable: boolean;
  classAverageWpmDataAvailable: boolean;
  classAverageComprehensionDataAvailable: boolean;
  classAverageWpm: number;
  classAverageComprehension: number;
  totalActivitiesCompleted: number;
  studentsAboveAverage: number;
  studentsAtAverage: number;
  studentsBelowAverage: number;
  topPerformers: SpeedReadingTeacherStudentPerformance[];
  studentsNeedingSupport: SpeedReadingTeacherStudentPerformance[];
}

export interface SpeedReadingTeacherAssignmentAnalytics {
  dateFrom: string;
  dateTo: string;
  dataAvailable: boolean;
  unavailableReason: string | null;
  assignmentInfo: { assignmentId: string; title: string; description: string; dueDate: string; assignedDate: string } | null;
  completionStats: { totalStudents: number; completed: number; inProgress: number; notStarted: number; completionRate: number } | null;
  performanceStats: { averageScore: number; medianScore: number; highestScore: number; lowestScore: number; standardDeviation: number } | null;
  scoreDistribution: AdminAnalyticsChartData[];
  studentBreakdown: { studentId: string; studentName: string; status: string; score: number | null; completionTime: number | null; submittedAt: string | null }[];
  timeStats: { averageCompletionTime: number; medianCompletionTime: number; fastestCompletion: number; slowestCompletion: number } | null;
}

export interface SpeedReadingTeacherContentAnalysisAnalytics {
  dateFrom: string;
  dateTo: string;
  exerciseAnalysis: { exerciseTypeName: string; totalCompletions: number; activeStudents: number; averageScore: number; performanceLevel: string }[];
  exerciseFrequencyChart: AdminAnalyticsChartData[];
  readingAnalysis: { difficultyLevel: number; totalReads: number; averageWpm: number; averageComprehension: number }[];
  readingPerformanceChart: AdminAnalyticsChartData[];
}

export interface SpeedReadingTeacherTimeProgressAnalytics {
  dateFrom: string;
  dateTo: string;
  weeklyProgressChart: AdminAnalyticsChartData[];
  monthlyProgressChart: AdminAnalyticsChartData[];
  activityIntensityChart: AdminAnalyticsChartData[];
  improvingStudents: { studentId: string; studentName: string; previousScore: number; currentScore: number; improvement: number; trend: string }[];
  decliningStudents: { studentId: string; studentName: string; previousScore: number; currentScore: number; improvement: number; trend: string }[];
}

export interface AdminStudentProgressSummary {
  id: string;
  userId: string;
  programTemplateId: string;
  currentDay: number;
  daysCompleted: number;
  exercisesCompleted: number;
  assignedDate: string;
}

export interface AdminStudentProgressDetails {
  progress: {
    id: string;
    programTemplateId: string;
    assignedDate: string;
    currentDay: number;
    currentWeek: number;
    currentDifficultyLevel: number;
    daysCompleted: number;
    exercisesCompleted: number;
    lastCompletionDate: string | null;
    isActive: boolean;
    completedDate: string | null;
    averageSuccessRate: number;
    currentStreak: number;
    longestStreak: number;
  };
  recentLogs: {
    id: string;
    exerciseId: string;
    exerciseTypeId: string;
    dayNumber: number;
    weekNumber: number;
    difficultyLevel: number;
    completedDate: string;
    timeSpentSeconds: number;
    successRate: number | null;
    isPassed: boolean;
    attemptNumber: number;
    isRetry: boolean;
    devicePlatform: string;
    correctCount: number;
    incorrectCount: number;
    totalAttempts: number;
    averageWpm: number | null;
    averageComprehension: number | null;
    measurementStatus: string;
  }[];
}

export interface SpeedReadingProduct {
  id: string;
  slug: string;
  name: string;
  description: string;
  includedProductSlugs: string[];
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
}

export interface SpeedReadingProductRequest {
  slug: string;
  name: string;
  description: string;
  includedProductSlugs?: string[];
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
}

export interface SpeedReadingPlan {
  id: string;
  name: string;
  description: string;
  slug: string;
  productId: string;
  productSlug: string;
  productName: string;
  includedProductSlugs: string[];
  modules: string[];
  price: number;
  billingPeriod: string;
  durationDays: number | null;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features: string[];
}

export interface SpeedReadingPlanRequest {
  name: string;
  description: string;
  slug: string;
  productId: string;
  price: number;
  billingPeriod: string;
  durationDays?: number | null;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features?: string[];
}

export interface SpeedReadingPlanUpdateRequest {
  name?: string;
  description?: string;
  price?: number;
  billingPeriod?: string;
  durationDays?: number | null;
  isActive?: boolean;
  isPublic?: boolean;
  sortOrder?: number;
  features?: string[];
}

export interface SpeedReadingSubscription {
  id: string;
  userId: string;
  userName: string | null;
  userEmail: string | null;
  plan: SpeedReadingPlan;
  productSlug: string;
  productName: string;
  status: string;
  startDate: string;
  endDate: string | null;
  notes: string | null;
  createdAt: string;
  isActive: boolean;
}

export interface SpeedReadingManualSubscriptionRequest {
  userId: string;
  userName?: string | null;
  userEmail?: string | null;
  planId: string;
  startDate: string;
  endDate?: string | null;
  notes?: string | null;
}

export interface SpeedReadingSubscriptionUpdateRequest {
  status: string;
  endDate?: string | null;
  notes?: string | null;
}

export interface SpeedReadingPayment {
  id: string;
  userId: string;
  userEmail: string;
  userName: string;
  planName: string;
  amount: number;
  currency: string;
  status: string;
  provider: string;
  providerPaymentId: string | null;
  errorMessage: string | null;
  subscriptionId: string | null;
  createdAt: string;
}

export interface SpeedReadingAgeGroup {
  id: string;
  name: string;
  displayName: string;
  minAge: number;
  maxAge: number | null;
  recommendedWpm: number;
  minWpm: number;
  maxWpm: number;
  recommendedComprehension: number;
  recommendedDailyMinutes: number;
  defaultDifficultyLevel: number;
  orderIndex: number;
  isActive: boolean;
  description: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingAgeGroupRequest {
  name: string;
  displayName: string;
  minAge: number;
  maxAge: number | null;
  minWpm: number;
  recommendedWpm: number;
  maxWpm: number;
  recommendedComprehension: number;
  recommendedDailyMinutes: number;
  defaultDifficultyLevel: number;
  orderIndex: number;
  isActive: boolean;
  description?: string | null;
}

export interface SpeedReadingAssessmentExercise {
  exerciseId: string;
  exerciseTitle: string;
  exerciseType: string;
  difficultyLevel: number;
  customTitle: string | null;
  customDescription: string | null;
  displayOrder: number;
}

export interface SpeedReadingAssessmentTemplate {
  id: string;
  name: string;
  targetAgeGroupId: string;
  ageGroupName: string;
  ageGroupDisplayName: string;
  exercises: SpeedReadingAssessmentExercise[];
  isActive: boolean;
  createdAt: string;
}

export interface SpeedReadingAssessmentExerciseInput {
  exerciseId: string;
  customTitle?: string | null;
  customDescription?: string | null;
  displayOrder: number;
}

export interface SpeedReadingAssessmentTemplateCreateRequest {
  name: string;
  targetAgeGroupId: string;
  exercises: SpeedReadingAssessmentExerciseInput[];
}

export interface SpeedReadingAssessmentTemplateUpdateRequest {
  name: string;
  exercises: SpeedReadingAssessmentExerciseInput[];
}

export interface SpeedReadingVisualizationQuestion {
  id: string;
  questionText: string;
  options: string[];
  correctAnswer: string;
  questionType: string;
  displayOrder: number;
  hintText: string | null;
}

export interface SpeedReadingVisualizationScene {
  id: string;
  exerciseId: string;
  description: string;
  imageUrl: string | null;
  duration: number;
  displayOrder: number;
  difficultyLevel: number;
  questions: SpeedReadingVisualizationQuestion[];
  createdAt: string | null;
}

export interface SpeedReadingVisualizationPage {
  items: SpeedReadingVisualizationScene[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SpeedReadingVisualizationExerciseOption {
  id: string;
  title: string;
  difficultyLevel: number;
}

export interface SpeedReadingVisualizationQuestionRequest {
  questionText: string;
  options: string[];
  correctAnswer: string;
  questionType: string;
  displayOrder: number;
  hintText?: string | null;
  id?: string | null;
}

export interface SpeedReadingVisualizationSceneRequest {
  exerciseId: string;
  description: string;
  imageUrl?: string | null;
  duration: number;
  displayOrder: number;
  difficultyLevel: number;
  questions: SpeedReadingVisualizationQuestionRequest[];
  targetAgeGroupConfigurationId?: string | null;
}

export interface SpeedReadingVisualizationImportResult {
  successCount: number;
  failedCount: number;
  message: string;
  errors: string[];
}

export interface SpeedReadingExamQuestion {
  id: string;
  content: string;
  question: string;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  optionE: string | null;
  correctOption: string;
  examType: number;
  difficulty: number;
  wordCount: number;
  topic: string | null;
  category: number;
  targetAgeGroupId: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingExamQuestionRequest {
  content: string;
  question: string;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  optionE?: string | null;
  correctOption: string;
  examType: number;
  difficulty: number;
  wordCount: number;
  topic?: string | null;
  category: number;
  targetAgeGroupId?: string | null;
}

export interface SpeedReadingExamQuestionPage {
  items: SpeedReadingExamQuestion[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface SpeedReadingVocabularyItem {
  id: string;
  word: string;
  definition: string;
  exampleSentence: string | null;
  synonyms: string | null;
  antonyms: string | null;
  targetAgeGroupId: string | null;
  targetAgeGroup: string | null;
  difficultyLevel: number;
  category: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingVocabularyItemRequest {
  word: string;
  definition: string;
  exampleSentence?: string | null;
  synonyms?: string | null;
  antonyms?: string | null;
  category: string;
  difficultyLevel: number;
  targetAgeGroupId?: string | null;
}

export interface SpeedReadingVocabularyPage {
  items: SpeedReadingVocabularyItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface SpeedReadingVocabularyImportResult {
  successCount: number;
  failureCount: number;
  errors: string[];
}

export interface SpeedReadingReportTemplate {
  id: string;
  name: string;
  description: string;
  type: string;
  category: string;
  configurationJson: string;
  isSystemTemplate: boolean;
  createdByUserId: string | null;
  createdAt: string;
  isActive: boolean;
}

export interface SpeedReadingReportTemplateCreateRequest {
  name: string;
  description: string;
  type: number;
  category: number;
  configurationJson: string;
}

export interface SpeedReadingReportTemplateUpdateRequest {
  name: string;
  description: string;
  configurationJson: string;
  isActive: boolean;
}

export interface SpeedReadingReportSnapshot {
  id: string;
  reportTemplateId: string;
  reportTemplateName: string;
  generatedAt: string;
  reportStartDate: string;
  reportEndDate: string;
  pdfFileUrl: string | null;
  excelFileUrl: string | null;
  isViewed: boolean;
  viewedAt: string | null;
}

export interface SpeedReadingReportSnapshotDetail extends SpeedReadingReportSnapshot {
  generatedForUserId: string;
  dataJson: string;
  dataJsonTruncated: boolean;
}

export interface SpeedReadingScheduledReport {
  id: string;
  reportTemplateId: string;
  reportTemplateName: string;
  frequency: string;
  dayOfWeek: number | null;
  dayOfMonth: number | null;
  deliveryTime: string;
  isActive: boolean;
  lastRunAt: string | null;
  nextRunAt: string | null;
  successCount: number;
  failureCount: number;
  sendEmail: boolean;
  saveToDashboard: boolean;
  emailRecipients: string | null;
}

export interface SpeedReadingScheduledReportCreateRequest {
  reportTemplateId: string;
  frequency: number;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  deliveryTime: string;
  sendEmail: boolean;
  saveToDashboard: boolean;
  emailRecipients?: string | null;
}

export interface SpeedReadingScheduledReportUpdateRequest {
  frequency: number;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  deliveryTime: string;
  isActive: boolean;
  sendEmail: boolean;
  saveToDashboard: boolean;
  emailRecipients?: string | null;
}

export interface SpeedReadingReportSnapshotCreateRequest {
  reportTemplateId: string;
  reportStartDate?: string | null;
  reportEndDate?: string | null;
  data?: unknown;
}

export interface SpeedReadingExerciseType {
  id: string;
  name: string;
  displayName: string;
  description: string;
  iconName: string;
  colorCode: string;
  sortOrder: number;
  isActive: boolean;
  engineType: string;
  categoryId: string | null;
}

export interface SpeedReadingExerciseTypeRequest {
  name: string;
  displayName: string;
  description?: string | null;
  iconName?: string | null;
  colorCode?: string | null;
  sortOrder: number;
  isActive: boolean;
  engineType: string;
  categoryId?: string | null;
}

export interface SpeedReadingExercise {
  id: string;
  title: string;
  description: string;
  difficultyLevel: number;
  exerciseTypeId: string;
  exerciseTypeName: string;
  configurationJson: string;
  targetAgeGroupConfigurationId: string | null;
}

export interface SpeedReadingExerciseRequest {
  title: string;
  description?: string | null;
  difficultyLevel: number;
  exerciseTypeId: string;
  configurationJson: string;
  targetAgeGroupConfigurationId?: string | null;
}

export interface SpeedReadingReadingText {
  id: string;
  title: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  language: string;
  isActive: boolean;
  exerciseId: string | null;
}

export interface SpeedReadingReadingTextRequest {
  title: string;
  content: string;
  wordCount: number;
  category: string;
  difficultyLevel: number;
  targetAgeGroupConfigurationId?: string | null;
  language: string;
  isActive: boolean;
  tags?: string | null;
  recommendedMinLevel: number;
  recommendedMaxLevel: number;
  exerciseId?: string | null;
}

export interface SpeedReadingReadingTextImportQuestionRequest {
  questionText: string;
  type: number;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
}

export interface SpeedReadingReadingTextImportRequest {
  title: string;
  content: string;
  difficultyLevel: number;
  category: string;
  language?: string | null;
  questions?: SpeedReadingReadingTextImportQuestionRequest[] | null;
}

export interface SpeedReadingReadingTextDetails extends SpeedReadingReadingText {
  content: string;
  targetAgeGroupConfigurationId: string | null;
  tags: string[];
  recommendedMinLevel: number;
  recommendedMaxLevel: number;
  questions: SpeedReadingReadingQuestion[];
}

export interface SpeedReadingReadingQuestion {
  id: string;
  questionText: string;
  type: number;
  bloomLevel: number;
  difficultyLevel: number;
  explanation: string | null;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  orderIndex: number;
}

export interface SpeedReadingReadingQuestionRequest {
  readingTextId: string;
  questionText: string;
  type: number;
  bloomLevel: number;
  difficultyLevel: number;
  explanation?: string | null;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
  correctAnswer: string;
  orderIndex: number;
}

export interface SpeedReadingReadingQuestionUpdateRequest
  extends Omit<SpeedReadingReadingQuestionRequest, 'readingTextId'> {}

export interface SpeedReadingProgramTemplate {
  id: string;
  name: string;
  description: string;
  targetAgeGroupConfigurationId: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  weeklyPatternJson: string;
  initialDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  maxDifficultyLevel: number;
  totalWeeks: number;
  totalDays: number;
  isActive: boolean;
  displayOrder: number;
  programType: number;
  examType: string | null;
  isAssessment: boolean;
}

export interface SpeedReadingProgramTemplateRequest {
  name: string;
  description: string;
  targetAgeGroupConfigurationId: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  weeklyPatternJson: string;
  initialDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  maxDifficultyLevel: number;
  totalWeeks: number;
  totalDays: number;
  isActive: boolean;
  displayOrder: number;
  programType: number;
  examType?: string | null;
  isAssessment: boolean;
}

export interface SpeedReadingLearningPathTemplate {
  id: string;
  name: string;
  targetAgeGroupConfigurationId: string | null;
  description: string | null;
  totalNodes: number;
  estimatedDays: number;
  isActive: boolean;
}

export interface SpeedReadingLearningPathTemplateRequest {
  name: string;
  targetAgeGroupConfigurationId?: string | null;
  description?: string | null;
  estimatedDays: number;
  isActive: boolean;
}

export interface SpeedReadingLearningPathNode {
  id: string;
  templateId: string;
  parentNodeId: string | null;
  nodeType: string;
  title: string;
  contentType: string | null;
  contentId: string | null;
  order: number;
  contents: SpeedReadingLearningPathNodeContent[];
  prerequisiteNodeIds: string[];
}

export interface SpeedReadingLearningPathNodeContent {
  id: string;
  exerciseId: string | null;
  readingTextId: string | null;
  description: string | null;
}

export interface SpeedReadingLearningPathTemplateDetails {
  template: SpeedReadingLearningPathTemplate;
  nodes: SpeedReadingLearningPathNode[];
}

export interface SpeedReadingLearningPathNodeRequest {
  templateId: string;
  parentNodeId?: string | null;
  nodeType: string;
  title: string;
  contentType?: string | null;
  contentId?: string | null;
  order: number;
}

export interface SpeedReadingLearningPathNodeUpdateRequest
  extends Omit<SpeedReadingLearningPathNodeRequest, 'templateId'> {}

export interface SpeedReadingLearningPathNodeContentRequest {
  nodeId: string;
  exerciseId?: string | null;
  readingTextId?: string | null;
  description?: string | null;
}

export interface SpeedReadingLearningPathNodeContentUpdateRequest
  extends Omit<SpeedReadingLearningPathNodeContentRequest, 'nodeId'> {}

export interface SpeedReadingLearningPathPrerequisiteRequest {
  nodeId: string;
  prerequisiteNodeId: string;
}

export interface SpeedReadingAchievement {
  id: string;
  name: string;
  description: string;
  category: string;
  tier: string;
  iconUrl: string;
  iconEmoji: string;
  criteriaType: string;
  criteriaValue: string;
  triggerType: string | null;
  triggerValue: number | null;
  isRepeatable: boolean;
  xpReward: number;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string | null;
  unlockedByUsersCount: number;
}

export interface SpeedReadingAchievementRequest {
  name: string;
  description: string;
  category: string;
  tier: string;
  iconUrl?: string | null;
  iconEmoji: string;
  criteriaType: string;
  criteriaValue: string;
  triggerType?: string | null;
  triggerValue?: number | null;
  isRepeatable: boolean;
  xpReward: number;
  isActive: boolean;
  sortOrder: number;
}

export interface SpeedReadingAchievementStats {
  totalCount: number;
  activeCount: number;
  inactiveCount: number;
  bronzeCount: number;
  silverCount: number;
  goldCount: number;
  diamondCount: number;
  categoryCounts: Record<string, number>;
}

export interface SpeedReadingCmsSeoSettings {
  metaTitle: string | null;
  metaDescription: string | null;
  metaKeywords: string | null;
  canonicalUrl: string | null;
  ogTitle: string | null;
  ogDescription: string | null;
  ogImage: string | null;
  noIndex: boolean;
}

export interface SpeedReadingCmsContentBlock {
  id: string;
  key: string;
  group: string;
  label: string | null;
  type: number;
  value: string;
}

export interface SpeedReadingCmsContentBlockRequest {
  key: string;
  group: string;
  label?: string | null;
  type: number;
  value: string;
}

export interface SpeedReadingCmsLandingUpdateRequest {
  group: string;
  blocks: Record<string, string>;
}

export interface SpeedReadingCmsPage {
  id: string;
  title: string;
  slug: string;
  content: string;
  isPublished: boolean;
  seoSettings: SpeedReadingCmsSeoSettings;
  createdAt: string;
  updatedAt: string | null;
  scheduledPublishAt: string | null;
}

export interface SpeedReadingCmsPageRequest {
  title: string;
  slug: string;
  content: string;
  isPublished: boolean;
  seoSettings: SpeedReadingCmsSeoSettings;
  scheduledPublishAt?: string | null;
}

export interface SpeedReadingCmsBlogPost {
  id: string;
  title: string;
  slug: string;
  summary: string | null;
  content: string;
  author: string | null;
  publishedAt: string | null;
  tags: string[];
  coverImageUrl: string | null;
  seoSettings: SpeedReadingCmsSeoSettings;
  viewCount: number;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string | null;
  scheduledPublishAt: string | null;
}

export interface SpeedReadingCmsBlogPostRequest {
  title: string;
  slug: string;
  summary?: string | null;
  content: string;
  author?: string | null;
  publishedAt?: string | null;
  tags?: string[] | null;
  coverImageUrl?: string | null;
  isPublished: boolean;
  seoSettings: SpeedReadingCmsSeoSettings;
  scheduledPublishAt?: string | null;
}

export interface SpeedReadingCmsRevision {
  id: string;
  entityType: string;
  entityId: string;
  version: number;
  createdAt: string;
  createdBy: string;
}

export interface SpeedReadingCmsContactMessage {
  id: string;
  name: string;
  email: string;
  subject: string;
  message: string;
  isRead: boolean;
  isReplied: boolean;
  repliedAt: string | null;
  replyContent: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingCmsNewsletterSubscriber {
  id: string;
  email: string;
  isActive: boolean;
  source: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingCmsMediaAsset {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  url: string;
  altText: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingCmsNavigationItem {
  id: string;
  menu: string;
  label: string;
  url: string;
  fragment: string | null;
  icon: string | null;
  sortOrder: number;
  isVisible: boolean;
  openInNewTab: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface SpeedReadingCmsNavigationItemRequest {
  menu: string;
  label: string;
  url: string;
  fragment?: string | null;
  icon?: string | null;
  sortOrder: number;
  isVisible: boolean;
  openInNewTab: boolean;
}

export interface SpeedReadingCmsContactReplyRequest {
  messageId: string;
  replyContent: string;
}

export interface SpeedReadingAnnouncement {
  id: string;
  title: string;
  content: string;
  plainTextContent: string | null;
  priority: number;
  displayType: number;
  icon: string | null;
  colorTheme: string | null;
  actionUrl: string | null;
  actionText: string | null;
  isPinned: boolean;
  startDate: string | null;
  expiresAt: string | null;
  createdAt: string;
  hasViewed: boolean;
  hasDismissed: boolean;
  hasClicked: boolean;
  targetAudience: number;
  targetInstitutionId: string | null;
  targetRoles: string[];
  isActive: boolean;
  sendEmailNotification: boolean;
  createInAppNotification: boolean;
  emailCampaignId: string | null;
  viewCount: number;
  clickCount: number;
  updatedAt: string | null;
  createdBy: string;
}

export interface SpeedReadingAnnouncementRequest {
  title: string;
  content: string;
  plainTextContent?: string | null;
  priority: number;
  targetAudience: number;
  targetInstitutionId?: string | null;
  targetRoles?: string[] | null;
  isPinned: boolean;
  startDate?: string | null;
  expiresAt?: string | null;
  displayType: number;
  icon?: string | null;
  colorTheme?: string | null;
  actionUrl?: string | null;
  actionText?: string | null;
  sendEmailNotification: boolean;
  createInAppNotification: boolean;
}

export interface SpeedReadingAnnouncementUpdateRequest extends Omit<SpeedReadingAnnouncementRequest, 'sendEmailNotification' | 'createInAppNotification'> {
  isActive: boolean;
}

export interface SpeedReadingAnnouncementStats {
  announcementId: string;
  totalViewCount: number;
  uniqueViewCount: number;
  totalClickCount: number;
  uniqueClickCount: number;
  dismissCount: number;
  viewRate: number;
  clickThroughRate: number;
  dismissRate: number;
  firstViewedAt: string | null;
  lastViewedAt: string | null;
}

export interface SpeedReadingEmailTemplate {
  id: string;
  name: string;
  code: string;
  subject: string;
  body: string;
  description: string;
  availableVariables: string;
  isActive: boolean;
  createdAt: string;
  lastModifiedAt: string | null;
}

export interface SpeedReadingEmailTemplateRequest {
  name: string;
  code: string;
  subject: string;
  body: string;
  description?: string | null;
  availableVariables?: string | null;
  isActive: boolean;
}

export interface SpeedReadingEmailCampaign {
  id: string;
  name: string;
  subject: string;
  status: number;
  targetRoles: string | null;
  targetInstitutionId: string | null;
  includeAllUsers: boolean;
  includeSubscribers: boolean;
  scheduledFor: string | null;
  sentAt: string | null;
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  openedCount: number;
  clickedCount: number;
  createdAt: string;
}

export interface SpeedReadingEmailCampaignRequest {
  name: string;
  subject: string;
  body: string;
  plainTextBody?: string | null;
  targetRoles?: string | null;
  targetInstitutionId?: string | null;
  includeAllUsers: boolean;
  includeSubscribers: boolean;
  scheduledFor?: string | null;
}

export interface SpeedReadingEmailCampaignStats {
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  openedCount: number;
  clickedCount: number;
  pendingCount: number;
}

export interface SpeedReadingAdminNotification {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: number;
  typeName: string;
  priority: number;
  priorityName: string;
  status: number;
  actionUrl: string | null;
  iconUrl: string | null;
  createdAt: string;
  readAt: string | null;
  userName: string;
  userEmail: string;
  userRole: string;
  isRead: boolean;
}

export interface SpeedReadingAdminNotificationPage {
  items: SpeedReadingAdminNotification[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SpeedReadingCreateNotificationRequest {
  userId: string;
  title: string;
  message: string;
  type?: number;
  priority?: number;
  actionUrl?: string | null;
  iconUrl?: string | null;
}

export interface SpeedReadingBulkNotificationRequest {
  targetType: string;
  targetRole?: string | null;
  title: string;
  message: string;
  type?: number;
  priority?: number;
  actionUrl?: string | null;
  sendEmail: boolean;
}

export interface SpeedReadingBulkNotificationResult {
  success: boolean;
  totalSent: number;
  totalFailed: number;
  emailsSent: number;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/speed-reading`;

  getSubscriptionProducts() {
    return this.http.get<{ success: boolean; data: SpeedReadingProduct[] }>(`${this.url}/products/all`)
      .pipe(map(response => response.data ?? []));
  }

  createSubscriptionProduct(request: SpeedReadingProductRequest) {
    return this.http.post<{ success: boolean; data: SpeedReadingProduct }>(`${this.url}/products`, request)
      .pipe(map(response => response.data));
  }

  updateSubscriptionProduct(id: string, request: Partial<SpeedReadingProductRequest>) {
    return this.http.put<{ success: boolean; data: SpeedReadingProduct }>(`${this.url}/products/${id}`, request)
      .pipe(map(response => response.data));
  }

  deactivateSubscriptionProduct(id: string) {
    return this.http.delete(`${this.url}/products/${id}`);
  }

  getSubscriptionPlans() {
    return this.http.get<{ success: boolean; data: SpeedReadingPlan[] }>(`${this.url}/subscription-plans/all`)
      .pipe(map(response => response.data ?? []));
  }

  createSubscriptionPlan(request: SpeedReadingPlanRequest) {
    return this.http.post<{ success: boolean; data: { id: string } }>(`${this.url}/subscription-plans`, request)
      .pipe(map(response => response.data));
  }

  updateSubscriptionPlan(id: string, request: SpeedReadingPlanUpdateRequest) {
    return this.http.put<{ success: boolean; data: SpeedReadingPlan }>(`${this.url}/subscription-plans/${id}`, request)
      .pipe(map(response => response.data));
  }

  deactivateSubscriptionPlan(id: string) {
    return this.http.delete(`${this.url}/subscription-plans/${id}`);
  }

  getUserSubscriptions(page = 1, pageSize = 25, status?: string, search?: string) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (search?.trim()) params = params.set('search', search.trim());
    return this.http.get<{ success: boolean; data: { items: SpeedReadingSubscription[]; totalCount: number; page: number; pageSize: number } }>(
      `${this.url}/subscriptions`, { params }
    ).pipe(map(response => ({
      items: response.data?.items ?? [],
      totalCount: response.data?.totalCount ?? 0,
      pageNumber: response.data?.page ?? page,
      pageSize: response.data?.pageSize ?? pageSize
    })));
  }

  createManualSubscription(request: SpeedReadingManualSubscriptionRequest) {
    return this.http.post<{ success: boolean; data: SpeedReadingSubscription }>(`${this.url}/subscriptions`, request)
      .pipe(map(response => response.data));
  }

  updateUserSubscription(id: string, request: SpeedReadingSubscriptionUpdateRequest) {
    return this.http.put<{ success: boolean; data: SpeedReadingSubscription }>(`${this.url}/subscriptions/${id}`, request)
      .pipe(map(response => response.data));
  }

  deleteUserSubscription(id: string) {
    return this.http.delete(`${this.url}/subscriptions/${id}`);
  }

  getPaymentHistory(page = 1, pageSize = 25, status?: string, search?: string) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (search?.trim()) params = params.set('search', search.trim());
    return this.http.get<{ items: SpeedReadingPayment[]; total: number; pageNumber: number; pageSize: number }>(
      `${this.url}/payment`, { params }
    ).pipe(map(response => ({
      items: response.items ?? [],
      totalCount: response.total ?? 0,
      pageNumber: response.pageNumber ?? page,
      pageSize: response.pageSize ?? pageSize
    })));
  }

  getAgeGroups(activeOnly = false) {
    const params = new HttpParams().set('activeOnly', activeOnly);
    return this.http.get<SpeedReadingAgeGroup[]>(
      `${this.url}/age-group-configurations`,
      { params }
    );
  }

  createAgeGroup(request: SpeedReadingAgeGroupRequest) {
    return this.http.post<SpeedReadingAgeGroup>(`${this.url}/age-group-configurations`, request);
  }

  updateAgeGroup(id: string, request: SpeedReadingAgeGroupRequest) {
    return this.http.put<void>(`${this.url}/age-group-configurations/${id}`, request);
  }

  deleteAgeGroup(id: string) {
    return this.http.delete<void>(`${this.url}/age-group-configurations/${id}`);
  }

  getAssessmentTemplates() {
    return this.http.get<SpeedReadingAssessmentTemplate[]>(
      `${this.url}/admin/assessment-templates`
    );
  }

  getAssessmentTemplateByAgeGroup(ageGroupId: string) {
    return this.http.get<SpeedReadingAssessmentTemplate>(
      `${this.url}/admin/assessment-templates/age-group/${ageGroupId}`
    );
  }

  createAssessmentTemplate(request: SpeedReadingAssessmentTemplateCreateRequest) {
    return this.http.post<string>(`${this.url}/admin/assessment-templates`, request);
  }

  updateAssessmentTemplate(id: string, request: SpeedReadingAssessmentTemplateUpdateRequest) {
    return this.http.put<void>(`${this.url}/admin/assessment-templates/${id}`, request);
  }

  deleteAssessmentTemplate(id: string) {
    return this.http.delete<void>(`${this.url}/admin/assessment-templates/${id}`);
  }

  getVisualizationScenes(pageNumber = 1, pageSize = 25, difficultyLevel?: number, searchTerm?: string) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (difficultyLevel) params = params.set('difficultyLevel', difficultyLevel);
    if (searchTerm?.trim()) params = params.set('searchTerm', searchTerm.trim());
    return this.http.get<SpeedReadingVisualizationPage>(
      `${this.url}/admin/visualization-scenes`,
      { params }
    );
  }

  getVisualizationScene(id: string) {
    return this.http.get<SpeedReadingVisualizationScene>(
      `${this.url}/admin/visualization-scenes/${id}`
    );
  }

  getVisualizationExercises() {
    return this.http.get<SpeedReadingVisualizationExerciseOption[]>(
      `${this.url}/admin/visualization-scenes/exercises`
    );
  }

  createVisualizationScene(request: SpeedReadingVisualizationSceneRequest) {
    return this.http.post<string>(`${this.url}/admin/visualization-scenes`, request);
  }

  updateVisualizationScene(id: string, request: SpeedReadingVisualizationSceneRequest) {
    return this.http.put<void>(`${this.url}/admin/visualization-scenes/${id}`, request);
  }

  deleteVisualizationScene(id: string) {
    return this.http.delete<void>(`${this.url}/admin/visualization-scenes/${id}`);
  }

  importVisualizationCsv(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<SpeedReadingVisualizationImportResult>(
      `${this.url}/admin/visualization-scenes/import/csv`,
      formData
    );
  }

  getExamQuestions(pageNumber = 1, pageSize = 25, examType?: number, difficulty?: number, category?: number, searchTerm?: string, ageGroupId?: string) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (examType !== undefined) params = params.set('examType', examType);
    if (difficulty !== undefined) params = params.set('difficulty', difficulty);
    if (category !== undefined) params = params.set('category', category);
    if (searchTerm?.trim()) params = params.set('searchTerm', searchTerm.trim());
    if (ageGroupId) params = params.set('ageGroupId', ageGroupId);
    return this.http.get<SpeedReadingExamQuestionPage>(`${this.url}/exam-questions`, { params });
  }

  getExamQuestion(id: string) {
    return this.http.get<SpeedReadingExamQuestion>(`${this.url}/exam-questions/${id}`);
  }

  createExamQuestion(request: SpeedReadingExamQuestionRequest) {
    return this.http.post<string>(`${this.url}/exam-questions`, request);
  }

  updateExamQuestion(id: string, request: SpeedReadingExamQuestionRequest) {
    return this.http.put<void>(`${this.url}/exam-questions/${id}`, request);
  }

  deleteExamQuestion(id: string) {
    return this.http.delete<void>(`${this.url}/exam-questions/${id}`);
  }

  getVocabulary(search = '', category = '', difficultyLevel?: number, ageGroupId?: string, pageNumber = 1, pageSize = 25) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (category.trim()) params = params.set('category', category.trim());
    if (difficultyLevel !== undefined) params = params.set('difficultyLevel', difficultyLevel);
    if (ageGroupId) params = params.set('ageGroupId', ageGroupId);
    return this.http.get<SpeedReadingVocabularyPage>(`${this.url}/vocabulary`, { params });
  }

  getVocabularyCategories() {
    return this.http.get<string[]>(`${this.url}/vocabulary/categories`);
  }

  getVocabularyItem(id: string) {
    return this.http.get<SpeedReadingVocabularyItem>(`${this.url}/vocabulary/${id}`);
  }

  createVocabularyItem(request: SpeedReadingVocabularyItemRequest) {
    return this.http.post<SpeedReadingVocabularyItem>(`${this.url}/vocabulary`, request);
  }

  updateVocabularyItem(id: string, request: SpeedReadingVocabularyItemRequest) {
    return this.http.put<SpeedReadingVocabularyItem>(`${this.url}/vocabulary/${id}`, request);
  }

  deleteVocabularyItem(id: string) {
    return this.http.delete<void>(`${this.url}/vocabulary/${id}`);
  }

  importVocabulary(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<SpeedReadingVocabularyImportResult>(`${this.url}/vocabulary/import`, formData);
  }

  exportVocabulary(category?: string, difficultyLevel?: number, ageGroupId?: string) {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    if (difficultyLevel !== undefined) params = params.set('difficultyLevel', difficultyLevel);
    if (ageGroupId) params = params.set('ageGroupId', ageGroupId);
    return this.http.get(`${this.url}/vocabulary/export`, { params, responseType: 'blob' });
  }

  downloadVocabularyTemplate() {
    return this.http.get(`${this.url}/vocabulary/download-template`, { responseType: 'blob' });
  }

  getReportTemplates(type?: string, isActive?: boolean, limit = 100) {
    let params = new HttpParams().set('limit', limit);
    if (type) params = params.set('type', type);
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<SpeedReadingReportTemplate[]>(`${this.url}/reports/templates`, { params });
  }

  getReportTemplate(id: string) {
    return this.http.get<SpeedReadingReportTemplate>(`${this.url}/reports/templates/${id}`);
  }

  createReportTemplate(request: SpeedReadingReportTemplateCreateRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingReportTemplate>(
      `${this.url}/reports/templates`, request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateReportTemplate(id: string, request: SpeedReadingReportTemplateUpdateRequest, idempotencyKey?: string) {
    return this.http.put<SpeedReadingReportTemplate>(
      `${this.url}/reports/templates/${id}`, request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteReportTemplate(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reports/templates/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getReportSnapshots(limit = 50) {
    return this.http.get<SpeedReadingReportSnapshot[]>(`${this.url}/reports/snapshots`, {
      params: new HttpParams().set('limit', limit)
    });
  }

  getReportSnapshot(id: string) {
    return this.http.get<SpeedReadingReportSnapshotDetail>(`${this.url}/reports/snapshots/${id}`);
  }

  createReportSnapshot(request: SpeedReadingReportSnapshotCreateRequest, idempotencyKey?: string) {
    return this.http.post<unknown>(
      `${this.url}/reports/snapshots`, request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteReportSnapshot(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reports/snapshots/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getScheduledReports(limit = 100) {
    return this.http.get<SpeedReadingScheduledReport[]>(`${this.url}/reports/scheduled`, {
      params: new HttpParams().set('limit', limit)
    });
  }

  createScheduledReport(request: SpeedReadingScheduledReportCreateRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingScheduledReport>(
      `${this.url}/reports/scheduled`, request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateScheduledReport(id: string, request: SpeedReadingScheduledReportUpdateRequest, idempotencyKey?: string) {
    return this.http.put<SpeedReadingScheduledReport>(
      `${this.url}/reports/scheduled/${id}`, request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateScheduledReportStatus(id: string, isActive: boolean, idempotencyKey?: string) {
    return this.http.patch<SpeedReadingScheduledReport>(
      `${this.url}/reports/scheduled/${id}/status`, { isActive },
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteScheduledReport(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reports/scheduled/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  exportReport(format: 'pdf' | 'excel', data: unknown) {
    return this.http.post(`${this.url}/reports/export/${format}`, data, { responseType: 'blob' });
  }

  getCapabilities() {
    return this.http.get<SpeedReadingCapabilities>(`${this.url}/capabilities`);
  }

  getPlatformUsage(dateFrom?: string, dateTo?: string) {
    return this.http.get<AdminPlatformUsageAnalytics>(
      `${this.url}/analytics/admin/platform-usage`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getContentAnalysis(dateFrom?: string, dateTo?: string) {
    return this.http.get<AdminContentAnalysisAnalytics>(
      `${this.url}/analytics/admin/content-analysis`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getSystemHealth(dateFrom?: string, dateTo?: string) {
    return this.http.get<AdminSystemHealthAnalytics>(
      `${this.url}/analytics/admin/system-health`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getInstitutionAnalytics(dateFrom?: string, dateTo?: string) {
    return this.http.get<AdminInstitutionAnalytics>(
      `${this.url}/analytics/admin/institutions`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getProgramAnalytics() {
    return this.http.get<SpeedReadingProgramAnalytics>(`${this.url}/analytics/admin/programs`);
  }

  getTeacherClassOverview(teacherId: string, dateFrom?: string, dateTo?: string) {
    return this.http.get<SpeedReadingTeacherClassOverviewAnalytics>(
      `${this.url}/analytics/admin/teachers/${encodeURIComponent(teacherId)}/class-overview`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getTeacherAssignmentAnalytics(teacherId: string, dateFrom?: string, dateTo?: string) {
    return this.http.get<SpeedReadingTeacherAssignmentAnalytics>(
      `${this.url}/analytics/admin/teachers/${encodeURIComponent(teacherId)}/assignments`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getTeacherContentAnalysis(teacherId: string, dateFrom?: string, dateTo?: string) {
    return this.http.get<SpeedReadingTeacherContentAnalysisAnalytics>(
      `${this.url}/analytics/admin/teachers/${encodeURIComponent(teacherId)}/content-analysis`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getTeacherTimeProgress(teacherId: string, dateFrom?: string, dateTo?: string) {
    return this.http.get<SpeedReadingTeacherTimeProgressAnalytics>(
      `${this.url}/analytics/admin/teachers/${encodeURIComponent(teacherId)}/time-progress`,
      { params: this.dateRangeParams(dateFrom, dateTo) }
    );
  }

  getStudentProgress(pageNumber = 1, pageSize = 25, searchTerm = '') {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    if (searchTerm.trim()) params = params.set('searchTerm', searchTerm.trim());
    return this.http.get<SpeedReadingPage<AdminStudentProgressSummary>>(
      `${this.url}/student-progress`,
      { params }
    );
  }

  getStudentProgressDetails(progressId: string) {
    return this.http.get<AdminStudentProgressDetails>(`${this.url}/student-progress/${progressId}`);
  }

  resetStudentProgress(progressId: string) {
    return this.http.post<void>(`${this.url}/student-progress/${progressId}/reset`, {});
  }

  getExerciseTypes(pageNumber = 1, pageSize = 20) {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http.get<SpeedReadingPage<SpeedReadingExerciseType>>(
      `${this.url}/exercise-types`,
      { params }
    );
  }

  createExerciseType(request: SpeedReadingExerciseTypeRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingExerciseType>(
      `${this.url}/exercise-types`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateExerciseType(
    id: string,
    request: SpeedReadingExerciseTypeRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingExerciseType>(
      `${this.url}/exercise-types/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteExerciseType(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/exercise-types/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getExercises(pageNumber = 1, pageSize = 50) {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingExercise>>(
      `${this.url}/exercises`,
      { params }
    );
  }

  createExercise(request: SpeedReadingExerciseRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingExercise>(
      `${this.url}/exercises`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateExercise(id: string, request: SpeedReadingExerciseRequest, idempotencyKey?: string) {
    return this.http.put<SpeedReadingExercise>(
      `${this.url}/exercises/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteExercise(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/exercises/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getReadingTexts(exerciseId?: string) {
    let params = new HttpParams();
    if (exerciseId) {
      params = params.set('exerciseId', exerciseId);
    }
    return this.http.get<SpeedReadingReadingText[]>(
      `${this.url}/reading-texts`,
      { params }
    );
  }

  getReadingText(id: string) {
    return this.http.get<SpeedReadingReadingTextDetails>(
      `${this.url}/reading-texts/${id}`
    );
  }

  createReadingText(request: SpeedReadingReadingTextRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingReadingText>(
      `${this.url}/reading-texts`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateReadingText(
    id: string,
    request: SpeedReadingReadingTextRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingReadingText>(
      `${this.url}/reading-texts/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteReadingText(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reading-texts/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  exportReadingText(id: string, format: 'pdf' | 'docx') {
    return this.http.get(`${this.url}/reading-texts/${id}/export/${format}`, { responseType: 'blob' });
  }

  exportReadingTexts(ids: string[], format: 'pdf' | 'docx') {
    return this.http.post(`${this.url}/reading-texts/export/${format}`, { ids }, { responseType: 'blob' });
  }

  importReadingTexts(file: File, format: 'csv' | 'excel', idempotencyKey?: string) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<unknown>(
      `${this.url}/reading-texts/import/${format}`,
      formData,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  importReadingTextsBulk(requests: SpeedReadingReadingTextImportRequest[], idempotencyKey?: string) {
    return this.http.post<unknown>(
      `${this.url}/reading-texts/import/bulk`,
      requests,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  createReadingQuestion(request: SpeedReadingReadingQuestionRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingReadingQuestion>(
      `${this.url}/reading-questions`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateReadingQuestion(
    id: string,
    request: SpeedReadingReadingQuestionUpdateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingReadingQuestion>(
      `${this.url}/reading-questions/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteReadingQuestion(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/reading-questions/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getProgramTemplates() {
    return this.http.get<SpeedReadingProgramTemplate[]>(`${this.url}/program-templates/admin`);
  }

  createProgramTemplate(request: SpeedReadingProgramTemplateRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingProgramTemplate>(
      `${this.url}/program-templates`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateProgramTemplate(
    id: string,
    request: SpeedReadingProgramTemplateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingProgramTemplate>(
      `${this.url}/program-templates/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteProgramTemplate(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/program-templates/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  cloneProgramTemplate(id: string, idempotencyKey?: string) {
    return this.http.post<SpeedReadingProgramTemplate>(
      `${this.url}/program-templates/${id}/clone`,
      {},
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getLearningPathTemplates() {
    return this.http.get<SpeedReadingLearningPathTemplate[]>(`${this.url}/learning-paths/templates/admin`);
  }

  createLearningPathTemplate(request: SpeedReadingLearningPathTemplateRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingLearningPathTemplate>(
      `${this.url}/learning-paths/templates`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateLearningPathTemplate(
    id: string,
    request: SpeedReadingLearningPathTemplateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingLearningPathTemplate>(
      `${this.url}/learning-paths/templates/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteLearningPathTemplate(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/learning-paths/templates/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getLearningPathTemplateDetails(id: string) {
    return this.http.get<SpeedReadingLearningPathTemplateDetails>(
      `${this.url}/learning-paths/templates/${id}/admin`
    );
  }

  createLearningPathNode(request: SpeedReadingLearningPathNodeRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingLearningPathNode>(
      `${this.url}/learning-paths/nodes`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateLearningPathNode(
    id: string,
    request: SpeedReadingLearningPathNodeUpdateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<SpeedReadingLearningPathNode>(
      `${this.url}/learning-paths/nodes/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteLearningPathNode(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/learning-paths/nodes/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  createLearningPathNodeContent(
    request: SpeedReadingLearningPathNodeContentRequest,
    idempotencyKey?: string
  ) {
    return this.http.post<unknown>(
      `${this.url}/learning-paths/node-contents`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateLearningPathNodeContent(
    id: string,
    request: SpeedReadingLearningPathNodeContentUpdateRequest,
    idempotencyKey?: string
  ) {
    return this.http.put<unknown>(
      `${this.url}/learning-paths/node-contents/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteLearningPathNodeContent(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/learning-paths/node-contents/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  createLearningPathPrerequisite(
    request: SpeedReadingLearningPathPrerequisiteRequest,
    idempotencyKey?: string
  ) {
    return this.http.post<void>(
      `${this.url}/learning-paths/prerequisites`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteLearningPathPrerequisite(
    nodeId: string,
    prerequisiteNodeId: string,
    idempotencyKey?: string
  ) {
    return this.http.delete<void>(
      `${this.url}/learning-paths/prerequisites/${nodeId}/${prerequisiteNodeId}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getAchievementsForAdmin(pageNumber = 1, pageSize = 50) {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingAchievement>>(
      `${this.url}/achievements/admin`,
      { params }
    );
  }

  getAchievementForAdmin(id: string) {
    return this.http.get<SpeedReadingAchievement>(`${this.url}/achievements/admin/${id}`);
  }

  getAchievementStats() {
    return this.http.get<SpeedReadingAchievementStats>(`${this.url}/achievements/admin/stats`);
  }

  getAchievementCategories() {
    return this.http.get<string[]>(`${this.url}/achievements/categories`);
  }

  getAchievementTiers() {
    return this.http.get<string[]>(`${this.url}/achievements/tiers`);
  }

  createAchievement(request: SpeedReadingAchievementRequest, idempotencyKey?: string) {
    return this.http.post<SpeedReadingAchievement>(
      `${this.url}/achievements`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  updateAchievement(id: string, request: SpeedReadingAchievementRequest, idempotencyKey?: string) {
    return this.http.put<SpeedReadingAchievement>(
      `${this.url}/achievements/${id}`,
      request,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  deleteAchievement(id: string, idempotencyKey?: string) {
    return this.http.delete<void>(
      `${this.url}/achievements/${id}`,
      { headers: this.idempotencyHeaders(idempotencyKey) }
    );
  }

  getCmsBlocks(group?: string) {
    let params = new HttpParams();
    if (group?.trim()) params = params.set('group', group.trim());
    return this.http.get<{ data: SpeedReadingCmsContentBlock[] }>(`${this.url}/admin/cms/blocks`, { params })
      .pipe(map(response => response.data ?? []));
  }

  getCmsLanding(group = 'HomePage') {
    return this.http.get<{ data: SpeedReadingCmsContentBlock[] }>(`${this.url}/admin/cms/landing`, {
      params: new HttpParams().set('group', group)
    }).pipe(map(response => response.data ?? []));
  }

  updateCmsLanding(request: SpeedReadingCmsLandingUpdateRequest) {
    return this.http.put<void>(`${this.url}/admin/cms/landing`, request);
  }

  createCmsBlock(request: SpeedReadingCmsContentBlockRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.url}/admin/cms/blocks`, request)
      .pipe(map(response => response.data.id));
  }

  updateCmsBlock(id: string, request: SpeedReadingCmsContentBlockRequest) {
    return this.http.put<void>(`${this.url}/admin/cms/blocks/${id}`, request);
  }

  deleteCmsBlock(id: string) {
    return this.http.delete<void>(`${this.url}/admin/cms/blocks/${id}`);
  }

  getCmsMedia(pageNumber = 1, pageSize = 30) {
    return this.http.get<{ data: SpeedReadingPage<SpeedReadingCmsMediaAsset> }>(`${this.url}/admin/cms/media`, {
      params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize)
    }).pipe(map(response => response.data));
  }

  uploadCmsMedia(file: File, altText?: string | null) {
    const formData = new FormData();
    formData.append('file', file, file.name);
    if (altText?.trim()) formData.append('altText', altText.trim());
    return this.http.post<{ data: SpeedReadingCmsMediaAsset }>(`${this.url}/admin/cms/media`, formData)
      .pipe(map(response => response.data));
  }

  deleteCmsMedia(id: string) {
    return this.http.delete<void>(`${this.url}/admin/cms/media/${id}`);
  }

  getCmsNavigation(menu = 'Main', includeHidden = true) {
    return this.http.get<{ data: SpeedReadingCmsNavigationItem[] }>(`${this.url}/admin/cms/navigation`, {
      params: new HttpParams().set('menu', menu).set('includeHidden', includeHidden)
    }).pipe(map(response => response.data ?? []));
  }

  createCmsNavigationItem(request: SpeedReadingCmsNavigationItemRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.url}/admin/cms/navigation`, request)
      .pipe(map(response => response.data.id));
  }

  updateCmsNavigationItem(id: string, request: SpeedReadingCmsNavigationItemRequest) {
    return this.http.put<void>(`${this.url}/admin/cms/navigation/${id}`, request);
  }

  deleteCmsNavigationItem(id: string) {
    return this.http.delete<void>(`${this.url}/admin/cms/navigation/${id}`);
  }

  getCmsPages(pageNumber = 1, pageSize = 25) {
    return this.http.get<{ data: SpeedReadingPage<SpeedReadingCmsPage> }>(`${this.url}/admin/cms/pages`, {
      params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize)
    }).pipe(map(response => response.data));
  }

  getCmsPage(id: string) {
    return this.http.get<{ data: SpeedReadingCmsPage }>(`${this.url}/admin/cms/pages/${id}`)
      .pipe(map(response => response.data));
  }

  previewCmsPage(id: string) {
    return this.http.get<{ data: SpeedReadingCmsPage }>(`${this.url}/admin/cms/pages/${id}/preview`)
      .pipe(map(response => response.data));
  }

  createCmsPage(request: SpeedReadingCmsPageRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.url}/admin/cms/pages`, request)
      .pipe(map(response => response.data.id));
  }

  updateCmsPage(id: string, request: SpeedReadingCmsPageRequest) {
    return this.http.put<void>(`${this.url}/admin/cms/pages/${id}`, request);
  }

  deleteCmsPage(id: string) {
    return this.http.delete<void>(`${this.url}/admin/cms/pages/${id}`);
  }

  getCmsBlogPosts(pageNumber = 1, pageSize = 25) {
    return this.http.get<{ data: SpeedReadingPage<SpeedReadingCmsBlogPost> }>(`${this.url}/admin/cms/blog`, {
      params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize)
    }).pipe(map(response => response.data));
  }

  getCmsBlogPost(id: string) {
    return this.http.get<{ data: SpeedReadingCmsBlogPost }>(`${this.url}/admin/cms/blog/${id}`)
      .pipe(map(response => response.data));
  }

  previewCmsBlogPost(id: string) {
    return this.http.get<{ data: SpeedReadingCmsBlogPost }>(`${this.url}/admin/cms/blog/${id}/preview`)
      .pipe(map(response => response.data));
  }

  createCmsBlogPost(request: SpeedReadingCmsBlogPostRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.url}/admin/cms/blog`, request)
      .pipe(map(response => response.data.id));
  }

  updateCmsBlogPost(id: string, request: SpeedReadingCmsBlogPostRequest) {
    return this.http.put<void>(`${this.url}/admin/cms/blog/${id}`, request);
  }

  deleteCmsBlogPost(id: string) {
    return this.http.delete<void>(`${this.url}/admin/cms/blog/${id}`);
  }

  getCmsRevisions(entityType: 'Page' | 'Blog', entityId: string) {
    return this.http.get<{ data: SpeedReadingCmsRevision[] }>(`${this.url}/admin/cms/revisions/${entityType}/${entityId}`)
      .pipe(map(response => response.data ?? []));
  }

  restoreCmsRevision(entityType: 'Page' | 'Blog', entityId: string, revisionId: string) {
    return this.http.post<void>(`${this.url}/admin/cms/revisions/${entityType}/${entityId}/${revisionId}/restore`, {});
  }

  getCmsSubscribers(pageNumber = 1, pageSize = 25, includeInactive = false) {
    return this.http.get<{ data: SpeedReadingPage<SpeedReadingCmsNewsletterSubscriber> }>(`${this.url}/admin/cms/newsletter/subscribers`, {
      params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('includeInactive', includeInactive)
    }).pipe(map(response => response.data));
  }

  deleteCmsSubscriber(id: string, hardDelete = false) {
    return this.http.delete<void>(`${this.url}/admin/cms/newsletter/subscribers/${id}`, {
      params: new HttpParams().set('hardDelete', hardDelete)
    });
  }

  restoreCmsSubscriber(id: string) {
    return this.http.put<void>(`${this.url}/admin/cms/newsletter/subscribers/${id}/restore`, {});
  }

  exportCmsSubscribers(includeInactive = true) {
    return this.http.get(`${this.url}/admin/cms/newsletter/subscribers/export`, {
      params: new HttpParams().set('includeInactive', includeInactive),
      responseType: 'blob'
    });
  }

  getCmsContactMessages(pageNumber = 1, pageSize = 25, isRead?: boolean, isReplied?: boolean) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (isRead !== undefined) params = params.set('isRead', isRead);
    if (isReplied !== undefined) params = params.set('isReplied', isReplied);
    return this.http.get<{ data: SpeedReadingPage<SpeedReadingCmsContactMessage> }>(`${this.url}/admin/cms/contact-messages`, { params })
      .pipe(map(response => response.data));
  }

  getCmsUnreadContactMessageCount() {
    return this.http.get<{ data: number }>(`${this.url}/admin/cms/contact-messages/unread-count`)
      .pipe(map(response => response.data ?? 0));
  }

  markCmsContactMessageRead(id: string, isRead = true) {
    return this.http.put<void>(`${this.url}/admin/cms/contact-messages/${id}/read`, { isRead });
  }

  replyToCmsContactMessage(request: SpeedReadingCmsContactReplyRequest) {
    return this.http.post<void>(`${this.url}/admin/cms/contact-messages/reply`, request);
  }

  deleteCmsContactMessage(id: string) {
    return this.http.delete<void>(`${this.url}/admin/cms/contact-messages/${id}`);
  }

  getAnnouncements(options: { isActive?: boolean; isPinned?: boolean; targetAudience?: number; includeExpired?: boolean; take?: number } = {}) {
    let params = new HttpParams();
    if (options.isActive !== undefined) params = params.set('isActive', options.isActive);
    if (options.isPinned !== undefined) params = params.set('isPinned', options.isPinned);
    if (options.targetAudience !== undefined) params = params.set('targetAudience', options.targetAudience);
    if (options.includeExpired !== undefined) params = params.set('includeExpired', options.includeExpired);
    if (options.take !== undefined) params = params.set('take', options.take);
    return this.http.get<SpeedReadingAnnouncement[]>(`${this.url}/announcements`, { params });
  }

  getAnnouncementStats(id: string) {
    return this.http.get<SpeedReadingAnnouncementStats>(`${this.url}/announcements/${id}/stats`);
  }

  createAnnouncement(request: SpeedReadingAnnouncementRequest) {
    return this.http.post<{ id: string }>(`${this.url}/announcements`, request).pipe(map(response => response.id));
  }

  updateAnnouncement(id: string, request: SpeedReadingAnnouncementUpdateRequest) {
    return this.http.put<void>(`${this.url}/announcements/${id}`, request);
  }

  deleteAnnouncement(id: string) {
    return this.http.delete<void>(`${this.url}/announcements/${id}`);
  }

  getSpeedReadingEmailTemplates() {
    return this.http.get<SpeedReadingEmailTemplate[]>(`${this.url}/email-templates`);
  }

  getSpeedReadingEmailTemplate(id: string) {
    return this.http.get<SpeedReadingEmailTemplate>(`${this.url}/email-templates/${id}`);
  }

  createSpeedReadingEmailTemplate(request: SpeedReadingEmailTemplateRequest) {
    return this.http.post<SpeedReadingEmailTemplate>(`${this.url}/email-templates`, request);
  }

  updateSpeedReadingEmailTemplate(id: string, request: SpeedReadingEmailTemplateRequest) {
    return this.http.put<void>(`${this.url}/email-templates/${id}`, request);
  }

  deleteSpeedReadingEmailTemplate(id: string) {
    return this.http.delete<void>(`${this.url}/email-templates/${id}`);
  }

  previewSpeedReadingEmailTemplate(id: string, variables: Record<string, string> = {}) {
    return this.http.post<{ subject: string; body: string }>(`${this.url}/email-templates/${id}/preview`, variables);
  }

  getSpeedReadingEmailCampaigns(status?: number) {
    let params = new HttpParams();
    if (status !== undefined) params = params.set('status', status);
    return this.http.get<SpeedReadingEmailCampaign[]>(`${this.url}/email-campaigns`, { params });
  }

  getSpeedReadingEmailCampaign(id: string) {
    return this.http.get<{ campaign: SpeedReadingEmailCampaign; body: string; plainTextBody: string | null }>(`${this.url}/email-campaigns/${id}`);
  }

  createSpeedReadingEmailCampaign(request: SpeedReadingEmailCampaignRequest) {
    return this.http.post<SpeedReadingEmailCampaign>(`${this.url}/email-campaigns`, request);
  }

  updateSpeedReadingEmailCampaign(id: string, request: SpeedReadingEmailCampaignRequest) {
    return this.http.put<void>(`${this.url}/email-campaigns/${id}`, request);
  }

  deleteSpeedReadingEmailCampaign(id: string) {
    return this.http.delete<void>(`${this.url}/email-campaigns/${id}`);
  }

  sendSpeedReadingEmailCampaign(id: string, sendNow = true) {
    return this.http.post<{ totalRecipients: number; campaign: SpeedReadingEmailCampaign }>(`${this.url}/email-campaigns/${id}/send`, { sendNow });
  }

  getSpeedReadingEmailCampaignStats(id: string) {
    return this.http.get<SpeedReadingEmailCampaignStats>(`${this.url}/email-campaigns/${id}/stats`);
  }

  getSpeedReadingNotifications(pageNumber = 1, pageSize = 25, options: { userId?: string; type?: number; isRead?: boolean; userRole?: string; searchTerm?: string } = {}) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(options)) {
      if (value !== undefined && value !== '') params = params.set(key, value);
    }
    return this.http.get<SpeedReadingAdminNotificationPage>(`${this.url}/notifications/all`, { params });
  }

  createSpeedReadingNotification(request: SpeedReadingCreateNotificationRequest) {
    return this.http.post<{ id: string }>(`${this.url}/notifications`, request).pipe(map(response => response.id));
  }

  sendSpeedReadingBulkNotification(request: SpeedReadingBulkNotificationRequest) {
    return this.http.post<SpeedReadingBulkNotificationResult>(`${this.url}/notifications/bulk`, request);
  }

  private idempotencyHeaders(idempotencyKey?: string): HttpHeaders {
    return new HttpHeaders({
      'Idempotency-Key': idempotencyKey ?? this.createIdempotencyKey()
    });
  }

  private dateRangeParams(dateFrom?: string, dateTo?: string): HttpParams {
    let params = new HttpParams();
    if (dateFrom) params = params.set('dateFrom', dateFrom);
    if (dateTo) params = params.set('dateTo', dateTo);
    return params;
  }

  private createIdempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
