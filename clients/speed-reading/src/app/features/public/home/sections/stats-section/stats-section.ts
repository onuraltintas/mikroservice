import { Component, OnInit, AfterViewInit, ElementRef, ViewChildren, QueryList, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

interface Stat {
  icon: string;
  value: string;
  label: string;
  evidence?: string;
  target: number;
  format: 'number' | 'number_plus' | 'millions' | 'percent';
}

interface PublishedEvidenceMetric {
  title: string;
  value: string;
  description?: string;
  source: string;
  period: string;
  sampleSize: number;
  icon?: string;
  verified?: boolean;
}

@Component({
  selector: 'app-stats-section',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './stats-section.html',
  styleUrl: './stats-section.scss'
})
export class StatsSectionComponent implements OnInit, AfterViewInit {
  private cmsService = inject(PublicCmsService);

  @ViewChildren('statValue') statValues!: QueryList<ElementRef>;

  // These are product principles, not unverified population outcome claims.
  stats: Stat[] = [
    { icon: 'speed', value: 'Ölç', label: 'Okuma hızını takip edin', target: 0, format: 'number' },
    { icon: 'quiz', value: 'Anla', label: 'Kavramayı birlikte değerlendirin', target: 0, format: 'number' },
    { icon: 'route', value: 'Uyarla', label: 'Uygun içerikle ilerleyin', target: 0, format: 'number' },
    { icon: 'insights', value: 'İzle', label: 'Gelişimi görün', target: 0, format: 'number' }
  ];

  private observer!: IntersectionObserver;
  private hasAnimated = false;
  private isContentLoaded = false;
  private isInView = false;

  ngOnInit() {
    this.loadContent();
  }

  ngAfterViewInit() {
    this.setupIntersectionObserver();
  }

  private loadContent() {
    this.cmsService.getLandingContent('EvidenceMetrics').subscribe({
      next: (content) => {
        const publishedMetrics = Object.values(content.blocks)
          .map(value => this.toPublishedEvidenceMetric(value))
          .filter((metric): metric is PublishedEvidenceMetric => metric !== null);
        if (publishedMetrics.length) {
          this.stats = publishedMetrics.map(metric => ({
            icon: metric.icon || 'insights',
            value: metric.value,
            label: metric.title,
            evidence: [metric.description, metric.source, metric.period, `n=${metric.sampleSize}`].filter(Boolean).join(' · '),
            target: 0,
            format: 'number'
          }));
        }
        this.isContentLoaded = true;
        // If already in view, start animation now
        if (this.isInView && !this.hasAnimated) {
          this.hasAnimated = true;
          this.animateCounters();
        }

        // Fallback: If still not animated after a short delay, animate anyway
        setTimeout(() => {
          if (!this.hasAnimated) {
            this.hasAnimated = true;
            this.animateCounters();
          }
        }, 500);
      },
      error: (err) => {
        console.warn('Failed to load landing content, using defaults', err);
        this.isContentLoaded = true;
        // Still animate with defaults if in view
        if (this.isInView && !this.hasAnimated) {
          this.hasAnimated = true;
          this.animateCounters();
        }

        // Fallback: If still not animated after a short delay, animate anyway
        setTimeout(() => {
          if (!this.hasAnimated) {
            this.hasAnimated = true;
            this.animateCounters();
          }
        }, 500);
      }
    });
  }

  setupIntersectionObserver() {
    this.observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting && !this.hasAnimated) {
          this.isInView = true;
          // Only animate if content is loaded
          if (this.isContentLoaded) {
            this.hasAnimated = true;
            this.animateCounters();
          }
        }
      });
    }, { threshold: 0.1 }); // Lower threshold to trigger earlier

    this.statValues.forEach(el => {
      this.observer.observe(el.nativeElement);
    });
  }

  animateCounters() {
    // Static product principles do not need animated numerical counters.
  }

  private toPublishedEvidenceMetric(rawValue: string): PublishedEvidenceMetric | null {
    try {
      const value = JSON.parse(rawValue) as Partial<PublishedEvidenceMetric>;
      const sampleSize = value.sampleSize;
      if (typeof value.title !== 'string' || !value.title.trim()
        || typeof value.value !== 'string' || !value.value.trim()
        || typeof value.source !== 'string' || !value.source.trim()
        || typeof value.period !== 'string' || !value.period.trim()
        || typeof sampleSize !== 'number' || !Number.isInteger(sampleSize) || sampleSize < 1) return null;
      if (/\d/.test(value.value) && value.verified !== true) return null;
      return value as PublishedEvidenceMetric;
    } catch {
      return null;
    }
  }

  animateValue(index: number, start: number, end: number, duration: number) {
    const range = end - start;
    const increment = range / (duration / 16);
    let current = start;

    const timer = setInterval(() => {
      current += increment;
      if (current >= end) {
        current = end;
        clearInterval(timer);
      }

      // Format based on stat format type
      const format = this.stats[index].format || 'number';
      switch (format) {
        case 'millions':
          this.stats[index].value = (Math.floor(current / 1000) / 1000).toFixed(1) + 'M+';
          break;
        case 'number_plus':
          this.stats[index].value = Math.floor(current).toLocaleString() + '+';
          break;
        case 'percent':
          this.stats[index].value = '%' + Math.floor(current);
          break;
        default:
          this.stats[index].value = Math.floor(current).toLocaleString();
      }
    }, 16);
  }

  ngOnDestroy() {
    if (this.observer) {
      this.observer.disconnect();
    }
  }
}
