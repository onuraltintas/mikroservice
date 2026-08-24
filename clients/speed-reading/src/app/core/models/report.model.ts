// Report Metadata
export interface ReportMetadata {
  reportId: string;
  reportType: string;
  generatedAt: Date;
  generatedBy: string;
  startDate: Date;
  endDate: Date;
  parameters?: Record<string, any>;
}

// Chart Data Structures
export interface ChartData {
  name: string;
  series: ChartSeries[];
}

export interface ChartSeries {
  name: string;
  value: number;
}

// Simple chart data for number cards and basic charts
export interface SimpleChartData {
  name: string;
  value: number;
  extra?: any;
}

export interface Benchmark {
  label: string;
  value: number;
  percentile?: number;
  color?: string;
}

// ==================== STUDENT REPORTS ====================

// 1. Student Dashboard Report
export interface StudentDashboardReport {
  metadata: ReportMetadata;

  // Direct properties (not nested in summary)
  totalActivities: number;
  totalReadingTime: number;
  currentWPM: number;
  averageComprehension: number;
  currentLevel: number;
  daysActive: number;
  dailyGoalMinutes: number;
  goalCompletionRate: number;
  streak: number;
  longestStreak: number;
  totalXP: number;
  milestonesEarned: number;

  // Chart data - matching backend DTO names
  activityTrend?: ChartData[];
  activityDistribution?: ChartData[];
  wpmProgress?: ChartData[];
  comprehensionTrend?: ChartData[];
  recentMilestones?: RecentMilestone[];
}

export interface RecentMilestone {
  id: string;
  title: string;
  description: string;
  earnedAt: Date;
  type: 'speed' | 'comprehension' | 'streak' | 'completion' | 'achievement';
  icon?: string;
  color?: string;
}

// 2. Student Reading Speed Report
export interface StudentReadingSpeedReport {
  metadata: ReportMetadata;
  currentWPM: number;
  wpmBenchmarks: WPMBenchmark[];
  wpmTrendChart: WPMTrendChart;
  categoryWPMChart: CategoryWPMChart;
  statistics: ReadingSpeedStatistics;
}

export interface WPMBenchmark {
  label: string;
  value: number;
  percentile?: number;
  color?: string;
}

export interface WPMTrendChart {
  data: ChartSeries[];
  labels: string[];
}

export interface CategoryWPMChart {
  data: ChartData[];
}

export interface ReadingSpeedStatistics {
  averageWPM: number;
  medianWPM: number;
  minWPM: number;
  maxWPM: number;
  standardDeviation: number;
  improvementRate: number;
  totalReadings: number;
}

// 3. Student Comprehension Report
export interface StudentComprehensionReport {
  metadata: ReportMetadata;
  overallComprehension: number;
  comprehensionBenchmarks: ComprehensionBenchmark[];
  questionTypeChart: QuestionTypeChart;
  categoryComprehensionChart: CategoryComprehensionChart;
  categoryBreakdown: CategoryComprehensionBreakdown[];
  improvementAreas: ImprovementArea[];
}

export interface ComprehensionBenchmark {
  label: string;
  value: number;
  percentile?: number;
  color?: string;
}

export interface QuestionTypeChart {
  data: ChartSeries[];
  labels: string[];
}

export interface CategoryComprehensionChart {
  data: ChartData[];
}

export interface CategoryComprehensionBreakdown {
  categoryId: string;
  categoryName: string;
  comprehensionRate: number;
  questionsAnswered: number;
  correctAnswers: number;
  averageTime: number;
}

export interface ImprovementArea {
  area: string;
  currentLevel: number;
  targetLevel: number;
  recommendations: string[];
  priority: 'high' | 'medium' | 'low';
}

// 4. Student Series Report
export interface StudentSeriesReport {
  metadata: ReportMetadata;
  dataAvailable?: boolean;
  unavailableReason?: string;
  summary: {
    totalSeriesStarted: number;
    seriesCompleted: number;
    seriesInProgress: number;
    totalMilestones: number;
  };
  activeSeries: ActiveSeries[];
  completionTimelineChart: CompletionTimelineChart;
  milestones: Milestone[];
  performanceStats: SeriesPerformanceStats;
}

export interface ActiveSeries {
  seriesId: string;
  seriesName: string;
  progress: number;
  daysCompleted: number;
  totalDays: number;
  startedAt: Date;
  lastActivityAt: Date | null;
  averageScore: number;
}

