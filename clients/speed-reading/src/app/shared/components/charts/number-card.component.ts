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
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }

    .number-card {
      text-align: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      transition: transform 0.2s;
    }

    .number-card:hover {
      transform: translateY(-4px);
    }

    .card-label {
      font-size: 14px;
      opacity: 0.9;
      margin-bottom: 8px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .card-value {
      font-size: 32px;
      font-weight: bold;
    }
  `]
})
export class NumberCardComponent {
  @Input() data: NumberCardData[] = [];
}
