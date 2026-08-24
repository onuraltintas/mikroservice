import { Component, signal, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SettingsService } from './core/services/settings.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  protected readonly title = signal('speed-reading-frontend');
  private settingsService = inject(SettingsService);

  ngOnInit(): void {
    // Settings service automatically loads and applies settings on initialization
    // No additional code needed here as the service constructor handles it
  }
}
