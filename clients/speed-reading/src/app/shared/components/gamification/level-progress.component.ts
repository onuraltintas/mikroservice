import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';

@Component({
  selector: 'app-level-progress',
  standalone: true,
  imports: [CommonModule, MatProgressBarModule],
  templateUrl: './level-progress.component.html',
  styleUrls: ['./level-progress.component.scss']
})
export class LevelProgressComponent implements OnInit {
  @Input() currentLevel: number = 1;
  @Input() currentLevelXP: number = 0;
  @Input() nextLevelXP: number = 100;

  progressPercentage: number = 0;
  levelColor: string = '#9E9E9E';
  levelGradient: string = '';
  levelTitle: string = 'Başlangıç Okuyucu';
  levelIcon: string = '📖';

  ngOnInit() {
    this.progressPercentage = (this.currentLevelXP / this.nextLevelXP) * 100;
    this.setLevelInfo();
  }

  private setLevelInfo() {
    const tier = Math.ceil(this.currentLevel / 5);
    switch (tier) {
      case 1:
        this.levelColor = '#9E9E9E';
        this.levelGradient = 'linear-gradient(135deg, #9E9E9E 0%, #BDBDBD 100%)';
        this.levelTitle = 'Başlangıç Okuyucu';
        this.levelIcon = '📖';
        break;
      case 2:
        this.levelColor = '#4CAF50';
        this.levelGradient = 'linear-gradient(135deg, #4CAF50 0%, #66BB6A 100%)';
        this.levelTitle = 'Gelişen Okuyucu';
        this.levelIcon = '📗';
        break;
      case 3:
        this.levelColor = '#2196F3';
        this.levelGradient = 'linear-gradient(135deg, #2196F3 0%, #42A5F5 100%)';
        this.levelTitle = 'İleri Okuyucu';
        this.levelIcon = '📘';
        break;
      case 4:
        this.levelColor = '#FFD700';
        this.levelGradient = 'linear-gradient(135deg, #FFD700 0%, #FFC107 100%)';
        this.levelTitle = 'Master Okuyucu';
        this.levelIcon = '📕';
        break;
    }
  }
}
