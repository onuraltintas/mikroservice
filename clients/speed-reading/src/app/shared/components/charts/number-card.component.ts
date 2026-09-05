import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';

export interface NumberCardData {
  name: string;
  value: number | string;
}

@Component({
  selector: 'app-number-card',
  standalone: true,
  imports: [CommonModule, MatCardModule],
  template: `
    <div class="number-cards-container">
      <mat-card *ngFor="let card of data" class="number-card">
        <mat-card-content>
          <div class="card-label">{{ card.name }}</div>
          <div class="card-value">{{ card.value }}</div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .number-cards-container {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: var(--ui-space-4, 16px);
      margin-bottom: var(--ui-space-6, 24px);
    }

    .number-card {
      text-align: center;
      background: linear-gradient(135deg, var(--ui-brand) 0%, var(--ui-accent) 100%);
      border: 0;
      border-radius: var(--ui-radius-lg, 16px);
      box-shadow: var(--ui-shadow-md);
      color: white;
      transition: transform var(--ui-transition-fast, 150ms ease), box-shadow var(--ui-transition-fast, 150ms ease);
    }

    .number-card:hover {
      transform: translateY(-4px);
      box-shadow: var(--ui-shadow-lg);
    }

    .card-label {
      font-size: 0.8125rem;
      opacity: 0.9;
      margin-bottom: var(--ui-space-2, 8px);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .card-value {
      font-size: clamp(1.75rem, 1.25rem + 1vw, 2rem);
      font-weight: bold;
    }
  `]
})
export class NumberCardComponent {
  @Input() data: NumberCardData[] = [];
}
