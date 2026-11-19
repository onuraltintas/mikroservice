import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { isInternalLink } from '../core/text';

/** A link written by the site's editors: inside maxkampus.com through the router, elsewhere as a normal link. */
@Component({
  selector: 'mk-site-link',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (internal()) {
      <a [routerLink]="path()" [fragment]="fragment()" [class]="cls()"><ng-content /></a>
    } @else {
      <a [href]="href()" [class]="cls()" rel="noopener"><ng-content /></a>
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
