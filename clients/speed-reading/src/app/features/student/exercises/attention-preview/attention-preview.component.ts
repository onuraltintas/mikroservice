import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AttentionGame, ATTENTION_PRESETS, AttentionShape } from './attention-game';

@Component({
  selector: 'app-attention-preview',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './attention-preview.component.html',
  styleUrl: './attention-preview.component.scss'
})
export class AttentionPreviewComponent implements OnInit, OnDestroy {
  readonly presets = ATTENTION_PRESETS;
  readonly shapeNames: Record<AttentionShape, string> = {
    star: 'Yıldız', circle: 'Daire', triangle: 'Üçgen', square: 'Kare', diamond: 'Baklava', cross: 'Artı'
  };
  selectedPresetIndex = 0;
  game = new AttentionGame(this.presets[0]);
  now = performance.now();
  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.timer = setInterval(() => { this.now = performance.now(); this.game.tick(this.now); }, 100);
  }
  ngOnDestroy(): void { clearInterval(this.timer); }

  selectPreset(index: number): void {
    if (this.game.status !== 'intro' || !Number.isInteger(index) || !this.presets[index]) return;
    this.selectedPresetIndex = index;
    this.game = new AttentionGame(this.presets[index]);
  }
  startTutorial(): void { this.now = performance.now(); this.game.startTutorial(this.now); }
  startSession(): void {
    if (this.game.status !== 'ready') return;
    this.now = performance.now(); this.game.startSession(this.now);
  }
  choose(index: number): void { this.now = performance.now(); this.game.choose(index, this.now); }
  togglePause(): void {
    this.now = performance.now();
    if (this.game.status === 'paused') this.game.resume(this.now);
    else this.game.pause(this.now);
  }
  reset(): void { this.game = new AttentionGame(this.presets[this.selectedPresetIndex]); }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (document.hidden) this.game.pause(performance.now());
  }
}
