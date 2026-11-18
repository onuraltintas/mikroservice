import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-no-subscription',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="ns-wrap">
      <div class="ns-card">
        <div class="ns-icon">
          <span class="material-icons">workspace_premium</span>
        </div>
        <h1 class="ns-title">Abonelik gerekli</h1>
        <p class="ns-desc">
          Hızlı Okuma'yı kullanmak için etkin bir aboneliğin olmalı.
          Planlara göz atıp hemen başlayabilirsin.
        </p>
        <div class="ns-actions">
          <a routerLink="/student/subscription" class="ns-btn ns-btn--primary">
            <span class="material-icons">shopping_cart</span>
            Planları gör
          </a>
          <button class="ns-btn ns-btn--outline" (click)="logout()">
            <span class="material-icons">logout</span>
            Çıkış yap
          </button>
        </div>
        <p class="ns-footer">
          Aboneliğin olduğunu düşünüyorsan
          <a routerLink="/iletisim">bize yaz</a>.
        </p>
      </div>
    </div>
  `,
  styles: [`

    .ns-wrap {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--ui-surface);
      padding: 24px;
      font-family: 'Inter', system-ui, sans-serif;
    }

    .ns-card {
      background: white;
      border: 1px solid var(--ui-brand-soft);
      border-radius: 20px;
      padding: 48px 40px;
      max-width: 440px;
      width: 100%;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      box-shadow: 0 4px 24px color-mix(in srgb, var(--ui-text) 6%, transparent);
    }

    .ns-icon {
      width: 72px;
      height: 72px;
      border-radius: 20px;
      background: var(--ui-brand-soft);
      display: flex;
      align-items: center;
      justify-content: center;
      .material-icons { font-size: 36px; color: var(--ui-brand-text); }
    }

    .ns-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: var(--ui-text);
      margin: 0;
    }

    .ns-desc {
      font-size: 0.9375rem;
      color: var(--ui-text);
      line-height: 1.6;
      margin: 0;
    }

    .ns-actions {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      justify-content: center;
      margin-top: 8px;
    }

    .ns-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 10px 20px;
      border-radius: 10px;
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
      text-decoration: none;
      font-family: inherit;
      transition: all 0.15s;

      &--primary {
        background: var(--ui-brand);
        color: white;
        &:hover { background: var(--ui-brand); }
      }

      &--outline {
        background: transparent;
        border: 1.5px solid var(--ui-brand-soft);
        color: var(--ui-text);
        &:hover { border-color: var(--ui-brand); color: var(--ui-brand-text); }
      }

      .material-icons { font-size: 18px; }
    }

    .ns-footer {
      font-size: 0.8125rem;
      color: var(--ui-brand-text);
      margin: 0;
      a { color: var(--ui-brand-text); &:hover { text-decoration: underline; } }
    }
  `],
})
export class NoSubscriptionComponent {
  private auth = inject(AuthService);
  logout(): void { this.auth.logout(); }
}
