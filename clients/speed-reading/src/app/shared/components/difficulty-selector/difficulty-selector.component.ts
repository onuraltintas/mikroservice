import { Component, Input, Output, EventEmitter, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DifficultyLevel } from '../../../core/models/difficulty-level.model';

@Component({
  selector: 'app-difficulty-selector',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule
  ],
  templateUrl: './difficulty-selector.component.html',
  styleUrls: ['./difficulty-selector.component.scss']
})
export class DifficultySelectorComponent implements OnInit {
  @Input() difficultyLevels: DifficultyLevel[] = [];
  @Input() defaultLevel?: number;
  @Input() backButtonText: string = 'Egzersizlere Dön';
  @Output() difficultySelected = new EventEmitter<number>();
  @Output() returnToExercises = new EventEmitter<void>();

  selectedLevel = signal<number | null>(null);

  ngOnInit() {
    if (this.defaultLevel) {
      this.selectedLevel.set(this.defaultLevel);
    } else {
      // Auto-select recommended level
      const recommended = this.difficultyLevels.find(l => l.recommended);
      if (recommended) {
        this.selectedLevel.set(recommended.level);
      }
    }
  }

  selectDifficulty(level: number) {
    this.selectedLevel.set(level);
    this.difficultySelected.emit(level);
  }

  goBackToExercises() {
    this.returnToExercises.emit();
  }
}
