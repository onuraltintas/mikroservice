import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { watchDecorativeIcons } from './app/core/a11y/decorative-icons';

bootstrapApplication(App, appConfig)
  .then(() => watchDecorativeIcons())
  .catch((err) => console.error(err));
