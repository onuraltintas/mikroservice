import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  PathTemplate,
  PathProgress,
  StartPathRequest,
  StartPathResult,
  CompleteNodeRequest,
  CompleteNodeResult,
  NodeProgress,
  PersonalizedLearningPathDto,
  LearningPathProgressDto,
  CompletePathItemRequest,
  CompletePathItemResponse,
  NodeStatus
} from '../models/learning-path.model';

@Injectable({
  providedIn: 'root'
})
export class LearningPathService {
  private apiUrl = `${environment.speedReadingApiUrl}/learning-paths`;

  // Current path progress state
  private currentPathProgressSubject = new BehaviorSubject<PathProgress | null>(null);
  public currentPathProgress$ = this.currentPathProgressSubject.asObservable();

  // Current node state
  private currentNodeSubject = new BehaviorSubject<NodeProgress | null>(null);
  public currentNode$ = this.currentNodeSubject.asObservable();

  // Personalized learning path state
  private personalizedPathSubject = new BehaviorSubject<LearningPathProgressDto | null>(null);
  public personalizedPath$ = this.personalizedPathSubject.asObservable();

  constructor(private http: HttpClient) { }

  /**
   * Available path template'lerini getir
   */
  getTemplates(ageGroup?: number): Observable<PathTemplate[]> {
    let params = new HttpParams();
    if (ageGroup) {
      params = params.set('ageGroup', ageGroup.toString());
    }
    return this.http.get<SpeedReadingLearningPathTemplate[]>(`${this.apiUrl}/templates`, { params }).pipe(
      map(templates => templates.map(template => ({
        id: template.id,
        name: template.name,
        description: template.description ?? '',
        targetAgeGroup: '',
        totalNodes: template.totalNodes,
        estimatedDays: template.estimatedDays,
        isActive: true,
        isStarted: false,
        isCompleted: false
      })))
    );
  }

  /**
   * Kullanıcının path progress'ini getir
   */
  getProgress(templateId?: string): Observable<PathProgress> {
    let params = new HttpParams();
    if (templateId) {
      params = params.set('templateId', templateId);
    }

    return this.http.get<SpeedReadingLearningPathProgress>(`${this.apiUrl}/progress`, { params }).pipe(
      map(progress => toPathProgress(progress)),
      tap(progress => {
        this.currentPathProgressSubject.next(progress);

        // Current node'u set et
        if (progress.currentNodeId) {
          const currentNode = progress.nodes.find(n => n.nodeId === progress.currentNodeId);
          if (currentNode) {
            this.currentNodeSubject.next(currentNode);
          }
        }
      })
    );
  }

  /**
   * Yeni bir path'e başla
   */
  startPath(templateId: string): Observable<StartPathResult> {
    const request: StartPathRequest = { templateId };
    return this.http.post<StartPathResult>(`${this.apiUrl}/start`, request).pipe(
      tap(result => {
        // Start edince progress'i refresh et
        this.getProgress(templateId).subscribe();
      })
    );
  }

  /**
   * Node'u tamamla
   */
  completeNode(
    nodeId: string,
    score: number,
    timeSpentMinutes: number,
    completedContentIds: string[]
  ): Observable<CompleteNodeResult> {
    const request: CompleteNodeRequest = {
      nodeId,
      score,
      timeSpentMinutes,
      completedContentIds
    };

    return this.http.post<CompleteNodeResult>(
      `${this.apiUrl}/node/${nodeId}/complete`,
      request
    ).pipe(
      tap(result => {
        // Complete edince progress'i refresh et
        const currentProgress = this.currentPathProgressSubject.value;
        if (currentProgress) {
          this.getProgress(currentProgress.templateId).subscribe();
        }
      })
    );
  }

  /**
   * Specific node detaylarını getir
   */
  getNodeDetails(nodeId: string): Observable<NodeProgress> {
    return this.http.get<NodeProgress>(`${this.apiUrl}/node/${nodeId}`);
  }

  /**
   * Current path'i state'e set et
   */
  setCurrentPath(progress: PathProgress): void {
    this.currentPathProgressSubject.next(progress);
  }

  /**
   * Current node'u state'e set et
   */
  setCurrentNode(node: NodeProgress): void {
    this.currentNodeSubject.next(node);
  }

  /**
   * Path'i clear et (logout vb için)
   */
  clearPath(): void {
    this.currentPathProgressSubject.next(null);
    this.currentNodeSubject.next(null);
  }

  /**
   * Node'un unlock olup olmadığını kontrol et
   */
  isNodeUnlocked(node: NodeProgress): boolean {
    return node.status === 'Unlocked' ||
      node.status === 'InProgress' ||
      node.status === 'Completed';
  }

  /**
   * Path completion yüzdesini hesapla
   */
  calculateCompletionPercentage(progress: PathProgress): number {
    if (progress.totalNodes === 0) return 0;
    return Math.round((progress.completedNodes / progress.totalNodes) * 100);
  }

