import { Directive, ElementRef, Input, OnChanges, OnDestroy, SimpleChanges, inject } from '@angular/core';

/**
 * CountUp Direktifi — Bir sayıyı animasyonlu biçimde 0'dan hedefe kadar sayar.
 *
 * Kullanım:
 *   <span [appCountUp]="currentStreak()" [cuDuration]="1200"></span>
 */
@Directive({
  selector: '[appCountUp]',
  standalone: true,
  host: { class: 'sp-count-up' }
})
export class CountUpDirective implements OnChanges, OnDestroy {
  @Input('appCountUp') target: number = 0;
  @Input() cuDuration: number = 1000;   // ms
  @Input() cuDecimals: number = 0;      // ondalık basamak sayısı
  @Input() cuSuffix: string = '';       // ör. '%', 'KDK'

  private el = inject(ElementRef<HTMLElement>);
  private rafId: number | null = null;
  private startTime: number | null = null;
  private startValue = 0;
  private endValue = 0;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['target']) {
      this.endValue = this.target ?? 0;
      this.startAnimation();
    }
  }

  ngOnDestroy(): void {
    this.cancelAnimation();
  }

  private startAnimation(): void {
    this.cancelAnimation();
    this.startValue = 0;
    this.startTime = null;
    this.rafId = requestAnimationFrame(ts => this.tick(ts));
  }

  private tick(timestamp: number): void {
    if (this.startTime === null) this.startTime = timestamp;

    const elapsed = timestamp - this.startTime;
    const progress = Math.min(elapsed / this.cuDuration, 1);
    const eased = this.easeOutExpo(progress);
    const current = this.startValue + (this.endValue - this.startValue) * eased;

    this.el.nativeElement.textContent =
      current.toFixed(this.cuDecimals) + this.cuSuffix;

    if (progress < 1) {
      this.rafId = requestAnimationFrame(ts => this.tick(ts));
    } else {
      this.el.nativeElement.textContent =
        this.endValue.toFixed(this.cuDecimals) + this.cuSuffix;
    }
  }

  private cancelAnimation(): void {
    if (this.rafId !== null) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
  }

  // easeOutExpo: başta hızlı, sonda yavaşlar — sayaç hissi için ideal
  private easeOutExpo(t: number): number {
    return t === 1 ? 1 : 1 - Math.pow(2, -10 * t);
  }
}
