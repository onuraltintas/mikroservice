// Learning Path Models

export interface PathTemplate {
  id: string;
  name: string;
  description: string;
  targetAgeGroup: string;
  totalNodes: number;
  estimatedDays: number;
  isActive: boolean;
  isStarted: boolean;
  isCompleted: boolean;
  progressPercentage?: number;
}

export interface PathProgress {
  pathProgressId: string;
  templateId: string;
  templateName: string;
  startedAt: Date;
  completedAt?: Date;
  status: string;
  completedNodes: number;
  totalNodes: number;
  completionPercentage: number;
  averageScore?: number;
  totalTimeSpentMinutes: number;
  currentNodeId?: string;
  earnedPoints: number;
  earnedBadges: number;
  nodes: NodeProgress[];
}

export interface NodeProgress {
  nodeId: string;
  title: string;
  description: string;
  nodeType: string;
  level: number;
  orderIndex: number;
  positionX: number;
  positionY: number;
  iconName?: string;
  colorCode?: string;

  // Progress
  status: NodeStatus;
  startedAt?: Date;
  completedAt?: Date;
  completionPercentage: number;
  score?: number;
  stars?: number;
  completedContentCount: number;
  totalContentCount: number;

  // Prerequisites
  prerequisiteNodeIds: string[];
  arePrerequisitesMet: boolean;

  // Content
  contents: NodeContentItem[];
}

export type NodeStatus = 'Locked' | 'Unlocked' | 'InProgress' | 'Completed' | 'Failed';

export interface NodeContentItem {
  contentId: string;
  contentType: string;
  contentTitle?: string;
  orderIndex: number;
  isRequired: boolean;
  isCompleted: boolean;
}

export interface StartPathRequest {
  templateId: string;
}

export interface StartPathResult {
  pathProgressId: string;
  templateId: string;
  message: string;
  totalNodes: number;
  estimatedDays: number;
  firstNodeId?: string;
}

export interface CompleteNodeRequest {
  nodeId: string;
  score: number;
  timeSpentMinutes: number;
  completedContentIds: string[];
}

export interface CompleteNodeResult {
  success: boolean;
  message: string;
  earnedPoints: number;
  stars?: number;
  unlockedNodes: UnlockedNode[];
  pathCompleted: boolean;
  pathCompletionPercentage: number;
}

export interface UnlockedNode {
  nodeId: string;
  title: string;
  description: string;
}

// Helper functions for node styling
export class NodeHelper {
  static getStatusColor(status: NodeStatus): string {
    switch (status) {
      case 'Locked':
        return '#9E9E9E';
      case 'Unlocked':
        return '#2196F3';
      case 'InProgress':
        return '#FF9800';
      case 'Completed':
        return '#4CAF50';
      case 'Failed':
        return '#F44336';
      default:
        return '#9E9E9E';
    }
  }

  static getStatusIcon(status: NodeStatus): string {
    switch (status) {
      case 'Locked':
        return 'lock';
      case 'Unlocked':
        return 'lock_open';
      case 'InProgress':
        return 'schedule';
      case 'Completed':
        return 'check_circle';
      case 'Failed':
        return 'cancel';
      default:
        return 'help';
    }
  }

  static getStarIcon(stars?: number): string {
    if (!stars) return '';
    return '⭐'.repeat(stars);
  }

  static calculateNodeSize(level: number): number {
    // Higher level nodes = larger size
    return 40 + (level * 8);
  }

  static isNodeAccessible(node: NodeProgress): boolean {
    return node.status === 'Unlocked' ||
           node.status === 'InProgress' ||
           node.status === 'Completed';
  }

  static getNodeTypeIcon(nodeType: string): string {
    switch (nodeType) {
      case 'Skill':
        return 'school';
      case 'Milestone':
        return 'flag';
      case 'Checkpoint':
        return 'beenhere';
      default:
        return 'circle';
    }
  }
}

// ==================== PERSONALIZED LEARNING PATH ====================

export interface PersonalizedLearningPathDto {
  totalItems: number;
  completedItems: number;
  remainingItems: number;
  completionPercentage: number;
  currentIndex: number;
  items: PersonalizedLearningPathItemDto[];
  nextItem: PersonalizedLearningPathItemDto | null;
}

export interface PersonalizedLearningPathItemDto {
  id: string;
  pathIndex: number;
  contentType: string;
  contentId: string;
  contentTitle: string;
  difficultyLevel: number;
  estimatedDurationMinutes: number;
  isCompleted: boolean;
  completedAt: Date | null;
  achievedScore: number | null;
  recommendationReason: string | null;
  isUnlocked: boolean;
}

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

export interface CompletePathItemRequest {
  achievedScore?: number;
}

export interface CompletePathItemResponse {
  success: boolean;
  message: string;
  nextItemId: string | null;
  nextItemType: string | null;
  nextItemContentId: string | null;
  completedCount: number;
  totalCount: number;
  completionPercentage: number;
}

// Helper class for personalized learning path
export class PersonalizedLearningPathHelper {
  static getContentTypeIcon(contentType: string): string {
    switch (contentType) {
      case 'ReadingText':
        return 'menu_book';
      case 'Exercise':
        return 'fitness_center';
      case 'ProgramTemplate':
        return 'playlist_play';
      default:
        return 'school';
    }
  }

  static getContentTypeLabel(contentType: string): string {
    switch (contentType) {
      case 'ReadingText':
        return 'Okuma Metni';
      case 'Exercise':
        return 'Egzersiz';
      case 'ProgramTemplate':
        return 'Program Şablonu';
      default:
        return 'İçerik';
    }
  }

  static getDifficultyLabel(level: number): string {
    switch (level) {
      case 1:
        return 'Başlangıç';
      case 2:
        return 'Temel';
      case 3:
        return 'Orta';
      case 4:
        return 'İleri';
      case 5:
        return 'Uzman';
      default:
        return `Seviye ${level}`;
    }
  }

  static getDifficultyColor(level: number): string {
    switch (level) {
      case 1:
        return '#4CAF50'; // Green
      case 2:
        return '#8BC34A'; // Light Green
      case 3:
        return '#FFC107'; // Amber
      case 4:
        return '#FF9800'; // Orange
      case 5:
        return '#F44336'; // Red
      default:
        return '#9E9E9E'; // Grey
    }
  }

  static formatDuration(minutes: number): string {
    if (minutes < 60) {
      return `${minutes} dk`;
    }
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    if (remainingMinutes === 0) {
      return `${hours} saat`;
    }
    return `${hours} saat ${remainingMinutes} dk`;
  }

  static getContentRoute(contentType: string, contentId: string): string[] {
    switch (contentType) {
      case 'ReadingText':
        return ['/student/reading', contentId];
      case 'Exercise':
        return ['/student/exercises', contentId];
      case 'ProgramTemplate':
        return ['/student/daily-exercises'];
      default:
        return ['/student/dashboard'];
    }
  }
}
