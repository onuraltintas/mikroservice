import { Injectable } from '@angular/core';
import { ChartOptions } from 'chart.js';

@Injectable({
    providedIn: 'root'
})
export class ChartConfigService {
    /**
     * Lively design (21.11.2026): chart colours come from the brand tokens (--sp-*), read when a chart is drawn, so charts
     * follow the brand and the light or dark theme. The hex values are only fallbacks for tests without the stylesheet.
     */
    get colors() {
        return {
            primary: this.token('--sp-primary', '#0B7A64'),
            accent:  this.token('--sp-focus-line', '#E8730C'),
            warn:    this.token('--sp-danger', '#C2410C'),
            success: this.token('--sp-success', '#15803D'),
            info:    this.token('--sp-tone-sea', '#0E6A7A'),
            warning: this.token('--sp-warning', '#B45309'),
            purple:  this.token('--sp-tone-peach', '#9A4508'),
            teal:    this.token('--sp-tone-mint', '#08624F'),
            orange:  this.token('--sp-focus-line', '#E8730C'),
            sand:    this.token('--sp-tone-sand', '#8A5A00'),
            grey:    this.token('--sp-text-3', '#4A5F5B'),
            neutral: this.token('--sp-border', '#D4E5DF'),
            surface: this.token('--sp-surface', '#FFFFFF'),
            text:    this.token('--sp-text-1', '#10201D'),
            muted:   this.token('--sp-text-3', '#4A5F5B')
        };
    }

    /** Series order: pine, orange marker, sea, sand, peach, mint, success. */
    get chartColors(): string[] {
        const c = this.colors;
        return [c.primary, c.accent, c.info, c.sand, c.purple, c.teal, c.success];
    }

    get fontFamily(): string {
        const font = this.raw('--sp-font-sans');
        return font || 'Lexend, system-ui, sans-serif';
    }

    /** Bars grow and lines draw once; nothing moves when the person asked for reduced motion. */
    get animation(): false | { duration: number; easing: 'easeOutQuart' } {
        const reduce = typeof window !== 'undefined' && typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        return reduce ? false : { duration: 900, easing: 'easeOutQuart' };
    }

    /** "#0b7a64" + alpha 0.12 → rgba(...). Works for #rgb, #rrggbb and rgb() token values. */
    withAlpha(color: string, alpha: number): string {
        return this.hexToRgba(this.toHex(color), alpha);
    }

    getDefaultOptions(): ChartOptions {
        const c = this.colors;
        const family = this.fontFamily;
        return {
            responsive: true,
            maintainAspectRatio: true,
            animation: this.animation,
            color: c.muted,
            plugins: {
                legend: {
                    display: true,
                    position: 'bottom',
                    labels: {
                        font: { family, size: 12 },
                        color: c.muted,
                        padding: 15,
                        usePointStyle: true
                    }
                },
                tooltip: {
                    enabled: true,
                    backgroundColor: c.text,
                    titleColor: c.surface,
                    bodyColor: c.surface,
                    titleFont: { family, size: 13, weight: 'bold' },
                    bodyFont: { family, size: 12 },
                    padding: 10,
                    cornerRadius: 10,
                    displayColors: false
                }
            }
        };
    }

    /** Shared axis look: no vertical grid, soft horizontal grid, muted ticks, value axis from zero. */
    axes(beginAtZero = true): Record<string, unknown> {
        const c = this.colors;
        const ticks = { font: { family: this.fontFamily, size: 11 }, color: c.muted };
        return {
            x: { grid: { display: false }, border: { display: false }, ticks },
            y: { beginAtZero, grid: { color: this.withAlpha(c.primary, 0.08) }, border: { display: false }, ticks }
        };
    }

    private raw(name: string): string {
        if (typeof document === 'undefined' || typeof getComputedStyle !== 'function') return '';
        const scope = document.querySelector('.sp-shell') ?? document.documentElement;
        return getComputedStyle(scope).getPropertyValue(name).trim();
    }

    private token(name: string, fallback: string): string {
        const value = this.toHex(this.raw(name));
        return value || fallback;
    }

    private toHex(value: string): string {
        if (!value) return '';
        const short = /^#([0-9a-f])([0-9a-f])([0-9a-f])$/i.exec(value);
        if (short) return `#${short[1]}${short[1]}${short[2]}${short[2]}${short[3]}${short[3]}`;
        if (/^#[0-9a-f]{6}$/i.test(value)) return value;
        if (/^#[0-9a-f]{8}$/i.test(value)) return value.slice(0, 7);
        const rgb = /^rgba?\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)/i.exec(value);
        if (rgb) return '#' + [rgb[1], rgb[2], rgb[3]].map(part => Number(part).toString(16).padStart(2, '0')).join('');
        return '';
    }

    getLineChartOptions(options?: Partial<ChartOptions>): ChartOptions<'line'> {
        return {
            ...this.getDefaultOptions(),
            scales: this.axes() as ChartOptions['scales'],
            ...options
        } as ChartOptions<'line'>;
    }

    getBarChartOptions(options?: Partial<ChartOptions>): ChartOptions<'bar'> {
        return {
            ...this.getDefaultOptions(),
            scales: this.axes() as ChartOptions['scales'],
            ...options
        } as ChartOptions<'bar'>;
    }

    getPieChartOptions(options?: Partial<ChartOptions>): ChartOptions<'pie'> {
        return {
            ...this.getDefaultOptions(),
            ...options
        } as ChartOptions<'pie'>;
    }

    getDoughnutChartOptions(options?: Partial<ChartOptions>): ChartOptions<'doughnut'> {
        return {
            ...this.getDefaultOptions(),
            ...options
        } as ChartOptions<'doughnut'>;
    }

    getRadarChartOptions(options?: Partial<ChartOptions>): ChartOptions<'radar'> {
        return {
            ...this.getDefaultOptions(),
            scales: {
                r: {
                    beginAtZero: true,
                    ticks: {
                        font: {
                            family: this.fontFamily,
                            size: 11
                        }
                    },
                    pointLabels: {
                        font: {
                            family: this.fontFamily,
                            size: 12
                        }
                    }
                }
            },
            ...options
        } as ChartOptions<'radar'>;
    }

    /**
     * Generate gradient background for charts
     */
    createGradient(ctx: CanvasRenderingContext2D, color: string, alpha: number = 0.2): CanvasGradient {
        const gradient = ctx.createLinearGradient(0, 0, 0, 400);
        gradient.addColorStop(0, this.hexToRgba(color, alpha));
        gradient.addColorStop(1, this.hexToRgba(color, 0));
        return gradient;
    }

    /**
     * Convert hex color to rgba
     */
    private hexToRgba(hex: string, alpha: number): string {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }
}
