import { Component, Input, Output, EventEmitter, HostListener, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { trigger, state, style, transition, animate } from '@angular/animations';

@Component({
  selector: 'app-exercise-container',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    MatTooltipModule
  ],
  templateUrl: './exercise-container.html',
  styleUrl: './exercise-container.scss',
  animations: [
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('300ms ease-in', style({ opacity: 1 }))
      ])
    ]),
    trigger('fadeOut', [
      transition(':leave', [
        animate('300ms ease-out', style({ opacity: 0 }))
      ])
    ]),
    trigger('autoHide', [
      state('visible', style({ opacity: 0.8 })),
      state('hidden', style({ opacity: 0 })),
      transition('visible <=> hidden', animate('200ms ease'))
    ])
  ]
})
export class ExerciseContainerComponent implements OnInit, OnDestroy {
  // Input properties
  @Input() exerciseName: string = 'Egzersiz';
  @Input() exerciseDescription?: string;
  @Input() showStats: boolean = true;
  @Input() showControls: boolean = true;
  @Input() hasSettings: boolean = false;
  @Input() currentStep: number = 0;
  @Input() totalSteps?: number;
  @Input() timeElapsed: number = 0;
  @Input() timeRemaining?: number;
  @Input() isPaused: boolean = false;

  // Output events
  @Output() exit = new EventEmitter<void>();
  @Output() pauseToggle = new EventEmitter<void>();
  @Output() settingsClick = new EventEmitter<void>();
  @Output() stop = new EventEmitter<void>();

  // Internal state
  controlsVisible: string = 'visible';
  showShortcutHint: boolean = true;
  isMobile: boolean = false;
  isFullscreen: boolean = false;
  private controlsTimer: any;
  private shortcutHintTimer: any;

  ngOnInit(): void {
    // Detect mobile
    this.isMobile = window.innerWidth < 768;

    // Hide shortcut hint after 5 seconds
    this.shortcutHintTimer = setTimeout(() => {
      this.showShortcutHint = false;
    }, 5000);

    // Start auto-hide timer
    this.resetControlsTimer();

    // Request fullscreen (optional)
    if (!this.isMobile) {
      this.requestFullscreen();
    }

    // Listen to fullscreen changes
    document.addEventListener('fullscreenchange', () => this.updateFullscreenState());
  }

  ngOnDestroy(): void {
    this.clearTimers();
    this.exitFullscreen();
    document.removeEventListener('fullscreenchange', () => this.updateFullscreenState());
  }

  // Keyboard shortcuts
  @HostListener('window:keydown', ['$event'])
  handleKeyboard(event: KeyboardEvent): void {
    // Check if user is typing in an input or textarea
    const target = event.target as HTMLElement;
    const isInputField = target.tagName === 'INPUT' || 
                        target.tagName === 'TEXTAREA' || 
                        target.isContentEditable;

    switch(event.key) {
      case 'Escape':
        event.preventDefault();
        this.onExit();
        break;
      case ' ': // Space
        // Allow space in input fields
        if (!isInputField) {
          event.preventDefault();
          this.onPauseToggle();
        }
        break;
      case 'F11':
        event.preventDefault();
        this.toggleFullscreen();
        break;
    }
  }

  // Mouse move detection (auto-hide controls)
  @HostListener('mousemove')
  @HostListener('touchstart')
  onMouseMove(): void {
    this.controlsVisible = 'visible';
    this.resetControlsTimer();
  }

  @HostListener('window:resize')
  onResize(): void {
    this.isMobile = window.innerWidth < 768;
  }

  onExit(): void {
    this.exit.emit();
  }

  onPauseToggle(): void {
    this.pauseToggle.emit();
  }

  onSettingsClick(): void {
    this.settingsClick.emit();
  }

  onStop(): void {
    this.stop.emit();
  }

  hideShortcutHint(): void {
    this.showShortcutHint = false;
    if (this.shortcutHintTimer) {
      clearTimeout(this.shortcutHintTimer);
    }
  }

  get progressPercentage(): number {
    if (!this.totalSteps || this.totalSteps === 0) return 0;
    return Math.round((this.currentStep / this.totalSteps) * 100);
  }

  formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  }

  private resetControlsTimer(): void {
    this.clearControlsTimer();

    if (!this.isPaused) {
      this.controlsTimer = setTimeout(() => {
        this.controlsVisible = 'hidden';
      }, 3000); // 3 saniye sonra gizle
    }
  }

  private clearControlsTimer(): void {
    if (this.controlsTimer) {
      clearTimeout(this.controlsTimer);
      this.controlsTimer = null;
    }
  }

  private clearTimers(): void {
    this.clearControlsTimer();
    if (this.shortcutHintTimer) {
      clearTimeout(this.shortcutHintTimer);
      this.shortcutHintTimer = null;
    }
  }

  // Fullscreen methods
  private requestFullscreen(): void {
    const elem = document.documentElement;
    if (elem.requestFullscreen) {
      elem.requestFullscreen().catch(() => {
      });
    }
  }

  private exitFullscreen(): void {
    if (document.fullscreenElement) {
      document.exitFullscreen().catch(() => {
      });
    }
  }

  toggleFullscreen(): void {
    if (!document.fullscreenElement) {
      this.requestFullscreen();
    } else {
      this.exitFullscreen();
    }
  }

  private updateFullscreenState(): void {
    this.isFullscreen = !!document.fullscreenElement;
  }
}
