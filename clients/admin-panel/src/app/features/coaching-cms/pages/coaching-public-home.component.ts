import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RevealDirective } from '../../../shared/public-site/reveal.directive';
import { RouterLink } from '@angular/router';
import { CoachingCmsEntry, CoachingManagementPage, CoachingManagementService, CoachingSubscriptionPlan } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';
import { SiteAnalyticsService } from '../../../core/services/site-analytics.service';
import { SiteSeoService } from '../../../core/services/site-seo.service';

@Component({
  selector: 'app-coaching-public-home',
  standalone: true,
  imports: [CommonModule, RouterLink, RevealDirective],
  template: `
    <div class="home">
      <section class="hero" aria-labelledby="coaching-home-title">
        <div class="hero__glow" aria-hidden="true"><span class="hero__blob hero__blob--ink"></span><span class="hero__blob hero__blob--sun"></span><span class="hero__blob hero__blob--leaf"></span></div>
        <div class="hero__copy">
          @if (hero()?.eyebrow) { <p class="hero__eyebrow"><span class="hero__dot" aria-hidden="true"></span>{{ hero()?.eyebrow }}</p> }
          <h1 id="coaching-home-title" class="hero__title">{{ hero()?.title || 'Sınava giden yol, haftalık bir plandan geçer.' }}</h1>
          <p class="hero__lead">{{ hero()?.summary || 'Boş saatlerini işaretle, planın hazırlansın. Her gün ne çalışacağını bil, deneme netlerini takip et, koçunla haftayı birlikte değerlendir.' }}</p>
          @if (hero()?.content) {
            <div class="cms-copy cms-copy--hero">
              @for (segment of contentSegments(hero()!.content); track $index) {
                @if (segment.type === 'heading') { <h2>{{ segment.value }}</h2> }
                @else if (segment.type === 'list') { <ul class="ticks">@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
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
                @if (day.today) { <span class="week__today">Bugün</span> }
              </div>
            }
          </div>
          <div class="week__foot" aria-hidden="true">
            <span>Bu hafta <strong>9 sa 40 dk</strong></span>
            <span>Son deneme <strong>72 net</strong></span>
            <span>Hedef <strong>80 net</strong></span>
          </div>
          <div class="week__progress" aria-hidden="true"><span style="width: 90%"></span></div>
        </figure>
      </section>

      <section id="nasil" class="band" aria-labelledby="coaching-process-title" publicReveal>
        <div class="section-head">
          <h2 id="coaching-process-title">{{ processHeading()?.title || 'Nasıl çalışır?' }}</h2>
          <p>{{ processHeading()?.summary || 'Hedef belirleme, planlı çalışma ve düzenli değerlendirme; öğrenci, öğretmen ve veli aynı planın üzerinde.' }}</p>
        </div>
        <ol class="steps">
          @if (processBlocks().length) {
            @for (block of processBlocks(); track block.id; let index = $index) {
              <li class="step" [attr.data-tone]="tone(index)" [publicReveal]="index">
                <span class="step__no" aria-hidden="true">{{ index + 1 }}</span>
                <h3><span class="ds-visually-hidden">{{ index + 1 }}. adım: </span>{{ block.title }}</h3>
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
            <li class="step" data-tone="math" publicReveal="0"><span class="step__no" aria-hidden="true">1</span><h3><span class="ds-visually-hidden">1. adım: </span>Hedefini ve boş saatlerini gir</h3><p>Hangi sınava hazırlandığını, okul dışında hangi saatlerde çalışabileceğini işaretle.</p></li>
            <li class="step" data-tone="turkish" publicReveal="1"><span class="step__no" aria-hidden="true">2</span><h3><span class="ds-visually-hidden">2. adım: </span>Planın günlere bölünsün</h3><p>Dersler ve konular haftaya yerleşir. Her gün açtığında yalnız o günün işlerini görürsün.</p></li>
            <li class="step" data-tone="science" publicReveal="2"><span class="step__no" aria-hidden="true">3</span><h3><span class="ds-visually-hidden">3. adım: </span>Koçunla haftayı değerlendir</h3><p>Denemelerin, ödevlerin ve çalıştığın süre koçunun ekranında. Bir sonraki haftayı birlikte ayarlarsınız.</p></li>
          }
        </ol>
      </section>

      <section class="section" aria-labelledby="coaching-areas-title">
        <div class="section-head section-head--row" publicReveal>
          <div>
            <h2 id="coaching-areas-title">{{ areaHeading()?.title || 'Herkes aynı planı kendi yerinden görür' }}</h2>
            @if (areaHeading()?.summary) { <p>{{ areaHeading()?.summary }}</p> }
          </div>
          @if (areaHeading()?.linkUrl) { <a [href]="areaHeading()?.linkUrl" class="ds-link">{{ areaHeading()?.linkLabel || 'Ayrıntılar' }}</a> }
        </div>
        @if (blocks().length) {
          <div class="cards cards--people" aria-label="Koçluk ana sayfa içerikleri">
            @for (block of blocks(); track block.id; let index = $index) {
              <article class="card person public-lift" [attr.data-tone]="tone(index)" [publicReveal]="index">
                <span class="person__icon" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="personIcon(index)"/></svg></span>
                <h3>{{ block.title }}</h3>
                @if (block.summary) { <p class="card__lead">{{ block.summary }}</p> }
                <div class="cms-copy">
                  @for (segment of contentSegments(block.content); track $index) {
                    @if (segment.type === 'heading') { <h4>{{ segment.value }}</h4> }
                    @else if (segment.type === 'list') { <ul class="ticks">@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
                    @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
                    @else { <p>{{ segment.value }}</p> }
                  }
                </div>
              </article>
            }
          </div>
        } @else {
          <div class="cards cards--people">
            <article class="card person public-lift" data-tone="math" publicReveal="0"><span class="person__icon" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="personIcon(0)"/></svg></span><h3>Öğrenci</h3><p class="card__lead">Günün işleri, haftalık süre, deneme netleri ve koçundan mesajlar.</p><a routerLink="/auth/register/student" class="ds-link">Ücretsiz kaydol</a></article>
            <article class="card person public-lift" data-tone="turkish" publicReveal="1"><span class="person__icon" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="personIcon(1)"/></svg></span><h3>Veli</h3><p class="card__lead">Çocuğunun izin verdiği kadarını görür: haftalık uyum, denemeler, görüşmeler.</p><a href="https://veli.maxkoc.com/auth/login" class="ds-link">Veli girişi</a></article>
            <article class="card person public-lift" data-tone="science" publicReveal="2"><span class="person__icon" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="personIcon(2)"/></svg></span><h3>Öğretmen ve kurum</h3><p class="card__lead">Öğrencilerin planlarını, ödevlerini ve gelişimini tek listede takip eder.</p><a href="https://ogretmen.eduivme.com/auth/register/teacher" class="ds-link">Öğretmen olarak katıl</a></article>
          </div>
        }
      </section>

      <section id="planlar" class="section plans" aria-labelledby="public-plans-title">
        <div class="section-head section-head--center" publicReveal>
          <p class="pill"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>Öğrenciler ücretsiz kullanır</p>
          <h2 id="public-plans-title">{{ plansHeading()?.title || 'Planlar' }}</h2>
          <p>{{ plansHeading()?.summary || 'Öğrenci, öğretmen ve kurumlar için koçluk planları.' }}</p>
        </div>
        @if (plansError()) {
          <div class="ds-alert ds-alert--warning" role="alert" data-testid="plans-error">
            <span>Planlar şu an yüklenemedi. Bağlantını kontrol edip yeniden dene.</span>
            <button type="button" class="ds-btn ds-btn--quiet" (click)="loadPlans()">Yeniden dene</button>
          </div>
        } @else if (plans().length) {
          @if (picked(); as pick) {
            <!-- 27.11.2026: teacher and institution packages are chosen here (who pays, period, students) and priced openly. -->
            <div class="picker" data-testid="plan-picker" publicReveal>
              <div class="picker__controls">
                @if (packageGroups().length > 1) {
                  <div class="segmented" role="group" aria-label="Kimin için">
                    @for (group of packageGroups(); track group.audience) {
                      <button type="button" [attr.aria-pressed]="pick.group.audience === group.audience" (click)="chooseAudience(group.audience)"
                        [attr.data-testid]="'plan-group-' + group.audience">{{ group.audience === 'Institution' ? 'Kurumlar için' : 'Öğretmenler için' }}</button>
                    }
                  </div>
                }
                @if (pick.periods.length > 1) {
                  <div class="segmented" role="group" aria-label="Ödeme dönemi">
                    @for (days of pick.periods; track days) {
                      <button type="button" [attr.aria-pressed]="pick.days === days" (click)="chooseDays(days)">
                        {{ periodLabel(days) }}@if (days >= 360 && pick.bestSaving > 0) {<span class="save">%{{ pick.bestSaving }}'e varan tasarruf</span>}
                      </button>
                    }
                  </div>
                }
                <div class="seats">
                  <p class="seats__label" id="seat-label">Kaç öğrenci?</p>
                  <div class="seats__list" role="group" aria-labelledby="seat-label">
                    @for (seats of pick.seatOptions; track seats) {
                      <button type="button" [attr.aria-pressed]="pick.seats === seats" (click)="chooseSeats(seats)">{{ seats }}</button>
                    }
                  </div>
                </div>
                <p class="picker__terms">Her pakette bütün özellikler var; paketler yalnız öğrenci sayısı ve süreyle ayrılır. Dönem içinde ek öğrenci alınabilir.</p>
              </div>
              <div class="price-card" [attr.data-testid]="'plan-' + pick.plan.slug">
                <p class="price-card__who">{{ pick.group.audience === 'Institution' ? 'Kurum paketi' : 'Öğretmen paketi' }}</p>
                <p class="price-card__name">{{ pick.plan.name }}</p>
                <p class="price-card__price" aria-live="polite"><strong>{{ priceText(pick.plan) }}</strong><span> / {{ periodLabel(pick.days).toLocaleLowerCase('tr') }}</span></p>
                <ul class="price-card__facts">
                  <li>Öğrenci başına ayda yaklaşık <strong>{{ amountText(pick.perStudentMonthly) }}</strong></li>
                  @if (pick.saving > 0) { <li>3 aylık ödemeye göre <strong>{{ amountText(pick.saving) }}</strong> tasarruf</li> }
                  @if (pick.plan.extraSeatPrice) { <li>Ek öğrenci: {{ amountText(pick.plan.extraSeatPrice) }}</li> }
                </ul>
                <a [routerLink]="pick.group.audience === 'Institution' ? '/auth/register/institution' : '/auth/register/teacher'" [queryParams]="campaign()"
                  [attr.data-testid]="'plan-group-register-' + pick.group.audience" (click)="selectPlan(pick.plan)"
                  class="ds-btn ds-btn--large ds-btn--block price-card__cta">{{ pick.group.audience === 'Institution' ? 'Kurum olarak kaydol' : 'Öğretmen olarak kaydol' }}</a>
                <a routerLink="/auth/login" [queryParams]="{ returnUrl: '/coaching-portal' }" class="price-card__login">Hesabım var, giriş yap</a>
              </div>
              @if (pick.group.features.length) {
                <div class="picker__features">
                  <p class="picker__features-title">Her pakette</p>
                  <ul class="feature-grid">
                    @for (feature of pick.group.features; track feature) {
                      <li><span class="tick" aria-hidden="true"><svg width="14" height="14" viewBox="0 0 16 16"><path d="M3 8.5l3 3 7-7" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"></path></svg></span>{{ feature }}</li>
                    }
                  </ul>
                </div>
              }
            </div>
          }
          @if (singlePlans().length) {
            <div class="cards cards--plans">
              @for (plan of singlePlans(); track plan.id; let index = $index) {
                <article class="card plan public-lift" [class.plan--featured]="plan.audience === 'Institution'" [attr.data-testid]="'plan-' + plan.slug" [publicReveal]="index">
                  <p class="plan__audience">{{ audienceLabel(plan) }}</p>
                  <h3>{{ plan.name }}</h3>
                  <p class="card__lead">{{ plan.description }}</p>
                  <p class="plan__price">{{ plan.isContactOnly ? (plan.audience === 'Institution' ? 'Kurumunuza özel' : 'Teklif alın') : priceText(plan) }}</p>
                  <p class="plan__terms">{{ plan.isContactOnly ? 'Fiyat ve koşullar için bizimle iletişime geçin' : plan.durationDays + ' gün erişim' }}@if ((plan.trialDays ?? 0) > 0 && !plan.isContactOnly) {, {{ plan.trialDays }} gün ücretsiz deneme }</p>
                  @if (plan.includedStudentSeats) { <p class="plan__terms">{{ plan.includedStudentSeats }} öğrenci koltuğu</p> }
                  @if (plan.features.length) {
                    <ul class="plan__features">
                      @for (feature of plan.features; track feature) {
                        <li><span class="tick" aria-hidden="true"><svg width="14" height="14" viewBox="0 0 16 16"><path d="M3 8.5l3 3 7-7" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"></path></svg></span><span>{{ feature }}</span></li>
                      }
                    </ul>
                  }
                  <div class="plan__actions">
                    @if (plan.isContactOnly) {
                      <a routerLink="/coaching/pages/kurumlar" [queryParams]="campaign()" class="ds-btn ds-btn--secondary ds-btn--block">{{ plan.audience === 'Institution' ? 'Kurum çözümlerini incele' : 'Bilgi al' }}</a>
                    } @else {
                      <a [routerLink]="registerLink(plan)" [queryParams]="registerParams(plan)" [attr.data-testid]="'plan-register-' + plan.slug" (click)="selectPlan(plan)" class="ds-btn ds-btn--primary ds-btn--block">{{ plan.audience === 'Teacher' ? 'Öğretmen olarak kaydol' : plan.audience === 'Institution' ? 'Kurum olarak kaydol' : ((plan.trialDays ?? 0) > 0 ? 'Kaydol ve ücretsiz dene' : 'Kaydol ve başla') }}</a>
                      <a routerLink="/auth/login" [queryParams]="{ returnUrl: plan.audience === 'Teacher' ? '/coaching-portal' : '/coaching-portal/account/payments' }" class="ds-link plan__login">Hesabım var, giriş yap</a>
                    }
                  </div>
                </article>
              }
            </div>
          }
          <div class="free" publicReveal>
            <span class="free__icon" aria-hidden="true"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10L12 5 2 10l10 5 10-5zM6 12v5c3 2 9 2 12 0v-5"/></svg></span>
            <div><p class="free__title">Öğrenci misin?</p><p>Hesap açmak ve planını kullanmak ücretsiz. Öğretmenin ya da kurumun seni davet edebilir.</p></div>
            <a routerLink="/auth/register/student" [queryParams]="campaign()" class="ds-btn ds-btn--secondary">Ücretsiz hesap aç</a>
          </div>
        } @else if (plansLoaded()) {
          <p class="empty">Yayınlanmış koçluk planı henüz yok. Planlar yayınlandığında burada görünecek.</p>
        }
      </section>

      @if (blog().items.length) {
        <section class="section" aria-labelledby="public-blog-title">
          <div class="section-head section-head--row" publicReveal>
            <div><h2 id="public-blog-title">{{ blogHeading()?.title || 'Daha iyi bir çalışma rutini için' }}</h2>@if (blogHeading()?.summary) { <p>{{ blogHeading()?.summary }}</p> }</div>
            <a [href]="blogHeading()?.linkUrl || '/coaching/blog'" class="ds-link">{{ blogHeading()?.linkLabel || 'Tüm yazılar' }}</a>
          </div>
          <div class="cards">
            @for (post of blog().items; track post.id; let index = $index) {
              <a [routerLink]="['/coaching/blog', post.slug]" class="card card--link public-lift" [attr.data-tone]="tone(index)" [publicReveal]="index">
                @if (isCoachingMediaUrl(post.coverImageUrl)) { <img [src]="post.coverImageUrl" [alt]="post.title" class="card__image" /> } @else { <span class="card__band" aria-hidden="true"></span> }
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
          <div class="section-head" publicReveal><h2 id="coaching-testimonials-title">{{ testimonialsHeading()?.title || 'Koçluk kullanıcılarından' }}</h2>@if (testimonialsHeading()?.summary) { <p>{{ testimonialsHeading()?.summary }}</p> }</div>
          <div class="cards">
            @for (item of testimonials(); track item.id; let index = $index) {
              <figure class="card quote" [publicReveal]="index">
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
          <div class="section-head" publicReveal><h2 id="coaching-home-faq-title">{{ faqHeading()?.title || 'Sık sorulan sorular' }}</h2>@if (faqHeading()?.summary) { <p>{{ faqHeading()?.summary }}</p> }</div>
          <div class="faq" publicReveal>
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

      <section class="closing" aria-labelledby="coaching-cta-title" publicReveal>
        <span class="closing__ring closing__ring--1" aria-hidden="true"></span><span class="closing__ring closing__ring--2" aria-hidden="true"></span>
        <div class="closing__copy">
          <h2 id="coaching-cta-title">{{ closingCta()?.title || 'Bu haftanın planını bugün kur.' }}</h2>
          <p>{{ closingCta()?.summary || 'Hesap açmak ücretsiz. Boş saatlerini gir, ilk haftan hazır olsun.' }}</p>
        </div>
        <a [href]="closingCta()?.linkUrl || '/auth/register/student'" class="ds-btn ds-btn--large closing__cta">{{ closingCta()?.linkLabel || 'Ücretsiz hesap aç' }}</a>
      </section>
    </div>
  `,
  styleUrls: ['./coaching-public-home-hero.scss', './coaching-public-home.component.scss', './coaching-public-home-plans.scss']
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
    return plan.audience === 'Teacher' ? '/auth/register/teacher' : plan.audience === 'Institution' ? '/auth/register/institution' : '/auth/register/student';
  }

  /** Teacher and institution plans that differ only in students and period (two or more): one card per audience. */
  readonly packageGroups = computed(() => (['Teacher', 'Institution'] as const).map(audience => {
    const tiers = this.plans().filter(plan => plan.audience === audience && !plan.isContactOnly && !!plan.includedStudentSeats);
    if (tiers.length < 2) return null;
    const seats = tiers.map(plan => plan.includedStudentSeats!);
    const days = [...new Set(tiers.map(plan => plan.durationDays))].sort((a, b) => a - b);
    const periods = days.map(day => day >= 360 ? 'yıllık' : day % 30 === 0 ? `${day / 30} aylık` : `${day} günlük`).join(' ya da ');
    const features = tiers[0].features.filter(feature => tiers.every(plan => plan.features.includes(feature)));
    const cheapest = [...tiers].sort((a, b) => a.price - b.price)[0];
    return { audience, tiers, minSeats: Math.min(...seats), maxSeats: Math.max(...seats), periods, features, cheapest };
  }).filter(group => group !== null));
  readonly chosenAudience = signal<'Teacher' | 'Institution' | null>(null);
  readonly chosenDays = signal<number | null>(null);
  readonly chosenSeats = signal<number | null>(null);
  /** The package the visitor picked (audience, period, students), with what it costs per student and saves per year. */
  readonly picked = computed(() => {
    const groups = this.packageGroups();
    if (!groups.length) return null;
    const group = groups.find(item => item.audience === this.chosenAudience()) ?? groups[0];
    const periods = [...new Set(group.tiers.map(plan => plan.durationDays))].sort((a, b) => a - b);
    const days = periods.includes(this.chosenDays() ?? -1) ? this.chosenDays()! : periods[periods.length - 1];
    const seatOptions = [...new Set(group.tiers.map(plan => plan.includedStudentSeats!))].sort((a, b) => a - b);
    const wanted = this.chosenSeats();
    const seats = wanted !== null && seatOptions.includes(wanted) ? wanted : seatOptions[Math.min(2, seatOptions.length - 1)];
    const plan = group.tiers.find(item => item.durationDays === days && item.includedStudentSeats === seats)
      ?? group.tiers.find(item => item.includedStudentSeats === seats) ?? group.tiers[0];
    const yearlySaving = (count: number) => {
      const year = group.tiers.find(item => item.includedStudentSeats === count && item.durationDays >= 360);
      const short = group.tiers.filter(item => item.includedStudentSeats === count && item.durationDays < 360).sort((a, b) => b.durationDays - a.durationDays)[0];
      if (!year || !short) return { amount: 0, percent: 0 };
      const asYear = short.price * Math.round(year.durationDays / short.durationDays);
      return { amount: Math.max(0, asYear - year.price), percent: asYear > 0 ? Math.max(0, Math.round((1 - year.price / asYear) * 100)) : 0 };
    };
    const months = Math.max(1, Math.round(plan.durationDays / 30));
    return {
      group, periods, days, seatOptions, seats, plan,
      perStudentMonthly: plan.price / (plan.includedStudentSeats || 1) / months,
      saving: plan.durationDays >= 360 ? yearlySaving(seats).amount : 0,
      bestSaving: Math.max(0, ...seatOptions.map(count => yearlySaving(count).percent))
    };
  });

  chooseAudience(audience: 'Teacher' | 'Institution'): void { this.chosenAudience.set(audience); this.chosenSeats.set(null); }
  chooseDays(days: number): void { this.chosenDays.set(days); }
  chooseSeats(seats: number): void { this.chosenSeats.set(seats); }
  periodLabel(days: number): string { return days >= 360 ? 'Yıllık' : days % 30 === 0 ? `${days / 30} aylık` : `${days} günlük`; }

  /** An amount in the plans' currency without decimals ("66 ₺"). */
  amountText(value: number): string {
    const currency = /^[A-Z]{3}$/.test(this.currency() ?? '') ? this.currency()! : 'TRY';
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);
  }

  private readonly tones = ['math', 'turkish', 'science', 'history'] as const;
  tone(index: number): string { return this.tones[index % this.tones.length]; }
  /** Student, parent, teacher, then a chart: the people cards' icons by position. */
  personIcon(index: number): string {
    return ['M22 10L12 5 2 10l10 5 10-5zM6 12v5c3 2 9 2 12 0v-5', 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M16 3.5a4 4 0 0 1 0 7.5M22 21a7 7 0 0 0-4-6.3',
      'M4 5h16v11H4zM8 20h8M12 16v4', 'M4 20V10M10 20V4M16 20v-8M22 20H2'][index % 4];
  }

  readonly singlePlans = computed(() => {
    const grouped = new Set(this.packageGroups().flatMap(group => group.tiers));
    return this.plans().filter(plan => !grouped.has(plan));
  });

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
