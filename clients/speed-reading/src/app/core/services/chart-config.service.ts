import { Injectable } from '@angular/core';
import { ChartConfiguration, ChartOptions } from 'chart.js';

@Injectable({
    providedIn: 'root'
})
export class ChartConfigService {
    // Color palette — sp-* design system ile eşleştirildi
    readonly colors = {
        primary: '#6366f1',   // --sp-primary (indigo)
        accent:  '#f97316',   // --sp-streak (turuncu)
        warn:    '#f43f5e',   // --sp-danger
        success: '#10b981',   // --sp-success (zümrüt)
        info:    '#818cf8',   // --sp-primary-light
        warning: '#f59e0b',   // --sp-warning (amber)
        purple:  '#8b5cf6',   // mor tonu
        teal:    '#06b6d4',   // --sp-diamond (camgöbeği)
        orange:  '#f97316',   // turuncu
        grey:    '#9ca3af'    // nötr gri
    };

    readonly chartColors = [
        this.colors.primary,
        this.colors.accent,
        this.colors.success,
        this.colors.warning,
        this.colors.purple,
        this.colors.teal,
        this.colors.orange
    ];

    getDefaultOptions(): ChartOptions {
        return {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'bottom',
                    labels: {
                        font: {
                            family: 'Inter, system-ui, sans-serif',
                            size: 12
                        },
                        padding: 15,
                        usePointStyle: true
                    }
                },
                tooltip: {
                    enabled: true,
                    backgroundColor: 'rgba(15, 15, 30, 0.92)',
                    titleFont: {
                        family: 'Inter, system-ui, sans-serif',
                        size: 13,
                        weight: 'bold'
                    },
                    bodyFont: {
                        family: 'Inter, system-ui, sans-serif',
                        size: 12
                    },
                    padding: 10,
                    cornerRadius: 8,
                    displayColors: true,
                    borderColor: 'rgba(99, 102, 241, 0.3)',
                    borderWidth: 1
                }
            }
        };
    }

    getLineChartOptions(options?: Partial<ChartOptions>): ChartOptions<'line'> {
        return {
            ...this.getDefaultOptions(),
            scales: {
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        font: {
                            family: 'Inter, system-ui, sans-serif',
                            size: 11
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(99, 102, 241, 0.08)'
                    },
                    ticks: {
                        font: {
                            family: 'Inter, system-ui, sans-serif',
                            size: 11
                        }
                    }
                }
            },
            ...options
        } as ChartOptions<'line'>;
    }

    getBarChartOptions(options?: Partial<ChartOptions>): ChartOptions<'bar'> {
        return {
            ...this.getDefaultOptions(),
            scales: {
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        font: {
                            family: 'Inter, system-ui, sans-serif',
                            size: 11
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(99, 102, 241, 0.08)'
                    },
                    ticks: {
                        font: {
                            family: 'Inter, system-ui, sans-serif',
                            size: 11
                        }
                    }
                }
            },
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
                            family: 'Inter, system-ui, sans-serif',
                            size: 11
                        }
                    },
                    pointLabels: {
                        font: {
                            family: 'Inter, system-ui, sans-serif',
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
