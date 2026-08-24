import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface MetronomeConfig {
  bpm: number;
  enabled: boolean;
  visualEnabled: boolean;
  audioEnabled: boolean;
  paceType: 'constant' | 'progressive' | 'interval';
}

/**
 * Metronome Service
 * Provides tempo control for speed reading exercises
 * Supports constant, progressive, and interval BPM modes
 */
@Injectable({
  providedIn: 'root'
})
export class MetronomeService {
  private audioContext?: AudioContext;
  private intervalId?: number;
  private currentBPM = 60;
  
  // Observable for beat events
  private beatSubject = new BehaviorSubject<boolean>(false);
  public beat$: Observable<boolean> = this.beatSubject.asObservable();

  constructor() {}

  /**
   * Start metronome with given BPM
   * @param bpm Beats per minute (tempo)
   */
  start(bpm: number): void {
    this.stop(); // Stop any existing metronome
    this.currentBPM = bpm;
    const intervalMs = 60000 / bpm; // Convert BPM to milliseconds
    
    this.intervalId = window.setInterval(() => {
      this.onBeat();
    }, intervalMs);
  }

  /**
   * Stop metronome
   */
  stop(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = undefined;
    }
  }

  /**
   * Handle beat event
   * Emits beat signal to subscribers
   */
  private onBeat(): void {
    this.beatSubject.next(true);
    
    // Reset beat indicator after short delay (visual feedback)
    setTimeout(() => {
      this.beatSubject.next(false);
    }, 100);
  }

  /**
   * Play audio beep sound
   * Creates a short sine wave tone
   */
  playBeep(): void {
    if (!this.audioContext) {
      this.audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
    }

    const oscillator = this.audioContext.createOscillator();
    const gainNode = this.audioContext.createGain();

    oscillator.connect(gainNode);
    gainNode.connect(this.audioContext.destination);

    // Configure sound
    oscillator.frequency.value = 800; // Hz - pitch of the beep
    oscillator.type = 'sine';

    // Volume envelope (fade out for smooth sound)
    gainNode.gain.setValueAtTime(0.3, this.audioContext.currentTime);
    gainNode.gain.exponentialRampToValueAtTime(0.01, this.audioContext.currentTime + 0.1);

    // Play beep
    oscillator.start(this.audioContext.currentTime);
    oscillator.stop(this.audioContext.currentTime + 0.1);
  }

  /**
   * Update BPM (for progressive mode)
   * @param newBPM New tempo value
   */
  updateBPM(newBPM: number): void {
    if (this.intervalId) {
      this.start(newBPM); // Restart with new BPM
    }
    this.currentBPM = newBPM;
  }

  /**
   * Get current BPM
   * @returns Current beats per minute
   */
  getCurrentBPM(): number {
    return this.currentBPM;
  }

  /**
   * Check if metronome is running
   * @returns True if metronome is active
   */
  isRunning(): boolean {
    return this.intervalId !== undefined;
  }

  /**
   * Cleanup resources
   * Should be called when component is destroyed
   */
  destroy(): void {
    this.stop();
    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = undefined;
    }
  }
}