export interface CompletionTimelineChart {
  data: ChartSeries[];
  labels: string[];
}

export interface Milestone {
  id: string;
  title: string;
  description: string;
  earnedAt: Date;
  seriesId: string;
  seriesName: string;
  type: string;
  icon?: string;
  color?: string;
}

export interface SeriesPerformanceStats {
  averageCompletionTime: number;
  averageScore: number;
  consistencyScore: number;
  engagementLevel: 'high' | 'medium' | 'low';
}

// 5. Student Activity Report
export interface StudentActivityReport {
  metadata: ReportMetadata;
  dataAvailable?: boolean;
  unavailableReason?: string;
  currentStreak: CurrentStreak;
  activityHeatmap: ActivityHeatmap;
  hourlyDistributionChart: HourlyDistributionChart;
  dailyDistributionChart: DailyDistributionChart;
  studyTime: StudyTimeStats;
}

export interface CurrentStreak {
  days: number;
  longestStreak: number;
  lastActivityDate: Date | null;
  isActive: boolean;
}

export interface ActivityHeatmap {
  data: HeatmapData[];
}

export interface HeatmapData {
  date: string;
  value: number;
  level: 0 | 1 | 2 | 3 | 4;
}

export interface HourlyDistributionChart {
  data: ChartData[];
}

export interface DailyDistributionChart {
  data: ChartData[];
}

export interface StudyTimeStats {
  totalMinutes: number;
  averageSessionLength: number;
  totalSessions: number;
  mostActiveHour: number;
  mostActiveDay: string;
  consistency: number;
}

// ==================== TEACHER REPORTS ====================

// 1. Teacher Class Overview Report
export interface TeacherClassOverviewReport {
  metadata: ReportMetadata;
  totalStudents: number;
  activeStudents: number;
  classAverageWPM: number;
  classAverageComprehension: number;
  totalActivitiesCompleted: number;
  studentsAboveAverage: number;
  studentsAtAverage: number;
  studentsBelowAverage: number;
  topPerformers: AnonymousStudentPerformance[];
  studentsNeedingSupport: AnonymousStudentPerformance[];
}

export interface AnonymousStudentPerformance {
  studentIdentifier: string;
  averageWPM: number;
  averageComprehension: number;
  activitiesCompleted: number;
  totalMinutes: number;
  performanceLevel: string;
}

// Legacy interfaces kept for backward compatibility
export interface ClassKPIs {
  totalStudents: number;
  activeStudents: number;
  averagePerformance: number;
  averageEngagement: number;
  completionRate: number;
}

export interface PerformanceDistribution {
  data: ChartData[];
}

export interface StudentPerformanceSummary {
  studentId: string;
  studentName: string;
  averageScore: number;
  activitiesCompleted: number;
  lastActivity: Date;
  performanceTrend: 'improving' | 'stable' | 'declining';
}

export interface AssignmentCompletionChart {
  data: ChartData[];
}

export interface ClassActivityTrendChart {
  data: ChartSeries[];
  labels: string[];
}

// 2. Teacher Student Detail Report
export interface TeacherStudentDetailReport {
  metadata: ReportMetadata;
  studentInfo: {
    studentId: string;
    studentName: string;
    enrollmentDate: Date;
    lastActivity: Date;
  };
  comparisonChart: StudentComparisonChart;
  studentReports: {
    dashboard: StudentDashboardReport;
    readingSpeed: StudentReadingSpeedReport;
    comprehension: StudentComprehensionReport;
    series: StudentSeriesReport;
    activity: StudentActivityReport;
  };
  strengths: string[];
  weaknesses: string[];
  recommendations: string[];
}

export interface StudentComparisonChart {
  data: ChartSeries[];
  labels: string[];
}

// 3. Teacher Assignment Report
export interface TeacherAssignmentReport {
  metadata: ReportMetadata;
  assignmentInfo: {
    assignmentId: string;
    title: string;
    description: string;
    dueDate: Date;
    assignedDate: Date;
  };
  completionStats: {
    totalStudents: number;
    completed: number;
    inProgress: number;
    notStarted: number;
    completionRate: number;
  };
  performanceStats: {
    averageScore: number;
    medianScore: number;
    highestScore: number;
    lowestScore: number;
    standardDeviation: number;
  };
  scoreDistribution: ScoreDistributionChart;
  studentBreakdown: AssignmentStudentBreakdown[];
  timeStats: {
    averageCompletionTime: number;
    medianCompletionTime: number;
    fastestCompletion: number;
    slowestCompletion: number;
  };
}