  /**
   * Node'ların grid pozisyonlarını hesapla (eğer backend'den gelmiyorsa)
   */
  calculateNodePositions(nodes: NodeProgress[]): NodeProgress[] {
    // Backend'den gelen pozisyonlar varsa kullan
    if (nodes.length > 0 && nodes[0].positionX !== 0) {
      return nodes;
    }

    // Yoksa otomatik grid layout yap
    const columns = 4;
    return nodes.map((node, index) => {
      const row = Math.floor(index / columns);
      const col = index % columns;

      return {
        ...node,
        positionX: (col * 25) + 12.5, // 0-100 arası yüzde
        positionY: (row * 20) + 10
      };
    });
  }

  /**
   * Level'a göre node rengini hesapla
   */
  getNodeColorByLevel(level: number): string {
    const colors = [
      '#4CAF50', // Level 1 - Green
      '#2196F3', // Level 2 - Blue
      '#FF9800', // Level 3 - Orange
      '#9C27B0', // Level 4 - Purple
      '#F44336'  // Level 5 - Red
    ];
    return colors[Math.min(level - 1, colors.length - 1)];
  }

  /**
   * Next accessible node'u bul
   */
  getNextAccessibleNode(progress: PathProgress): NodeProgress | null {
    const unlockedNodes = progress.nodes.filter(
      n => n.status === 'Unlocked' || n.status === 'InProgress'
    );

    if (unlockedNodes.length === 0) return null;

    // En düşük orderIndex'e sahip olanı döndür
    return unlockedNodes.reduce((prev, curr) =>
      prev.orderIndex < curr.orderIndex ? prev : curr
    );
  }

  /**
   * Path statistics hesapla
   */
  getPathStatistics(progress: PathProgress): PathStatistics {
    const completedNodes = progress.nodes.filter(n => n.status === 'Completed');
    const totalStars = completedNodes.reduce((sum, node) => sum + (node.stars || 0), 0);
    const averageStars = completedNodes.length > 0 ? totalStars / completedNodes.length : 0;

    return {
      totalNodes: progress.totalNodes,
      completedNodes: progress.completedNodes,
      lockedNodes: progress.nodes.filter(n => n.status === 'Locked').length,
      unlockedNodes: progress.nodes.filter(n => n.status === 'Unlocked').length,
      inProgressNodes: progress.nodes.filter(n => n.status === 'InProgress').length,
      averageScore: progress.averageScore || 0,
      averageStars: averageStars,
      totalStars: totalStars,
      earnedPoints: progress.earnedPoints,
      earnedBadges: progress.earnedBadges,
      timeSpentHours: Math.round(progress.totalTimeSpentMinutes / 60 * 10) / 10
    };
  }

  // ==================== PERSONALIZED LEARNING PATH METHODS ====================

