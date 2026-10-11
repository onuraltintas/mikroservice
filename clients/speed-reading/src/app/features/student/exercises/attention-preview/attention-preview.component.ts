import { ChangeDetectorRef, Component, ElementRef, HostListener, inject, OnDestroy, OnInit } from '@angular/core';
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
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private focusPosition = 0;
  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.timer = setInterval(() => {
      const previousStatus = this.game.status;
      this.now = performance.now(); this.game.tick(this.now);
      // Restore a usable control when feedback or practice removes/disables the focused button.
      if (previousStatus !== this.game.status && ['playing', 'ready', 'complete'].includes(this.game.status)) {
        this.focusControl();
      }
    }, 100);
  }
  ngOnDestroy(): void { clearInterval(this.timer); }

  selectPreset(index: number): void {
    if (this.game.status !== 'intro' || !Number.isInteger(index) || !this.presets[index]) return;
    this.selectedPresetIndex = index;
    this.game = new AttentionGame(this.presets[index]);
  }
  startTutorial(): void {
    this.now = performance.now(); this.game.startTutorial(this.now);
    this.focusPosition = 0; this.focusControl();
  }
  startSession(): void {
    if (this.game.status !== 'ready') return;
    this.now = performance.now(); this.game.startSession(this.now);
    this.focusPosition = 0; this.focusControl();
  }
  choose(index: number): void {
    this.now = performance.now();
    if (this.game.status === 'playing' && Number.isInteger(index) && index >= 0 && index < this.game.shapes.length) {
      this.focusPosition = index;
    }
    this.game.choose(index, this.now);
  }
  togglePause(): void {
    this.now = performance.now();
    if (this.game.status === 'paused') { this.game.resume(this.now); this.focusControl(); }
    else this.game.pause(this.now);
  }
  reset(): void { this.game = new AttentionGame(this.presets[this.selectedPresetIndex]); this.focusControl(); }

  private focusControl(): void {
    this.changeDetector.detectChanges();
    const root = this.element.nativeElement;
    const control = this.game.status === 'playing'
      ? root.querySelectorAll<HTMLButtonElement>('.shape-button')[this.focusPosition]
      : root.querySelector<HTMLButtonElement>('.primary');
    control?.focus({ preventScroll: true });
  }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (document.hidden) this.game.pause(performance.now());
  }
}