export interface ScoreDistributionChart {
  data: ChartData[];
}

export interface AssignmentStudentBreakdown {
  studentId: string;
  studentName: string;
  status: 'completed' | 'in-progress' | 'not-started';
  score?: number;
  completionTime?: number;
  submittedAt?: Date;
}

// 4. Teacher Content Analysis Report
export interface TeacherContentAnalysisReport {
  metadata: ReportMetadata;
  exerciseAnalysis: ExerciseTypeAnalysis[];
  exerciseFrequencyChart: ChartData[];
  readingAnalysis: ReadingLevelAnalysis[];
  readingPerformanceChart: ChartData[];
}

export interface ExerciseTypeAnalysis {
  exerciseTypeName: string;
  totalCompletions: number; // matches backend usage
  activeStudents: number;
  averageScore: number;
  performanceLevel: string;
}

export interface ReadingLevelAnalysis {
  difficultyLevel: number;
  totalReads: number;
  averageWPM: number;
  averageComprehension: number;
}

// 5. Teacher Time-Based Progress Report
export interface TeacherTimeBasedProgressReport {
  metadata: ReportMetadata;
  weeklyProgressChart: ChartData[];
  monthlyProgressChart: ChartData[];
  activityIntensityChart: ChartData[];
  improvingStudents: StudentProgressSummary[];
  decliningStudents: StudentProgressSummary[];
}

// Wrappers removed as they are not used anymore
// Keeping interfaces if they are used elsewhere?
// WeeklyProgressChart, ActivityIntensityChart interfaces were defined below this class.
// I should remove them if unused or keep them but not use them here.

export interface StudentProgressSummary {
  studentId: string;
  studentName: string;
  previousScore: number;
  currentScore: number;
  improvement: number;
  trend: 'improving' | 'declining';
}

// ==================== ADMIN REPORTS ====================

// 1. Admin Institution Report
export interface AdminInstitutionReport {
  metadata: ReportMetadata;
  totalInstitutions: number;
  activeInstitutions: number;
  totalUsers: number;
  totalStudents: number;
  totalTeachers: number;
  institutionComparison: InstitutionComparisonData[];
  institutionComparisonChart: ChartData;
  usersByInstitution: ChartData[];
  activityByInstitution: ChartData[];
  performanceByInstitution: ChartData[];
  topInstitutions: TopInstitution[];
}

export interface InstitutionComparisonData {
  institutionId: string;
  institutionName: string;
  totalUsers: number;
  activeUsers: number;
  totalStudents: number;
  totalTeachers: number;
  totalActivities: number;
  averageWPM: number;
  averageWPMDataAvailable?: boolean;
  averageComprehension: number;
  averageComprehensionDataAvailable?: boolean;
  averagePerformance: number;
  engagementRate: number;
}

export interface InstitutionComparisonChart {
  data: ChartSeries[];
  labels: string[];
}

export interface TopInstitution {
  institutionName: string;
  averageWPM: number;
  averageWPMDataAvailable?: boolean;
  averageComprehension: number;
  averageComprehensionDataAvailable?: boolean;
  activeStudents: number;
  activeStudentsDataAvailable?: boolean;
  totalActivities: number;
}

// 2. Admin Platform Usage Report
export interface AdminPlatformUsageReport {
  metadata: ReportMetadata;
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  newUserDataAvailable?: boolean;
  totalActivities: number;
  totalReadingSessions: number;
  averageSessionDuration: number;
  userGrowthRate: number;
  userGrowthRateDataAvailable?: boolean;
  engagementRate: number;
  retentionRate: number;
  userGrowth: ChartData[];
  dailyActiveUsers: ChartData[];
  activityVolume: ChartData[];
  hourlyActivity: ChartData[];
  popularContent: PopularContent[];
  topInstitutions: InstitutionActivity[];
  featureUsageStats: { [key: string]: number };
}

export interface PopularContent {
  title: string;
  type: string;
  usageCount: number;
}

export interface InstitutionActivity {
  name: string;
  activeUserCount: number;
  totalActionCount: number;
}