  /**
   * Personalized learning path'i getir
   * Backend pageNumber/pageSize kabul ediyor; onlyIncomplete client-side uygulanır
   */
  getPersonalizedLearningPath(
    pageNumber: number = 1,
    pageSize: number = 20
  ): Observable<PersonalizedLearningPathDto> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<SpeedReadingPage<SpeedReadingPersonalizedItem>>(`${this.apiUrl}/personalized`, { params }).pipe(
      map(page => toPersonalizedPath(page))
    );
  }

  /**
   * Personalized learning path progress getir (summary)
   */
  getPersonalizedLearningPathProgress(): Observable<LearningPathProgressDto> {
    return this.getPersonalizedLearningPath(1, 100).pipe(
      map(path => toPersonalizedProgress(path)),
      tap(progress => {
        this.personalizedPathSubject.next(progress);
      })
    );
  }

  /**
   * Sıradaki item'ı getir
   */
  getNextPersonalizedItem(): Observable<any> {
    return this.http.get(`${this.apiUrl}/personalized/next`);
  }

  /**
   * Personalized path auto-generate (ilk kez kullanımda)
   */
  generatePersonalizedPath(): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/personalized/generate`, {}).pipe(
      tap(() => this.getPersonalizedLearningPathProgress().subscribe())
    );
  }

  /**
   * Path item'ı tamamla
   */
  completePersonalizedPathItem(
    pathItemId: string,
    achievedScore?: number
  ): Observable<CompletePathItemResponse> {
    const request: CompletePathItemRequest = { achievedScore };

    return this.http.post<CompletePathItemResponse>(
      `${this.apiUrl}/personalized/${pathItemId}/complete`,
      request
    ).pipe(
      tap(() => {
        // Interceptor zaten unwrap etti, doğrudan progress refresh et
        this.getPersonalizedLearningPathProgress().subscribe();
      })
    );
  }

  /**
   * Personalized path state'i clear et
   */
  clearPersonalizedPath(): void {
    this.personalizedPathSubject.next(null);
  }
}

interface SpeedReadingPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

interface SpeedReadingLearningPathTemplate {
  id: string;
  name: string;
  description?: string;
  totalNodes: number;
  estimatedDays: number;
}

interface SpeedReadingLearningPathProgress {
  id: string;
  templateId: string;
  progress: number;
  isCompleted: boolean;
  currentNodeId?: string;
  nodes: SpeedReadingLearningPathNode[];
  nodeProgress: SpeedReadingLearningPathNodeProgress[];
}

interface SpeedReadingLearningPathNode {
  id: string;
  parentNodeId?: string;
  nodeType: string;
  title: string;
  contentType?: string;
  contentId?: string;
  order: number;
  contents: Array<{ id: string; exerciseId?: string; readingTextId?: string; description?: string }>;
  prerequisiteNodeIds: string[];
}

interface SpeedReadingLearningPathNodeProgress {
  nodeId: string;
  status: string;
  score?: number;
  completedAt?: string;
}

interface SpeedReadingPersonalizedItem {
  id: string;
  pathIndex: number;
  contentType: string;
  contentId?: string;
  contentTitle: string;
  difficultyLevel: number;
  estimatedDurationMinutes: number;
  isCompleted: boolean;
  completedAt?: string;
  achievedScore?: number;
  recommendationReason?: string;
  isUnlocked: boolean;
}

function toPathProgress(progress: SpeedReadingLearningPathProgress): PathProgress {
  const progressByNode = new Map(progress.nodeProgress.map(item => [item.nodeId, item]));
  const nodes = progress.nodes.map(node => {
    const nodeProgress = progressByNode.get(node.id);
    const completed = nodeProgress?.status === 'Completed';
    return {
      nodeId: node.id,
      title: node.title,
      description: '',
      nodeType: node.nodeType,
      level: 1,
      orderIndex: node.order,
      positionX: 0,
      positionY: 0,
      status: (nodeProgress?.status ?? 'Locked') as NodeStatus,
      completedAt: nodeProgress?.completedAt ? new Date(nodeProgress.completedAt) : undefined,
      completionPercentage: completed ? 100 : 0,
      score: nodeProgress?.score,
      completedContentCount: completed ? node.contents.length : 0,
      totalContentCount: node.contents.length,
      prerequisiteNodeIds: node.prerequisiteNodeIds,
      arePrerequisitesMet: node.prerequisiteNodeIds.length === 0,
      contents: node.contents.map((content, index) => ({
        contentId: content.exerciseId ?? content.readingTextId ?? content.id,
        contentType: content.exerciseId ? 'Exercise' : 'ReadingText',
        contentTitle: content.description,
        orderIndex: index,
        isRequired: true,
        isCompleted: completed
      }))
    };
  });
  const completedNodes = nodes.filter(node => node.status === 'Completed').length;
  return {
    pathProgressId: progress.id,
    templateId: progress.templateId,
    templateName: '',
    startedAt: new Date(),
    status: progress.isCompleted ? 'Completed' : 'InProgress',
    completedNodes,
    totalNodes: nodes.length,
    completionPercentage: progress.progress,
    totalTimeSpentMinutes: 0,
    currentNodeId: progress.currentNodeId,
    earnedPoints: 0,
    earnedBadges: 0,
    nodes
  };
}

function toPersonalizedPath(page: SpeedReadingPage<SpeedReadingPersonalizedItem>): PersonalizedLearningPathDto {
  const items = page.items.map(item => ({
    id: item.id,
    pathIndex: item.pathIndex,
    contentType: item.contentType,
    contentId: item.contentId ?? '',
    contentTitle: item.contentTitle,
    difficultyLevel: item.difficultyLevel,
    estimatedDurationMinutes: item.estimatedDurationMinutes,
    isCompleted: item.isCompleted,
    completedAt: item.completedAt ? new Date(item.completedAt) : null,
    achievedScore: item.achievedScore ?? null,
    recommendationReason: item.recommendationReason ?? null,
    isUnlocked: item.isUnlocked
  }));
  const nextItem = items.find(item => item.isUnlocked && !item.isCompleted) ?? null;
  return {
    totalItems: page.totalCount,
    completedItems: items.filter(item => item.isCompleted).length,
    remainingItems: Math.max(page.totalCount - items.filter(item => item.isCompleted).length, 0),
    completionPercentage: page.totalCount === 0 ? 0 : Math.round((items.filter(item => item.isCompleted).length / page.totalCount) * 100),
    currentIndex: nextItem?.pathIndex ?? 0,
    items,
    nextItem
  };
}

function toPersonalizedProgress(path: PersonalizedLearningPathDto): LearningPathProgressDto {
  return {
    totalItems: path.totalItems,
    completedItems: path.completedItems,
    remainingItems: path.remainingItems,
    completionPercentage: path.completionPercentage,
    currentIndex: path.currentIndex,
    nextItem: path.nextItem ? {
      id: path.nextItem.id,
      contentType: path.nextItem.contentType,
      contentId: path.nextItem.contentId,
      contentTitle: path.nextItem.contentTitle,
      difficultyLevel: path.nextItem.difficultyLevel,
      estimatedDurationMinutes: path.nextItem.estimatedDurationMinutes,
      recommendationReason: path.nextItem.recommendationReason
    } : null
  };
}

export interface PathStatistics {
  totalNodes: number;
  completedNodes: number;
  lockedNodes: number;
  unlockedNodes: number;
  inProgressNodes: number;
  averageScore: number;
  averageStars: number;
  totalStars: number;
  earnedPoints: number;
  earnedBadges: number;
  timeSpentHours: number;
}
