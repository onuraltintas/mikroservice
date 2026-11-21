import { Component, computed, input } from '@angular/core';
import { SITE_ICON_PATHS } from '../../../../../src/app/features/public/eduivme/eduivme-site-content';

/** The stroke icons the editors pick by name (the same set as eduivme.com). */
@Component({
  selector: 'mk-site-icon',
  standalone: true,
  template: `<span class="icon"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="path()"></path></svg></span>`,
  styles: `.icon { display: inline-flex; align-items: center; justify-content: center; width: 44px; height: 44px; border-radius: 12px; background: var(--mk-icon-bg, var(--mk-soft)); color: var(--mk-icon-fg, var(--mk-mark)); }`
})
export class SiteIconComponent {
  readonly name = input<string | null>(null);
  readonly path = computed(() => SITE_ICON_PATHS[(this.name() ?? 'star') as keyof typeof SITE_ICON_PATHS] ?? SITE_ICON_PATHS.star);
}
