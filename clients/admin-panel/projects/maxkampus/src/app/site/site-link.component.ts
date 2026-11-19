import { Component, computed, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { isInternalLink } from '../core/text';

/** A link written by the site's editors: inside maxkampus.com through the router, elsewhere as a normal link. */
@Component({
  selector: 'mk-site-link',
  standalone: true,
  imports: [RouterLink, NgTemplateOutlet],
  // Projected content goes to one place only, so both links show it through a template.
  template: `
    <ng-template #label><ng-content /></ng-template>
    @if (internal()) {
      <a [routerLink]="path()" [fragment]="fragment()" [class]="cls()"><ng-container [ngTemplateOutlet]="label" /></a>
    } @else {
      <a [href]="href()" [class]="cls()" rel="noopener"><ng-container [ngTemplateOutlet]="label" /></a>
    }
  `,
  styles: `:host { display: contents; }`
})
export class SiteLinkComponent {
  readonly href = input.required<string>();
  readonly cls = input('');
  readonly internal = computed(() => isInternalLink(this.href()));
  readonly path = computed(() => this.href().split('#')[0] || '/');
  readonly fragment = computed(() => this.href().split('#')[1] || undefined);
}
