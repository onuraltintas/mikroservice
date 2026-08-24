export interface UserGameification {
  userId: string;
  totalXP: number;
  currentLevel: number;
  currentLevelXP: number;
  nextLevelXP: number;
  levelTitle: string;
  levelIcon: string;
  currentStreak: number;
  longestStreak: number;
  lastActivityDate?: Date;
  streakFreezeCount: number;
  totalActivitiesCompleted: number;
  totalReadingMinutes: number;
  createdAt: Date;
  updatedAt?: Date;
}

export interface Achievement {
  id: string;
  name: string;
  description: string;
  category: AchievementCategory;
  tier: AchievementTier;
  iconUrl: string;
  iconEmoji: string;
  criteriaType: string;
  criteriaValue: string;
  xpReward: number;
  isActive: boolean;
  sortOrder: number;
  createdAt: Date;
}

export interface UserAchievement {
  id: string;
  userId: string;
  achievementId: string;
  achievement?: Achievement;
  unlockedAt: Date;
  isShowcased: boolean;
  showcaseOrder?: number;
}

export interface LevelUpResult {
  leveledUp: boolean;
  oldLevel: number;
  newLevel: number;
  oldTier: number;
  newTier: number;
  isTierChange: boolean;
  levelTitle: string;
  levelIcon: string;
  achievementsUnlocked: Achievement[];
  totalXP: number;
  currentLevelXP: number;
  nextLevelXP: number;
}

export enum AchievementCategory {
  Reading = 'Reading',
  RSVP = 'RSVP',
  Exercise = 'Exercise',
  Streak = 'Streak',
  Progress = 'Progress',
  Special = 'Special'
}

export enum AchievementTier {
  Bronze = 'Bronze',
  Silver = 'Silver',
  Gold = 'Gold',
  Diamond = 'Diamond',
  Special = 'Special'
}

export interface AchievementProgress {
  achievement: Achievement;
  userAchievement?: UserAchievement;
  progress?: {
    current: number;
    target: number;
  };
}

export interface StreakDay {
  date: Date;
  hasActivity: boolean;
  isToday: boolean;
  activityCount: number;
  activityMinutes: number;
  freezeUsed: boolean;
}

export enum LeaderboardType {
  TotalXP = 'TotalXP',
  CurrentLevel = 'CurrentLevel',
  CurrentStreak = 'CurrentStreak',
  LongestStreak = 'LongestStreak',
  TotalActivities = 'TotalActivities',
  ReadingMinutes = 'ReadingMinutes'
}

export interface LeaderboardEntry {
  userId: string;
  userName: string;
  rank: number;
  value: number;
  level: number;
  levelTitle: string;
  levelIcon: string;
  showcasedAchievements: Achievement[];
}
