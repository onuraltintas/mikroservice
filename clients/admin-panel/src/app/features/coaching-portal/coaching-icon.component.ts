import { Component, Input } from '@angular/core';

export type CoachingIcon = 'home' | 'plan' | 'book' | 'clock' | 'spark' | 'chart' | 'target' | 'calendar' | 'bell' | 'wallet' | 'card' | 'arrow' | 'check' | 'menu' | 'close' | 'logout' | 'sun' | 'moon' | 'chat';

@Component({
  selector: 'app-coaching-icon', standalone: true,
  template: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="paths[name]" /></svg>`,
  styles: [':host{display:inline-flex;flex-shrink:0;width:1.25em;height:1.25em}svg{width:100%;height:100%}']
})
export class CoachingIconComponent {
  @Input() name: CoachingIcon = 'home';
  readonly paths: Record<CoachingIcon, string> = {
    sun: 'M16 12a4 4 0 1 0-8 0 4 4 0 0 0 8 0M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5 19 19M5 19l1.5-1.5M17.5 6.5 19 5',
    moon: 'M21 13a9 9 0 0 1-10-10 9 9 0 1 0 10 10',
    chat: 'M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12',
    home: 'M3 10 12 3l9 7M5 9v11h5v-6h4v6h5V9',
    plan: 'M8 4H5v17h14V4h-3M9 2h6v4H9zM8 11h8M8 15h5',
    book: 'M12 5v16M12 5C8 2 4 3 2 4v16c4-2 7-1 10 1 3-2 6-3 10-1V4c-3-2-7-2-10 1',
    clock: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18M12 7v5l3 2',
    spark: 'm12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5L12 3',
    chart: 'M4 3v17h17M8 15l4-5 4 2 5-7',
    target: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10M12 11v2',
    calendar: 'M5 4h14a2 2 0 0 1 2 2v13H3V6a2 2 0 0 1 2-2M7 2v4M17 2v4M3 9h18M7 13h3M14 13h3M7 16h3',
    bell: 'M6 9a6 6 0 0 1 12 0c0 7 3 7 3 8H3c0-1 3-1 3-8M10 21h4',
    wallet: 'M4 5h15V3H6a3 3 0 0 0-3 3v13h18V7H4M21 11h-6v4h6',
    card: 'M3 5h18v14H3zM3 9h18M7 15h3',
    arrow: 'M5 12h14m-5-5 5 5-5 5', check: 'm5 12 4 4L19 6',
    menu: 'M4 6h16M4 12h16M4 18h16', close: 'm6 6 12 12M6 18 18 6',
    logout: 'M9 4H4v16h5M10 12h11m-4-4 4 4-4 4'
  };
}
