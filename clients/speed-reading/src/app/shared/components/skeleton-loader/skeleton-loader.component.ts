import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-skeleton-loader',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="skeleton-loader" [ngClass]="type">
      <div class="skeleton-shimmer"></div>
    </div>
  `,
    styles: [`
    .skeleton-loader {
      position: relative;
      overflow: hidden;
      background: #e5e7eb;
      border-radius: 8px;

      &.text {
        height: 1rem;
        margin-bottom: 0.5rem;
      }

      &.title {
        height: 2rem;
        margin-bottom: 1rem;
      }

      &.card {
        height: 300px;
      }

      &.image {
        height: 200px;
      }

      &.circle {
        width: 48px;
        height: 48px;
        border-radius: 50%;
      }

      .skeleton-shimmer {
        position: absolute;
        top: 0;
        left: -100%;
        width: 100%;
        height: 100%;
        background: linear-gradient(
          90deg,
          transparent,
          rgba(255, 255, 255, 0.6),
          transparent
        );
        animation: shimmer 1.5s infinite;
      }
    }

    @keyframes shimmer {
      0% {
        left: -100%;
      }
      100% {
        left: 100%;
      }
    }
  `]
})
export class SkeletonLoaderComponent {
    @Input() type: 'text' | 'title' | 'card' | 'image' | 'circle' = 'text';
}
