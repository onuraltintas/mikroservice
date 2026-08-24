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
        <h1 class="ns-title">Abonelik Gerekli</h1>
        <p class="ns-desc">
          Hızlı Okuma platformuna erişmek için aktif bir aboneliğe ihtiyacınız var.
          Abonelik planlarını inceleyerek hemen başlayabilirsiniz.
        </p>
        <div class="ns-actions">
          <a routerLink="/odeme" class="ns-btn ns-btn--primary">
            <span class="material-icons">shopping_cart</span>
            Abonelik Planlarını Gör
          </a>
          <button class="ns-btn ns-btn--outline" (click)="logout()">
            <span class="material-icons">logout</span>
            Çıkış Yap
          </button>
        </div>
        <p class="ns-footer">
          Aboneliğiniz olduğunu düşünüyorsanız
          <a routerLink="/iletisim">bizimle iletişime geçin</a>.
        </p>
      </div>
    </div>
  `,
  styles: [`
    @import url('https://fonts.googleapis.com/icon?family=Material+Icons');

    .ns-wrap {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #f8fafc;
      padding: 24px;
      font-family: 'Inter', system-ui, sans-serif;
    }

    .ns-card {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 20px;
      padding: 48px 40px;
      max-width: 440px;
      width: 100%;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      box-shadow: 0 4px 24px rgba(0,0,0,0.06);
    }

    .ns-icon {
      width: 72px;
      height: 72px;
      border-radius: 20px;
      background: #eef2ff;
      display: flex;
      align-items: center;
      justify-content: center;
      .material-icons { font-size: 36px; color: #6366f1; }
    }

    .ns-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: #0f172a;
      margin: 0;
    }

    .ns-desc {
      font-size: 0.9375rem;
      color: #475569;
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
        background: #6366f1;
        color: white;
        &:hover { background: #4f46e5; }
      }

      &--outline {
        background: transparent;
        border: 1.5px solid #e2e8f0;
        color: #475569;
        &:hover { border-color: #6366f1; color: #6366f1; }
      }

      .material-icons { font-size: 18px; }
    }

    .ns-footer {
      font-size: 0.8125rem;
      color: #94a3b8;
      margin: 0;
      a { color: #6366f1; &:hover { text-decoration: underline; } }
    }
  `],
})
export class NoSubscriptionComponent {
  private auth = inject(AuthService);
  logout(): void { this.auth.logout(); }
}
