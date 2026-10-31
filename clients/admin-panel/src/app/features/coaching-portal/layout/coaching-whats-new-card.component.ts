import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CoachingWhatsNewService } from './coaching-whats-new.service';

/** T2 (31.10.2026): a short, one-time note about the new student menu. On a phone it shrinks to one line (T7). */
@Component({
  selector: 'app-coaching-whats-new-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (whatsNew.visible()) {
    <section class="whats-new" aria-labelledby="whats-new-title">
      <div>
        <p class="tag">Yeni</p>
        <h2 id="whats-new-title">Panelin yenilendi</h2>
        <p class="long">Her şey artık daha az menüde: <strong>Bugün</strong>, <strong>Planım</strong>, <strong>Ödevlerim</strong>, <strong>Gelişimim</strong> ve <strong>Mesajlar</strong>. Profil, ödemeler ve tema sağ üstteki profil simgende.</p>
        <p class="short">Menü beş bölüme indi. Profil, ödemeler ve tema sağ üstte.</p>
      </div>
      <button type="button" (click)="whatsNew.dismiss()">Anladım</button>
    </section>
  }`,
  styles: [`.whats-new{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:var(--coaching-space-4);margin-bottom:var(--coaching-space-6);padding:var(--coaching-space-5);border:1px solid var(--ui-border);border-radius:var(--coaching-radius-card);background:var(--ui-brand-soft);color:var(--ui-text)}
    .whats-new>div{flex:1 1 320px;min-width:0}
    .tag{display:inline-block;margin:0 0 var(--coaching-space-2);padding:2px 10px;border-radius:var(--coaching-radius-pill);background:var(--ui-accent-soft);color:var(--ui-accent-text);font-size:var(--coaching-text-sm);font-weight:800}
    h2{margin:0 0 var(--coaching-space-1);font-size:var(--coaching-text-lg);font-weight:800}
    p{margin:0;font-size:var(--coaching-text-body);color:var(--ui-text)}
    .short{display:none}
    /* Phone: one compact line so the first task of the day stays above the fold. */
    @media (max-width:640px){
      .whats-new{flex-wrap:nowrap;gap:var(--coaching-space-3);margin-bottom:var(--coaching-space-4);padding:var(--coaching-space-3) var(--coaching-space-4)}
      .whats-new>div{flex:1 1 auto}
      .tag,.long{display:none}.short{display:block;font-size:var(--coaching-text-sm)}
      h2{font-size:var(--coaching-text-body)}
      button{flex:none;padding:0 var(--coaching-space-4)}
    }
    button{min-height:44px;padding:0 var(--coaching-space-5);border:0;border-radius:var(--coaching-radius-control);background:var(--ui-brand);color:var(--ui-brand-contrast);font:inherit;font-weight:700;cursor:pointer}`]
})
export class CoachingWhatsNewCardComponent {
  readonly whatsNew = inject(CoachingWhatsNewService);
}