// 3. Admin Content Analysis Report
export interface AdminContentAnalysisReport {
  metadata: ReportMetadata;
  totalExercises: number;
  totalReadingTexts: number;
  totalTrainingSeries: number; // Legacy - kept for backward compatibility
  totalProgramTemplates?: number; // New system
  totalAssignments: number;
  assignmentDataAvailable?: boolean;
  mostUsedContent: ContentUsageData[];
  leastUsedContent: ContentUsageData[];
  performanceByContentType: ChartData[];
  engagementByContentType: ChartData[];
  contentGaps: string[];
  popularTopics: string[];
  readingAnalysis: ReadingLevelAnalysis[];
  exerciseAnalysis: ExerciseTypeAnalysis[];
  readingPerformanceChart: ChartData[];
  exerciseFrequencyChart: ChartData[];
}

export interface ContentUsageData {
  contentId: string;
  contentType: string;
  title: string;
  usageCount: number;
  averageScore: number;
  averageComprehension: number;
}

// 4. Admin System Health Report
export interface AdminSystemHealthReport {
  metadata: ReportMetadata;
  overallHealthScore: number;
  overallHealthDataAvailable?: boolean;
  healthStatus: string;
  averagePlatformWPM: number;
  averagePlatformComprehension: number;
  userSatisfactionScore: number;
  userSatisfactionDataAvailable?: boolean;
  totalExercisesCompleted: number;
  totalQuestionsAnswered: number;
  successRate: number;
  errorRate: number;
  errorRateDataAvailable?: boolean;
  systemAlertsDataAvailable?: boolean;
  healthTrend: ChartData[];
  performanceTrend: ChartData[];
  systemAlerts: SystemAlert[];
}

export interface SystemAlert {
  severity: string;
  alertType: string;
  message: string;
  detectedAt: Date;
}

// ==================== REPORT TEMPLATES & SCHEDULING ====================

export interface ReportTemplate {
  id: string;
  name: string;
  description: string;
  reportType: string;
  category: 'student' | 'teacher' | 'admin';
  metrics: string[];
  filters: Record<string, any>;
  createdBy: string;
  createdAt: Date;
  updatedAt: Date;
  isActive?: boolean;
  configurationJson?: string;
  type?: string;
}

export interface ScheduledReport {
  id: string;
  templateId: string;
  name: string;
  frequency: 'daily' | 'weekly' | 'monthly';
  dayOfWeek?: number;
  dayOfMonth?: number;
  time: string;
  recipients: string[];
  isActive: boolean;
  lastRun?: Date;
  nextRun: Date;
  deliveryOptions: {
    sendEmail: boolean;
    saveToDashboard: boolean;
  };
}

export interface ReportSnapshot {
  id: string;
  reportType: string;
  templateId?: string;
  data: any;
  generatedAt: Date;
  generatedBy: string;
  expiresAt?: Date;
}

// ==================== EXPORT OPTIONS ====================

export interface ReportExportOptions {
  format: 'pdf' | 'excel' | 'csv';
  includeCharts: boolean;
  includeRawData: boolean;
  orientation?: 'portrait' | 'landscape';
}

// ==================== DATE RANGE ====================

export interface DateRange {
  startDate: Date;
  endDate: Date;
  preset?: 'last7days' | 'last30days' | 'last3months' | 'custom';
}

// ==================== ADAPTIVE PERFORMANCE REPORT ====================

export interface AdaptivePerformanceReport {
  studentId: string;
  studentName: string;
  startDate?: Date;
  endDate?: Date;

  // Overall adaptive statistics
  totalAdaptiveSessions: number;
  totalSpeedAdjustments: number;
  averageSpeedImprovement: number; // Percentage
  adaptiveEffectiveness: number; // Score 0-100

  // Exercise-specific stats
  exerciseStats: ExerciseAdaptiveStats[];

  // Trend data for charts
  speedProgressionTrend: AdaptiveTrendPoint[];
  accuracyTrend: AdaptiveTrendPoint[];
}

export interface ExerciseAdaptiveStats {
  exerciseId: string;
  exerciseTitle: string;
  exerciseType: string;

  sessionCount: number;
  totalAdjustments: number;

  // Speed metrics
  averageInitialSpeed?: number; // ms
  averageFinalSpeed?: number; // ms
  averageSpeedImprovement: number; // Percentage

  // Performance metrics
  averageAccuracy: number; // Percentage
  averageMaxStreak: number;

  // Adaptive effectiveness
  adaptiveEffectiveness: number; // 0-100 score

  // Last session info
  lastSessionDate?: Date;
  lastSessionSpeedImprovement?: number;
}

export interface AdaptiveTrendPoint {
  date: Date;
  value: number;
  label: string;

  // Additional context
  sessionCount: number;
  exerciseType?: string;
}
