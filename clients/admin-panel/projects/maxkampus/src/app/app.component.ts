import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MaxkampusSession } from './core/session.service';

@Component({
  selector: 'mk-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`
})
export class AppComponent implements OnInit {
  private readonly session = inject(MaxkampusSession);
  /** A returning visitor's session comes back from the refresh cookie (silently; nothing is shown if there is none). */
  ngOnInit(): void { void this.session.refresh().catch(() => false); }
}
