import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { DarkModeService } from './core/services/dark-mode.service';
import { CookieConsentComponent } from './shared/components/cookie-consent.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatSnackBarModule, CookieConsentComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = signal('admin-panel');
  // Initialize the theme before any route (including auth) renders so the
  // OS preference and the user's saved choice use one document-level class.
  constructor() {
    inject(DarkModeService);
  }
}
