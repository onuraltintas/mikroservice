import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { Subject, takeUntil } from 'rxjs';
import { LearningPathService, PathStatistics } from '../../../core/services/learning-path.service';
import { PathProgress, NodeProgress, NodeHelper, PathTemplate } from '../../../core/models/learning-path.model';
import { Router } from '@angular/router';

@Component({
  selector: 'app-learning-path',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule
  ],
  templateUrl: './learning-path.html',
  styleUrl: './learning-path.scss'
})
export class LearningPath implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  pathProgress: PathProgress | null = null;
  pathStatistics: PathStatistics | null = null;
  selectedNode: NodeProgress | null = null;
  isLoading = true;
  error: string | null = null;
  NodeHelper = NodeHelper;

  // SVG viewBox dimensions
  svgWidth = 1000;
  svgHeight = 800;

  constructor(
    public learningPathService: LearningPathService,
    private router: Router,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadPathProgress();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPathProgress(): void {
    this.isLoading = true;
    this.error = null;

    this.learningPathService.getProgress()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (progress) => {
          this.pathProgress = progress;
          this.pathStatistics = this.learningPathService.getPathStatistics(progress);

          // Calculate positions if not set
          if (progress.nodes.length > 0) {
            progress.nodes = this.learningPathService.calculateNodePositions(progress.nodes);
          }

          this.isLoading = false;
        },
        error: (err) => {
          console.error('Error loading path progress:', err);
          this.error = 'Öğrenme yolu yüklenirken hata oluştu. Lütfen bir öğrenme yoluna başlayın.';
          this.isLoading = false;
        }
      });
  }

  onNodeClick(node: NodeProgress): void {
    if (!this.learningPathService.isNodeUnlocked(node)) {
      return; // Locked node'a tıklanamaz
    }

    this.selectedNode = node;
    // Node detaylarını göster veya node içeriğine git
    this.openNodeDetails(node);
  }

  openNodeDetails(node: NodeProgress): void {
    // Node contents'e göre uygun sayfaya yönlendir
    if (node.contents.length > 0) {
      const firstContent = node.contents[0];

      switch (firstContent.contentType) {
        case 'ReadingText':
          this.router.navigate(['/student/reading'], {
            queryParams: { nodeId: node.nodeId, contentId: firstContent.contentId }
          });
          break;
        case 'Exercise':
          this.router.navigate(['/student/exercises'], {
            queryParams: { nodeId: node.nodeId, contentId: firstContent.contentId }
          });
          break;
        default:
          console.error('Unknown content type:', firstContent.contentType);
      }
    }
  }

  getNodePosition(node: NodeProgress): { x: number; y: number } {
    // Convert percentage to SVG coordinates
    return {
      x: (node.positionX / 100) * this.svgWidth,
      y: (node.positionY / 100) * this.svgHeight
    };
  }

  getConnectionPath(fromNode: NodeProgress, toNode: NodeProgress): string {
    const from = this.getNodePosition(fromNode);
    const to = this.getNodePosition(toNode);

    // Bezier curve için control points
    const midX = (from.x + to.x) / 2;
    const controlY = Math.abs(from.y - to.y) / 3;

    return `M ${from.x} ${from.y} Q ${midX} ${from.y + controlY}, ${to.x} ${to.y}`;
  }

  getNodeConnections(node: NodeProgress): NodeProgress[] {
    if (!this.pathProgress) return [];

    return this.pathProgress.nodes.filter(n =>
      node.prerequisiteNodeIds.includes(n.nodeId)
    );
  }

  getNodeRadius(node: NodeProgress): number {
    return NodeHelper.calculateNodeSize(node.level);
  }

  getNodeFillColor(node: NodeProgress): string {
    if (node.status === 'Completed') {
      return NodeHelper.getStatusColor('Completed');
    }
    if (node.colorCode) {
      return node.colorCode;
    }
    return this.learningPathService.getNodeColorByLevel(node.level);
  }

  getNodeStrokeColor(node: NodeProgress): string {
    return NodeHelper.getStatusColor(node.status);
  }

  getNodeStrokeWidth(node: NodeProgress): number {
    if (node.status === 'InProgress') {
      return 6;
    }
    if (node.status === 'Completed') {
      return 4;
    }
    return 3;
  }

  getNodeCursor(node: NodeProgress): string {
    return this.learningPathService.isNodeUnlocked(node) ? 'pointer' : 'not-allowed';
  }

  getNodeOpacity(node: NodeProgress): number {
    return node.status === 'Locked' ? 0.4 : 1;
  }

  refreshPath(): void {
    this.loadPathProgress();
  }

  goToDashboard(): void {
    this.router.navigate(['/student/dashboard']);
  }
}
