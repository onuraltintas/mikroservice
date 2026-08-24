/**
 * Age Group Configuration Model
 * Represents age group configuration for personalized recommendations
 */
export interface AgeGroupConfiguration {
  id: string;
  name: string;
  displayName: string;
  minAge: number;
  maxAge: number | null;
  minWPM: number;
  recommendedWPM: number;
  maxWPM: number;
  recommendedComprehension: number;
  recommendedDailyMinutes: number;
  defaultDifficultyLevel: number;  // 1-5 (1=Beginner, 2=Intermediate, 3=Advanced)
  orderIndex: number;
  isActive: boolean;
  description?: string;
  createdAt: Date;
  updatedAt?: Date;

  // Computed property helpers
  ageRangeDisplay?: string;
}

/**
 * Create Age Group Configuration DTO
 * Used for creating new age group configurations
 */
export interface CreateAgeGroupConfiguration {
  name: string;
  displayName: string;
  minAge: number;
  maxAge?: number | null;
  minWPM: number;
  recommendedWPM: number;
  maxWPM: number;
  recommendedComprehension: number;
  recommendedDailyMinutes: number;
  defaultDifficultyLevel: number;  // 1-5 (1=Beginner, 2=Intermediate, 3=Advanced)
  orderIndex: number;
  isActive: boolean;
  description?: string;
}

/**
 * Update Age Group Configuration DTO
 * Used for updating existing age group configurations
 */
export interface UpdateAgeGroupConfiguration extends CreateAgeGroupConfiguration {
  id: string;
}

/**
 * Age Recommendations Response
 * Contains recommended values for a specific age
 */
export interface AgeRecommendations {
  age: number;
  recommendedWPM: number;
  recommendedComprehension: number;
  recommendedDailyMinutes: number;
}
