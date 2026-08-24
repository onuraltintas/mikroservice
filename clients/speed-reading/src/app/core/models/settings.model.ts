export interface UserSettings {
    // Reading Preferences
    fontSize: number;
    fontFamily: string;
    theme: 'light' | 'dark' | 'sepia';
    lineHeight: number;
    letterSpacing: number;

    // Notification Preferences
    dailyReminder: boolean;
    reminderTime?: string;
    emailNotifications: boolean;
    achievementNotifications: boolean;
    progressReports: boolean;

    // Privacy
    shareProgress: boolean;
    allowAnalytics: boolean;
}

export const DEFAULT_SETTINGS: UserSettings = {
    fontSize: 16,
    fontFamily: 'Arial',
    theme: 'light',
    lineHeight: 150,
    letterSpacing: 0,
    dailyReminder: true,
    reminderTime: '09:00',
    emailNotifications: false,
    achievementNotifications: true,
    progressReports: true,
    shareProgress: false,
    allowAnalytics: true
};

