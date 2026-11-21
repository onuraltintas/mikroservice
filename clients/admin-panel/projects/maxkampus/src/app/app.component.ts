import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MaxkampusSession } from './core/session.service';
import { DarkModeService } from '../../../../src/app/core/services/dark-mode.service';

@Component({
  selector: 'mk-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`
})
export class AppComponent implements OnInit {
  private readonly session = inject(MaxkampusSession);
  /** Starts the theme: the device setting, or the visitor's own choice from the Görünüm menu. */
  private readonly theme = inject(DarkModeService);
  /** A returning visitor's session comes back from the refresh cookie (silently; nothing is shown if there is none). */
  ngOnInit(): void { void this.session.refresh().catch(() => false); }
}
