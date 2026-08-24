import { Component, Input, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MetronomeService } from '../../../core/services/metronome.service';
import { Subscription } from 'rxjs';

/**
 * Visual Metronome Component
 * Provides visual feedback for metronome beats during speed reading exercises
 * Displays a pulsating indicator synchronized with BPM
 */
@Component({
  selector: 'app-visual-metronome',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './visual-metronome.component.html',
  styleUrls: ['./visual-metronome.component.scss']
})
export class VisualMetronomeComponent implements OnInit, OnDestroy {
  /**
   * Enable/disable the metronome display
   */
  @Input() enabled: boolean = true;

  /**
   * Current BPM (Beats Per Minute)
   */
  @Input() bpm: number = 60;

  /**
   * Default color when not beating
   */
  @Input() color: string = '#3f51b5';

  /**
   * Color during beat (pulse)
   */
  @Input() beatColor: string = '#ff4081';

  /**
   * Current beat state
   */
  isBeat = false;

  private beatSubscription?: Subscription;
  private metronomeService = inject(MetronomeService);

  ngOnInit() {
    if (this.enabled) {
      // Subscribe to beat events from MetronomeService
      this.beatSubscription = this.metronomeService.beat$.subscribe(beat => {
        this.isBeat = beat;
      });
    }
  }

  ngOnDestroy() {
    // Clean up subscription
    this.beatSubscription?.unsubscribe();
  }
}
