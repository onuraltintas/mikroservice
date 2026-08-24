import { Component, OnInit, AfterViewInit, ElementRef, ViewChildren, QueryList, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';

interface Stat {
  icon: string;
  value: string;
  label: string;
  target: number;
  format: 'number' | 'number_plus' | 'millions' | 'percent';
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

  // Default stats (fallback)
  stats: Stat[] = [
    { icon: 'people', value: '0', label: 'Aktif Kullanıcı', target: 50000, format: 'number_plus' },
    { icon: 'menu_book', value: '0', label: 'Okunan Kitap', target: 1000000, format: 'millions' },
    { icon: 'trending_up', value: '0', label: 'Hız Artışı', target: 300, format: 'percent' },
    { icon: 'star', value: '0', label: 'Başarı Oranı', target: 95, format: 'percent' }
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
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        if (content.blocks['stats_data']) {
          try {
            const parsedStats = JSON.parse(content.blocks['stats_data']);
            if (Array.isArray(parsedStats) && parsedStats.length > 0) {
              // Merge CMS stats with defaults (keep icon and value='0' for animation)
              this.stats = parsedStats.map((stat: any, index: number) => ({
                icon: stat.icon || this.stats[index]?.icon || 'star',
                value: '0',
                label: stat.label || this.stats[index]?.label || '',
                target: stat.target || this.stats[index]?.target || 0,
                format: stat.format || this.stats[index]?.format || 'number'
              }));
            }
          } catch (e) {
            console.warn('Failed to parse stats_data, using defaults', e);
          }
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
    this.stats.forEach((stat, index) => {
      this.animateValue(index, 0, stat.target, 2000);
    });
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
