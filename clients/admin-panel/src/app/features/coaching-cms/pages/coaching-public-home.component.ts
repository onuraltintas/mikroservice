import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoachingCmsEntry, CoachingManagementPage, CoachingManagementService, CoachingSubscriptionPlan } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';
import { SiteAnalyticsService } from '../../../core/services/site-analytics.service';
import { SiteSeoService } from '../../../core/services/site-seo.service';

@Component({
  selector: 'app-coaching-public-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="home">
      <section class="hero" aria-labelledby="coaching-home-title">
        <div class="hero__copy">
          @if (hero()?.eyebrow) { <p class="hero__note">{{ hero()?.eyebrow }}</p> }
          <h1 id="coaching-home-title" class="hero__title">{{ hero()?.title || 'Sınava giden yol, haftalık bir plandan geçer.' }}</h1>
          <p class="hero__lead">{{ hero()?.summary || 'Boş saatlerini işaretle, planın hazırlansın. Her gün ne çalışacağını bil, deneme netlerini takip et, koçunla haftayı birlikte değerlendir.' }}</p>
          @if (hero()?.content) {
            <div class="cms-copy cms-copy--hero">
              @for (segment of contentSegments(hero()!.content); track $index) {
                @if (segment.type === 'heading') { <h2>{{ segment.value }}</h2> }
                @else if (segment.type === 'list') { <ul>@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
                @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
                @else { <p>{{ segment.value }}</p> }
              }
            </div>
          }
          <div class="hero__actions">
            <a [href]="hero()?.linkUrl || '/auth/register/student'" class="ds-btn ds-btn--primary ds-btn--large">{{ hero()?.linkLabel || 'Ücretsiz hesap aç' }}</a>
            <a [href]="hero()?.secondaryLinkUrl || '#nasil'" class="ds-btn ds-btn--secondary ds-btn--large">{{ hero()?.secondaryLinkLabel || 'Nasıl çalışır?' }}</a>
          </div>
          <p class="hero__note">LGS ve YKS öğrencileri için. Veli ve öğretmen aynı planı kendi ekranından izler.</p>
        </div>

        <figure class="week" role="img" aria-label="Haftalık plan örneği: beş günün dersleri, bu haftaki çalışma süresi 9 saat 40 dakika, son deneme 72 net, hedef 80 net">
          <div class="week__head"><strong>Elif'in haftası</strong><span>16–22 Kasım</span></div>
          <div class="week__days" aria-hidden="true">
            @for (day of sampleWeek; track day.name) {
              <div class="week__day" [class.week__day--today]="day.today">
                <span class="week__name">{{ day.name }}</span>
                @for (lesson of day.lessons; track $index) { <span class="week__lesson" [attr.data-subject]="lesson.subject">{{ lesson.label }}</span> }
              </div>
            }
          </div>
          <div class="week__foot" aria-hidden="true">
            <span>Bu hafta <strong>9 sa 40 dk</strong></span>
            <span>Son deneme <strong>72 net</strong></span>
            <span>Hedef <strong>80 net</strong></span>
          </div>
        </figure>
      </section>

      <section id="nasil" class="band" aria-labelledby="coaching-process-title">
        <div class="section-head">
          <h2 id="coaching-process-title">{{ processHeading()?.title || 'Nasıl çalışır?' }}</h2>
          <p>{{ processHeading()?.summary || 'Hedef belirleme, planlı çalışma ve düzenli değerlendirme; öğrenci, öğretmen ve veli aynı planın üzerinde.' }}</p>
        </div>
        <ol class="steps">
          @if (processBlocks().length) {
            @for (block of processBlocks(); track block.id; let index = $index) {
              <li class="step">
                <span class="step__no">{{ index + 1 }}. adım</span>
                <h3>{{ block.title }}</h3>
                @if (block.summary) { <p>{{ block.summary }}</p> }
                <div class="cms-copy">
                  @for (segment of contentSegments(block.content); track $index) {
                    @if (segment.type === 'heading') { <h4>{{ segment.value }}</h4> }
                    @else if (segment.type === 'list') { <ul>@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
                    @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
                    @else { <p>{{ segment.value }}</p> }
                  }
                </div>
              </li>
            }
          } @else {
            <li class="step"><span class="step__no">1. adım</span><h3>Hedefini ve boş saatlerini gir</h3><p>Hangi sınava hazırlandığını, okul dışında hangi saatlerde çalışabileceğini işaretle.</p></li>
            <li class="step"><span class="step__no">2. adım</span><h3>Planın günlere bölünsün</h3><p>Dersler ve konular haftaya yerleşir. Her gün açtığında yalnız o günün işlerini görürsün.</p></li>
            <li class="step"><span class="step__no">3. adım</span><h3>Koçunla haftayı değerlendir</h3><p>Denemelerin, ödevlerin ve çalıştığın süre koçunun ekranında. Bir sonraki haftayı birlikte ayarlarsınız.</p></li>
          }
        </ol>
      </section>

      <section class="section" aria-labelledby="coaching-areas-title">
        <div class="section-head section-head--row">
          <div>
            <h2 id="coaching-areas-title">{{ areaHeading()?.title || 'Herkes aynı planı kendi yerinden görür' }}</h2>
            @if (areaHeading()?.summary) { <p>{{ areaHeading()?.summary }}</p> }
          </div>
          @if (areaHeading()?.linkUrl) { <a [href]="areaHeading()?.linkUrl" class="ds-link">{{ areaHeading()?.linkLabel || 'Ayrıntılar' }}</a> }
        </div>
        @if (blocks().length) {
          <div class="cards" aria-label="Koçluk ana sayfa içerikleri">
            @for (block of blocks(); track block.id) {
              <article class="card">
                <h3>{{ block.title }}</h3>
                @if (block.summary) { <p class="card__lead">{{ block.summary }}</p> }
                <div class="cms-copy">
                  @for (segment of contentSegments(block.content); track $index) {
                    @if (segment.type === 'heading') { <h4>{{ segment.value }}</h4> }
                    @else if (segment.type === 'list') { <ul>@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
                    @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
                    @else { <p>{{ segment.value }}</p> }
                  }
                </div>
              </article>
            }
          </div>
        } @else {
          <div class="cards">
            <article class="card"><h3>Öğrenci</h3><p class="card__lead">Günün işleri, haftalık süre, deneme netleri ve koçundan mesajlar.</p><a routerLink="/auth/register/student" class="ds-link">Ücretsiz kaydol</a></article>
            <article class="card"><h3>Veli</h3><p class="card__lead">Çocuğunun izin verdiği kadarını görür: haftalık uyum, denemeler, görüşmeler.</p><a href="https://veli.maxkoc.com/auth/login" class="ds-link">Veli girişi</a></article>
            <article class="card"><h3>Öğretmen ve kurum</h3><p class="card__lead">Öğrencilerin planlarını, ödevlerini ve gelişimini tek listede takip eder.</p><a href="https://ogretmen.eduivme.com/auth/register/teacher" class="ds-link">Öğretmen olarak katıl</a></article>
          </div>
        }
      </section>

      <section id="planlar" class="section" aria-labelledby="public-plans-title">
        <div class="section-head">
          <h2 id="public-plans-title">{{ plansHeading()?.title || 'Planlar' }}</h2>
          <p>{{ plansHeading()?.summary || 'Öğrenci, öğretmen ve kurumlar için koçluk planları.' }}</p>
        </div>
        @if (plansError()) {
          <div class="ds-alert ds-alert--warning" role="alert" data-testid="plans-error">
            <span>Planlar şu an yüklenemedi. Bağlantını kontrol edip yeniden dene.</span>
            <button type="button" class="ds-btn ds-btn--quiet" (click)="loadPlans()">Yeniden dene</button>
          </div>
        } @else if (plans().length) {
          <div class="cards">
            @for (plan of plans(); track plan.id) {
              <article class="card plan" [class.plan--featured]="plan.audience === 'Institution'" [attr.data-testid]="'plan-' + plan.slug">
                <p class="plan__audience">{{ audienceLabel(plan) }}</p>
                <h3>{{ plan.name }}</h3>
                <p class="card__lead">{{ plan.description }}</p>
                <p class="plan__price">{{ plan.isContactOnly ? (plan.audience === 'Institution' ? 'Kurumunuza özel' : 'Teklif alın') : priceText(plan) }}</p>
                <p class="plan__terms">{{ plan.isContactOnly ? 'Fiyat ve koşullar için bizimle iletişime geçin' : plan.durationDays + ' gün erişim' }}@if ((plan.trialDays ?? 0) > 0 && !plan.isContactOnly) {, {{ plan.trialDays }} gün ücretsiz deneme }</p>
                @if (plan.includedStudentSeats) { <p class="plan__terms">{{ plan.includedStudentSeats }} öğrenci koltuğu</p> }
                @if (plan.features.length) {
                  <ul class="plan__features">
                    @for (feature of plan.features; track feature) {
                      <li><svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true"><path d="M3 8.5l3 3 7-7" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></path></svg><span>{{ feature }}</span></li>
                    }
                  </ul>
                }
                <div class="plan__actions">
                  @if (plan.audience === 'Institution' || plan.isContactOnly) {
                    <a routerLink="/coaching/pages/kurumlar" [queryParams]="campaign()" class="ds-btn ds-btn--secondary ds-btn--block">{{ plan.audience === 'Institution' ? 'Kurum çözümlerini incele' : 'Bilgi al' }}</a>
                  } @else {
                    <a [routerLink]="registerLink(plan)" [queryParams]="registerParams(plan)" [attr.data-testid]="'plan-register-' + plan.slug" (click)="selectPlan(plan)" class="ds-btn ds-btn--primary ds-btn--block">{{ plan.audience === 'Teacher' ? 'Öğretmen olarak kaydol' : ((plan.trialDays ?? 0) > 0 ? 'Kaydol ve ücretsiz dene' : 'Kaydol ve başla') }}</a>
                    <a routerLink="/auth/login" [queryParams]="{ returnUrl: plan.audience === 'Teacher' ? '/coaching-portal' : '/coaching-portal/account/payments' }" class="ds-link plan__login">Hesabım var, giriş yap</a>
                  }
                </div>
              </article>
            }
          </div>
        } @else if (plansLoaded()) {
          <p class="empty">Yayınlanmış koçluk planı henüz yok. Planlar yayınlandığında burada görünecek.</p>
        }
      </section>

      @if (blog().items.length) {
        <section class="section" aria-labelledby="public-blog-title">
          <div class="section-head section-head--row">
            <div><h2 id="public-blog-title">{{ blogHeading()?.title || 'Daha iyi bir çalışma rutini için' }}</h2>@if (blogHeading()?.summary) { <p>{{ blogHeading()?.summary }}</p> }</div>
            <a [href]="blogHeading()?.linkUrl || '/coaching/blog'" class="ds-link">{{ blogHeading()?.linkLabel || 'Tüm yazılar' }}</a>
          </div>
          <div class="cards">
            @for (post of blog().items; track post.id) {
              <a [routerLink]="['/coaching/blog', post.slug]" class="card card--link">
                @if (isCoachingMediaUrl(post.coverImageUrl)) { <img [src]="post.coverImageUrl" [alt]="post.title" class="card__image" /> }
                <p class="card__meta">{{ post.publishedAt || post.scheduledPublishAt || post.updatedAt || post.createdAt | date:'dd.MM.yyyy' }}@if (post.author) {, {{ post.author }}}</p>
                <h3>{{ post.title }}</h3>
                <p class="card__lead card__clamp">{{ post.summary || post.content }}</p>
              </a>
            }
          </div>
        </section>
      }

      @if (testimonials().length) {
        <section class="section" aria-labelledby="coaching-testimonials-title">
          <div class="section-head"><h2 id="coaching-testimonials-title">{{ testimonialsHeading()?.title || 'Koçluk kullanıcılarından' }}</h2>@if (testimonialsHeading()?.summary) { <p>{{ testimonialsHeading()?.summary }}</p> }</div>
          <div class="cards">
            @for (item of testimonials(); track item.id) {
              <figure class="card quote">
                @if (isCoachingMediaUrl(item.imageUrl)) { <img [src]="item.imageUrl" [alt]="item.title" class="quote__image" /> }
                <blockquote>“{{ item.content }}”</blockquote>
                <figcaption><strong>{{ item.title }}</strong>@if (item.summary) { <span>{{ item.summary }}</span> }</figcaption>
              </figure>
            }
          </div>
        </section>
      }

      @if (faq().length) {
        <section class="section" aria-labelledby="coaching-home-faq-title">
          <div class="section-head"><h2 id="coaching-home-faq-title">{{ faqHeading()?.title || 'Sık sorulan sorular' }}</h2>@if (faqHeading()?.summary) { <p>{{ faqHeading()?.summary }}</p> }</div>
          <div class="faq">
            @for (item of faq(); track item.id) {
              <details>
                <summary>{{ item.title }}</summary>
                <div class="cms-copy">
                  @for (segment of contentSegments(item.content); track $index) {
                    @if (segment.type === 'heading') { <h3>{{ segment.value }}</h3> }
                    @else if (segment.type === 'list') { <ul>@for (part of segment.items; track $index) { <li>{{ part }}</li> }</ul> }
                    @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
                    @else { <p>{{ segment.value }}</p> }
                  }
                </div>
              </details>
            }
          </div>
        </section>
      }

      <section class="closing" aria-labelledby="coaching-cta-title">
        <div>
          <h2 id="coaching-cta-title">{{ closingCta()?.title || 'Bu haftanın planını bugün kur.' }}</h2>
          <p>{{ closingCta()?.summary || 'Hesap açmak ücretsiz. Boş saatlerini gir, ilk haftan hazır olsun.' }}</p>
        </div>
        <a [href]="closingCta()?.linkUrl || '/auth/register/student'" class="ds-btn ds-btn--primary ds-btn--large">{{ closingCta()?.linkLabel || 'Ücretsiz hesap aç' }}</a>
      </section>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .home { display: flex; flex-direction: column; gap: 72px; color: var(--ds-text); }
    h1, h2, h3, h4, p, ul, ol, figure, blockquote { margin: 0; }

    .hero { display: grid; grid-template-columns: minmax(0, 1.1fr) minmax(0, 0.9fr); align-items: center; gap: 56px; padding-top: 24px; }
    .hero__copy { display: flex; flex-direction: column; gap: 20px; }
    .hero__title { max-width: 620px; font-size: clamp(36px, 4.6vw, 56px); font-weight: 700; line-height: 1.06; letter-spacing: -0.025em; text-wrap: balance; }
    .hero__lead { max-width: 560px; color: var(--ds-muted); font-size: 19px; line-height: 1.6; }
    .hero__actions { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 4px; }
    .hero__note { color: var(--ds-muted); font-size: 15px; }

    .week { padding: 20px; border: 1px solid var(--ds-line); border-radius: var(--ds-radius); background: var(--ds-surface); box-shadow: var(--ds-shadow); }
    .week__head { display: flex; justify-content: space-between; align-items: baseline; padding-bottom: 14px; }
    .week__head span { color: var(--ds-muted); font-size: 14px; }
    .week__days { display: flex; flex-direction: column; gap: 6px; }
    .week__day { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; padding: 8px 10px; border-radius: var(--ds-radius-control); }
    .week__day--today { background: var(--ds-soft); box-shadow: inset 4px 0 0 var(--ds-accent); }
    .week__name { width: 40px; color: var(--ds-muted); font-size: 14px; font-weight: 600; }
    .week__lesson { padding: 3px 9px; border-radius: 6px; font-size: 13px; font-weight: 600; background: var(--ds-bg); color: var(--ds-text); }
    .week__lesson[data-subject='math'] { background: var(--ds-math-bg); color: var(--ds-math-fg); }
    .week__lesson[data-subject='turkish'] { background: var(--ds-turkish-bg); color: var(--ds-turkish-fg); }
    .week__lesson[data-subject='science'] { background: var(--ds-science-bg); color: var(--ds-science-fg); }
    .week__lesson[data-subject='history'] { background: var(--ds-history-bg); color: var(--ds-history-fg); }
    .week__foot { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px 16px; margin-top: 14px; padding-top: 14px; border-top: 1px solid var(--ds-line); color: var(--ds-muted); font-size: 14px; }
    .week__foot strong { color: var(--ds-text); font-variant-numeric: tabular-nums; }

    .section, .band { display: flex; flex-direction: column; gap: 24px; }
    .band { padding: 40px; border-radius: var(--ds-radius); background: var(--ds-surface); border: 1px solid var(--ds-line); }
    .section-head { display: flex; flex-direction: column; gap: 8px; max-width: 680px; }
    .section-head--row { flex-direction: row; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; max-width: none; }
    .section-head--row > div { display: flex; flex-direction: column; gap: 8px; max-width: 680px; }
    .section-head h2 { font-size: clamp(26px, 2.6vw, 32px); font-weight: 700; line-height: 1.2; letter-spacing: -0.015em; }
    .section-head p { color: var(--ds-muted); font-size: 17px; line-height: 1.6; }

    .steps { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 24px; padding: 0; list-style: none; }
    .step { display: flex; flex-direction: column; gap: 8px; padding-top: 16px; border-top: 3px solid var(--ds-primary); }
    .step__no { color: var(--ds-primary); font-size: 14px; font-weight: 600; }
    .step h3 { font-size: 19px; font-weight: 600; line-height: 1.3; }
    .step p { color: var(--ds-muted); line-height: 1.6; }

    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
    .card { display: flex; flex-direction: column; gap: 10px; padding: 22px; border: 1px solid var(--ds-line); border-radius: var(--ds-radius); background: var(--ds-surface); color: var(--ds-text); }
    .card h3 { font-size: 19px; font-weight: 600; line-height: 1.3; }
    .card__lead { color: var(--ds-muted); line-height: 1.6; }
    .card__meta { color: var(--ds-muted); font-size: 14px; }
    .card__image { width: 100%; height: 160px; border-radius: var(--ds-radius-control); object-fit: cover; }
    .card__clamp { display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
    .card--link { text-decoration: none; transition: border-color 150ms ease; }
    .card--link:hover { border-color: var(--ds-primary); }
    .card .ds-link { margin-top: auto; align-self: flex-start; }

    .plan__audience { color: var(--ds-primary); font-size: 14px; font-weight: 600; }
    .plan--featured { border-color: var(--ds-primary); box-shadow: var(--ds-shadow); }
    .plan__price { margin-top: 6px; font-size: 26px; font-weight: 700; font-variant-numeric: tabular-nums; }
    .plan__terms { color: var(--ds-muted); font-size: 14px; }
    .plan__features { display: flex; flex-direction: column; gap: 8px; padding: 0; list-style: none; }
    .plan__features li { display: flex; gap: 8px; line-height: 1.5; }
    .plan__features svg { flex: none; margin-top: 4px; color: var(--ds-success); }
    .plan__actions { display: flex; flex-direction: column; align-items: center; gap: 4px; margin-top: auto; padding-top: 8px; }
    .plan__login { display: inline-flex; align-items: center; min-height: 44px; font-size: 14px; }

    .quote blockquote { line-height: 1.6; }
    .quote figcaption span { margin-left: 8px; color: var(--ds-muted); }
    .quote__image { width: 48px; height: 48px; border-radius: 50%; object-fit: cover; }

    .faq { border: 1px solid var(--ds-line); border-radius: var(--ds-radius); background: var(--ds-surface); }
    .faq details + details { border-top: 1px solid var(--ds-line); }
    .faq summary { display: flex; align-items: center; min-height: 52px; padding: 0 18px; font-weight: 600; cursor: pointer; }
    .faq .cms-copy { padding: 0 18px 18px; }

    .cms-copy { display: flex; flex-direction: column; gap: 10px; color: var(--ds-muted); line-height: 1.6; }
    .cms-copy h2, .cms-copy h3, .cms-copy h4 { color: var(--ds-text); font-size: 16px; font-weight: 600; }
    .cms-copy ul { padding-left: 20px; }
    .cms-copy img { max-height: 288px; border-radius: var(--ds-radius-control); object-fit: cover; }
    .cms-copy p { white-space: pre-wrap; }

    .empty { padding: 20px; border: 1px dashed var(--ds-field); border-radius: var(--ds-radius); color: var(--ds-muted); }
    .closing { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 20px; padding: 36px 40px; border-radius: var(--ds-radius); background: var(--ds-visual-bg); }
    .closing h2 { font-size: 26px; font-weight: 700; line-height: 1.2; }
    .closing p { margin-top: 6px; color: var(--ds-visual-muted); }
    .ds-alert .ds-btn--quiet { margin-left: -8px; }

    @media (max-width: 960px) { .hero { grid-template-columns: 1fr; gap: 32px; } .steps { grid-template-columns: 1fr; } .band, .closing { padding: 24px; } }
  `]
})
export class CoachingPublicHomeComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly seo = inject(SiteSeoService);
  private readonly analytics = inject(SiteAnalyticsService);
  readonly plansError = signal(false);
  readonly plansLoaded = signal(false);
  readonly hero = signal<CoachingCmsEntry | null>(null);
  readonly areaHeading = signal<CoachingCmsEntry | null>(null);
  readonly blocks = signal<CoachingCmsEntry[]>([]);
  readonly processHeading = signal<CoachingCmsEntry | null>(null);
  readonly processBlocks = signal<CoachingCmsEntry[]>([]);
  readonly plansHeading = signal<CoachingCmsEntry | null>(null);
  readonly blogHeading = signal<CoachingCmsEntry | null>(null);
  readonly closingCta = signal<CoachingCmsEntry | null>(null);
  readonly faqHeading = signal<CoachingCmsEntry | null>(null);
  readonly testimonialsHeading = signal<CoachingCmsEntry | null>(null);
  readonly faq = signal<CoachingCmsEntry[]>([]);
  readonly testimonials = signal<CoachingCmsEntry[]>([]);
  readonly plans = signal<CoachingSubscriptionPlan[]>([]);
  readonly currency = signal<string | null>(null);
  readonly blog = signal<CoachingManagementPage<CoachingCmsEntry>>({ items: [], pageNumber: 1, pageSize: 3, totalCount: 0 });
  readonly contentSegments = parseCoachingCmsContent;
  /** Hero illustration only (aria-hidden; the figure's label tells the same). */
  readonly sampleWeek = [
    { name: 'Pzt', today: false, lessons: [{ subject: 'math', label: 'Mat 40' }, { subject: 'turkish', label: 'Türkçe 30' }] },
    { name: 'Sal', today: true, lessons: [{ subject: 'science', label: 'Fen 35' }, { subject: 'math', label: 'Mat 40' }, { subject: 'history', label: 'İnk 25' }] },
    { name: 'Çar', today: false, lessons: [{ subject: 'math', label: 'Mat 40' }, { subject: 'turkish', label: 'Türkçe 30' }, { subject: 'science', label: 'Fen 35' }] },
    { name: 'Per', today: false, lessons: [{ subject: 'math', label: 'Mat 40' }, { subject: 'other', label: 'Din 20' }] },
    { name: 'Cum', today: false, lessons: [{ subject: 'other', label: 'İng 30' }, { subject: 'other', label: 'Görüşme' }] }
  ] as const;

  isCoachingMediaUrl(value: string | null | undefined): value is string {
    return !!value && /^\/api\/coaching\/cms\/media\/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
  }

  ngOnInit(): void {
    this.service.getPublicCmsBlocks('HomeHero').subscribe({
      next: blocks => {
        const hero = blocks[0] ?? null;
        this.hero.set(hero);
        this.seo.apply({ title: hero?.seoTitle || `${hero?.title || 'Öğrenci Koçluğu'} | maxkoç`, description: hero?.seoDescription || hero?.summary,
          path: '/coaching', jsonLd: this.seo.organization() });
      },
      error: () => { this.hero.set(null); this.seo.apply({ title: 'Öğrenci Koçluğu | maxkoç', path: '/coaching', jsonLd: this.seo.organization() }); }
    });
    this.loadSingleBlock('HomeAreaHeading', this.areaHeading);
    this.loadSingleBlock('HomeProcessHeading', this.processHeading);
    this.loadSingleBlock('HomePlansHeading', this.plansHeading);
    this.loadSingleBlock('HomeBlogHeading', this.blogHeading);
    this.loadSingleBlock('HomeClosingCta', this.closingCta);
    this.loadSingleBlock('HomeFaqHeading', this.faqHeading);
    this.loadSingleBlock('HomeTestimonialsHeading', this.testimonialsHeading);
    this.service.getPublicCmsBlocks('HomePage').subscribe({ next: blocks => this.blocks.set(blocks), error: () => this.blocks.set([]) });
    this.service.getPublicCmsBlocks('HomeHowItWorks').subscribe({ next: blocks => this.processBlocks.set(blocks), error: () => this.processBlocks.set([]) });
    this.service.getPublicCmsBlocks('HomeFaq').subscribe({ next: blocks => this.faq.set(blocks), error: () => this.faq.set([]) });
    this.service.getPublicCmsBlocks('HomeTestimonials').subscribe({ next: blocks => this.testimonials.set(blocks), error: () => this.testimonials.set([]) });
    this.loadPlans();
    this.service.getPublicPlanCurrency().subscribe({ next: currency => this.currency.set(currency), error: () => this.currency.set(null) });
    this.service.getPublicCmsBlog(1, 3).subscribe({ next: page => this.blog.set(page), error: () => this.blog.set({ items: [], pageNumber: 1, pageSize: 3, totalCount: 0 }) });
  }

  /** A failed request is not shown as "no plans": the visitor gets an error and a retry. */
  loadPlans(): void {
    this.plansError.set(false);
    this.service.getPublicSubscriptionPlans().subscribe({
      next: plans => { this.plans.set(plans.filter(plan => plan.isActive && plan.isPublic)); this.plansLoaded.set(true); },
      error: () => { this.plans.set([]); this.plansError.set(true); }
    });
  }

  /** Formatted with Intl so no Angular locale data is needed ("2.400 ₺"). */
  priceText(plan: CoachingSubscriptionPlan): string {
    const currency = /^[A-Z]{3}$/.test(this.currency() ?? '') ? this.currency()! : 'TRY';
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency, minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(plan.price);
  }

  audienceLabel(plan: CoachingSubscriptionPlan): string {
    return plan.audience === 'Institution' ? 'Kurumlar için' : plan.audience === 'Teacher' ? 'Öğretmenler için' : 'Öğrenciler için';
  }

  registerLink(plan: CoachingSubscriptionPlan): string {
    return plan.audience === 'Teacher' ? '/auth/register/teacher' : '/auth/register/student';
  }

  /** Students come back to the subscription page with the plan chosen; campaign tags travel with the link. */
  registerParams(plan: CoachingSubscriptionPlan): Record<string, string> {
    return { ...this.campaign(), plan: plan.slug, ...(plan.audience === 'Teacher' ? {} : { returnUrl: `/coaching-portal/account/payments?plan=${plan.slug}` }) };
  }

  campaign(): Record<string, string> {
    const utm = this.analytics.registrationUtm();
    const params: Record<string, string> = {};
    if (utm?.utmSource) params['utm_source'] = utm.utmSource;
    if (utm?.utmMedium) params['utm_medium'] = utm.utmMedium;
    if (utm?.utmCampaign) params['utm_campaign'] = utm.utmCampaign;
    return params;
  }

  selectPlan(plan: CoachingSubscriptionPlan): void {
    this.analytics.track('select_item', { item_list_name: 'coaching_plans', item_id: plan.slug, item_name: plan.name });
  }

  private loadSingleBlock(group: string, target: { set(value: CoachingCmsEntry | null): void }): void {
    this.service.getPublicCmsBlocks(group).subscribe({ next: blocks => target.set(blocks[0] ?? null), error: () => target.set(null) });
  }
}
