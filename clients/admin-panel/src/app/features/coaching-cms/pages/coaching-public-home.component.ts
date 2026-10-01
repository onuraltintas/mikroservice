import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CoachingCmsEntry, CoachingManagementPage, CoachingManagementService, CoachingSubscriptionPlan } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="coaching-home space-y-12 sm:space-y-16">
      <section class="hero-panel overflow-hidden rounded-[2rem] px-6 py-10 text-white shadow-xl sm:px-10 sm:py-14 lg:px-14" aria-labelledby="coaching-home-title">
        <div class="hero-grid">
          <div class="relative z-10 max-w-2xl">
            <p class="eyebrow text-indigo-100">{{ hero()?.eyebrow || 'Eduİvme · Öğrenci Koçluğu' }}</p>
            <h1 id="coaching-home-title" class="mt-4 text-4xl font-semibold leading-tight tracking-tight sm:text-5xl lg:text-6xl">{{ hero()?.title || 'Hedefinden başla, adım adım ilerle.' }}</h1>
            <p class="mt-5 max-w-xl text-base leading-7 text-indigo-100 sm:text-lg">{{ hero()?.summary || 'Öğrenci, öğretmen ve kurumların hedefleri, çalışma planlarını ve ilerleme değerlendirmelerini tek bir koçluk alanında buluşturun.' }}</p>
            @if (hero()?.content) {
              <div class="hero-copy mt-4 max-w-xl space-y-3 text-sm leading-6 text-indigo-100">
                @for (segment of contentSegments(hero()!.content); track $index) {
                  @if (segment.type === 'heading') { <h2 class="font-semibold text-white">{{ segment.value }}</h2> }
                  @else if (segment.type === 'list') { <ul class="list-disc space-y-1 pl-5">@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
                  @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" class="max-h-72 rounded-xl object-cover" /> }
                  @else { <p class="whitespace-pre-wrap">{{ segment.value }}</p> }
                }
              </div>
            }
            <div class="mt-8 flex flex-wrap gap-3">
              <a [href]="hero()?.linkUrl || '/auth/login?returnUrl=%2Fcoaching-portal'" class="button-primary">{{ hero()?.linkLabel || 'Koçluk alanına giriş yap' }}</a>
              <a [href]="hero()?.secondaryLinkUrl || '/coaching/pages/nasil-calisir'" class="button-quiet">{{ hero()?.secondaryLinkLabel || 'Nasıl çalışır?' }} <span aria-hidden="true">→</span></a>
            </div>
            <p class="mt-5 text-xs text-indigo-200">Hesap ve koçluk verileri yalnız Koçluk platformu kapsamında kullanılır.</p>
          </div>

            <div class="hidden min-h-[19rem] lg:grid lg:place-items-center" aria-label="Koçluk sürecinin adımları" role="img">
            <div class="visual-card">
              <div class="visual-card-header"><span class="visual-dot"></span><span>{{ processHeading()?.title || 'Koçluk süreci' }}</span></div>
              @for (step of processBlocks().slice(0, 3); track step.id; let index = $index) {<div class="visual-step"><span class="step-number">0{{ index + 1 }}</span><span><strong>{{ step.title }}</strong><small>{{ step.summary }}</small></span><span class="step-arrow" aria-hidden="true">↗</span></div>}
              <div class="visual-note"><span class="note-mark" aria-hidden="true">✦</span><span>{{ processHeading()?.summary || 'Her adım, öğrencinin ihtiyacına göre şekillenir.' }}</span></div>
            </div>
          </div>
        </div>
      </section>

      <section class="mx-auto w-full max-w-6xl" aria-labelledby="coaching-areas-title">
        <div class="section-heading flex flex-wrap items-end justify-between gap-4">
          <div><p class="section-kicker">{{ areaHeading()?.eyebrow || 'Tek platform, farklı sorumluluklar' }}</p><h2 id="coaching-areas-title" class="mt-2 text-3xl font-semibold tracking-tight">{{ areaHeading()?.title || 'Koçluk sürecinde herkes için net bir alan' }}</h2>@if (areaHeading()?.summary) {<p class="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ areaHeading()?.summary }}</p>}</div>
          <a [href]="areaHeading()?.linkUrl || '/coaching/pages/nasil-calisir'" class="section-link">{{ areaHeading()?.linkLabel || 'Nasıl çalışır?' }} <span aria-hidden="true">→</span></a>
        </div>
        @if (blocks().length) {
          <div class="mt-7 grid gap-4 md:grid-cols-2 xl:grid-cols-3" aria-label="Koçluk ana sayfa içerikleri">
            @for (block of blocks(); track block.id) {
              <article class="audience-card">
                <span class="card-mark" aria-hidden="true">{{ block.sortOrder === 0 ? '01' : block.sortOrder === 1 ? '02' : '03' }}</span>
                <h3 class="mt-5 text-xl font-semibold tracking-tight">{{ block.title }}</h3>
                @if (block.summary) { <p class="mt-2 text-sm leading-6 text-slate-600 dark:text-slate-300">{{ block.summary }}</p> }
                <div class="cms-copy mt-4 space-y-3 text-sm leading-6 text-slate-700 dark:text-slate-200">
                  @for (segment of contentSegments(block.content); track $index) {
                    @if (segment.type === 'heading') { <h4 class="font-semibold text-slate-900 dark:text-white">{{ segment.value }}</h4> }
                    @else if (segment.type === 'list') { <ul class="list-disc space-y-1 pl-5">@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
                    @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" class="max-h-72 rounded-xl object-cover" /> }
                    @else { <p class="whitespace-pre-wrap">{{ segment.value }}</p> }
                  }
                </div>
              </article>
            }
          </div>
        } @else {
          <p class="mt-6 rounded-2xl border border-dashed border-slate-300 p-6 text-sm text-slate-600 dark:border-slate-700 dark:text-slate-300">Koçluk alanları yakında burada yer alacak.</p>
        }
      </section>

      <section class="process-panel mx-auto w-full max-w-6xl rounded-[1.75rem] p-6 sm:p-9" aria-labelledby="coaching-process-title">
        <div class="max-w-2xl"><p class="section-kicker">{{ processHeading()?.eyebrow || 'Sade ve takip edilebilir' }}</p><h2 id="coaching-process-title" class="mt-2 text-3xl font-semibold tracking-tight">{{ processHeading()?.title || 'Nasıl çalışır?' }}</h2><p class="mt-3 text-sm leading-6 text-slate-600 dark:text-slate-300">{{ processHeading()?.summary || 'Koçluk; hedef belirleme, planlı çalışma ve düzenli değerlendirme adımlarını öğrenci, öğretmen ve kurumun ihtiyaçlarına göre bir araya getirir.' }}</p></div>
        @if (processBlocks().length) {
          <div class="process-steps mt-7 grid gap-3 md:grid-cols-3">
            @for (block of processBlocks(); track block.id; let index = $index) {
              <article><span>{{ index + 1 }}</span><h3>{{ block.title }}</h3>@if (block.summary) { <p>{{ block.summary }}</p> }<div class="mt-3 space-y-2 text-sm leading-6 text-slate-600 dark:text-slate-300">@for (segment of contentSegments(block.content); track $index) {@if (segment.type === 'heading') { <h4 class="font-semibold">{{ segment.value }}</h4> } @else if (segment.type === 'list') { <ul class="list-disc space-y-1 pl-5">@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> } @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" class="max-h-40 rounded-lg object-cover" /> } @else { <p class="whitespace-pre-wrap">{{ segment.value }}</p> }}</div></article>
            }
          </div>
        } @else {
          <div class="process-steps mt-7 grid gap-3 md:grid-cols-3">
            <article><span>1</span><h3>İhtiyacı tanımla</h3><p>Öğrenci ve koç, üzerinde çalışılacak hedefi netleştirir.</p></article>
            <article><span>2</span><h3>Adımları planla</h3><p>Hedef, izlenebilir çalışma ve görüşme adımlarına dönüşür.</p></article>
            <article><span>3</span><h3>Birlikte gözden geçir</h3><p>Ödevler, hedefler ve seanslar üzerinden ilerleme değerlendirilir.</p></article>
          </div>
        }
      </section>

      <section id="planlar" class="mx-auto w-full max-w-6xl scroll-mt-8" aria-labelledby="public-plans-title">
        <div class="section-heading"><div><p class="section-kicker">{{ plansHeading()?.eyebrow || 'Koçluk için erişim seçenekleri' }}</p><h2 id="public-plans-title" class="mt-2 text-3xl font-semibold tracking-tight">{{ plansHeading()?.title || 'Size uygun planı bulun' }}</h2><p class="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ plansHeading()?.summary || 'Koçluk planları ve erişim seçenekleri.' }}</p></div></div>
        @if (plans().length) {
          <div class="mt-7 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            @for (plan of plans(); track plan.id) {
              <article class="plan-card" [class.plan-card-featured]="plan.audience === 'Institution'">
                <p class="plan-audience">{{ plan.audience === 'Institution' ? 'Kurumlar için' : 'Bireysel kullanım' }}</p>
                <h3 class="mt-2 text-xl font-semibold">{{ plan.name }}</h3>
                <p class="mt-2 min-h-12 text-sm leading-6 text-slate-600 dark:text-slate-300">{{ plan.description }}</p>
                <p class="mt-5 text-2xl font-semibold tracking-tight">{{ plan.isContactOnly ? 'Kurumunuza özel' : currency() ? (plan.price | currency:(currency() || 'TRY'):'symbol':'1.0-2':'tr-TR') : 'Ücret bilgisi için giriş yapın' }}</p>
                <p class="mt-1 text-xs text-slate-500 dark:text-slate-400">{{ plan.isContactOnly ? 'Kurum çözümleri için bilgi alın' : currency() ? plan.durationDays + ' gün erişim' : 'Hesabınızda plan ayrıntılarını görüntüleyin' }}</p>
                @if (plan.includedStudentSeats) { <p class="mt-3 text-sm text-slate-600 dark:text-slate-300">{{ plan.includedStudentSeats }} öğrenci koltuğu</p> }
                @if (plan.features.length) { <ul class="mt-5 space-y-2 text-sm text-slate-700 dark:text-slate-200">@for (feature of plan.features; track feature) { <li class="flex gap-2"><span class="text-emerald-600" aria-hidden="true">✓</span><span>{{ feature }}</span></li> }</ul> }
                @if (plan.audience === 'Institution') {
                  <a routerLink="/coaching/pages/kurumlar" class="mt-auto inline-flex w-full justify-center rounded-xl border border-indigo-200 px-4 py-3 text-sm font-semibold text-indigo-800 transition hover:bg-indigo-50 dark:border-indigo-800 dark:text-indigo-200 dark:hover:bg-indigo-950">Kurum çözümlerini incele</a>
                } @else if (!plan.isContactOnly) {
                  <a routerLink="/auth/login" [queryParams]="{ returnUrl: '/coaching-portal/subscription' }" class="mt-auto inline-flex w-full justify-center rounded-xl bg-indigo-700 px-4 py-3 text-sm font-semibold text-white transition hover:bg-indigo-800">Planı incele ve devam et</a>
                } @else {
                  <a routerLink="/auth/login" [queryParams]="{ returnUrl: '/coaching-portal/subscription' }" class="mt-auto inline-flex w-full justify-center rounded-xl bg-indigo-700 px-4 py-3 text-sm font-semibold text-white transition hover:bg-indigo-800">Koçluk hesabına giriş yap</a>
                }
              </article>
            }
          </div>
        } @else {
          <div class="mt-7 rounded-2xl border border-dashed border-slate-300 p-6 text-sm text-slate-600 dark:border-slate-700 dark:text-slate-300">Yayınlanmış koçluk planı henüz yok. Planlar yayınlandığında burada görüntülenir.</div>
        }
      </section>

      @if (blog().items.length) {
        <section class="mx-auto w-full max-w-6xl" aria-labelledby="public-blog-title">
          <div class="section-heading flex flex-wrap items-end justify-between gap-4"><div><p class="section-kicker">{{ blogHeading()?.eyebrow || 'Koçluk kaynakları' }}</p><h2 id="public-blog-title" class="mt-2 text-3xl font-semibold tracking-tight">{{ blogHeading()?.title || 'Daha iyi bir çalışma rutini için' }}</h2>@if (blogHeading()?.summary) {<p class="mt-2 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ blogHeading()?.summary }}</p>}</div><a [href]="blogHeading()?.linkUrl || '/coaching/blog'" class="section-link">{{ blogHeading()?.linkLabel || 'Tüm yazılar' }} <span aria-hidden="true">→</span></a></div>
          <div class="mt-7 grid gap-4 md:grid-cols-3">@for (post of blog().items; track post.id) {<a [routerLink]="['/coaching/blog', post.slug]" class="blog-card">@if (isCoachingMediaUrl(post.coverImageUrl)) {<img [src]="post.coverImageUrl" [alt]="post.title" class="mb-4 h-40 w-full rounded-lg object-cover" />}<p class="text-xs text-slate-500">{{ post.publishedAt || post.scheduledPublishAt || post.updatedAt || post.createdAt | date:'dd.MM.yyyy' }} @if (post.author) {<span> · {{ post.author }}</span>}</p><h3 class="mt-3 text-lg font-semibold leading-6">{{ post.title }}</h3><p class="mt-2 line-clamp-3 text-sm leading-6 text-slate-600 dark:text-slate-300">{{ post.summary || post.content }}</p><span class="mt-5 inline-flex text-sm font-semibold text-indigo-700 dark:text-indigo-300">Yazıyı oku <span class="ml-1" aria-hidden="true">→</span></span></a>}</div>
        </section>
      }

      @if (testimonials().length) {
        <section class="mx-auto w-full max-w-6xl" aria-labelledby="coaching-testimonials-title"><div><p class="section-kicker">{{ testimonialsHeading()?.eyebrow || 'Koçluk deneyimleri' }}</p><h2 id="coaching-testimonials-title" class="mt-2 text-3xl font-semibold tracking-tight">{{ testimonialsHeading()?.title || 'Koçluk kullanıcılarından' }}</h2>@if (testimonialsHeading()?.summary) {<p class="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ testimonialsHeading()?.summary }}</p>}</div><div class="mt-7 grid gap-4 md:grid-cols-2 xl:grid-cols-3">@for (item of testimonials(); track item.id) {<figure class="testimonial-card">@if (isCoachingMediaUrl(item.imageUrl)) {<img [src]="item.imageUrl" [alt]="item.title" class="h-12 w-12 rounded-full object-cover" />}<blockquote class="mt-4 text-sm leading-6">“{{ item.content }}”</blockquote><figcaption class="mt-4"><strong>{{ item.title }}</strong>@if (item.summary) {<span class="ml-2 text-sm text-slate-500">{{ item.summary }}</span>}</figcaption></figure>}</div></section>
      }

      @if (faq().length) {
        <section class="mx-auto w-full max-w-6xl" aria-labelledby="coaching-home-faq-title"><div><p class="section-kicker">{{ faqHeading()?.eyebrow || 'Yardım ve bilgi' }}</p><h2 id="coaching-home-faq-title" class="mt-2 text-3xl font-semibold tracking-tight">{{ faqHeading()?.title || 'Sıkça sorulan sorular' }}</h2>@if (faqHeading()?.summary) {<p class="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ faqHeading()?.summary }}</p>}</div><div class="mt-6 divide-y divide-slate-200 rounded-2xl border border-slate-200 bg-white px-5 dark:divide-slate-700 dark:border-slate-800 dark:bg-slate-900">@for (item of faq(); track item.id) {<details class="py-4"><summary class="cursor-pointer font-semibold">{{ item.title }}</summary><div class="cms-article mt-3 space-y-2 text-sm leading-6 text-slate-600 dark:text-slate-300">@for (segment of contentSegments(item.content); track $index) {@if (segment.type === 'heading') {<h3 class="font-semibold">{{ segment.value }}</h3>} @else if (segment.type === 'list') {<ul class="list-disc space-y-1 pl-5">@for (part of segment.items; track $index) {<li>{{ part }}</li>}</ul>} @else if (segment.type === 'image') {<img [src]="segment.src" [alt]="segment.alt" class="max-h-72 rounded-xl object-cover" />} @else {<p class="whitespace-pre-wrap">{{ segment.value }}</p>}}</div></details>}</div></section>
      }

      <section class="closing-panel mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-5 rounded-[1.75rem] px-6 py-8 sm:px-9" aria-labelledby="coaching-cta-title">
        <div><p class="text-sm font-medium text-indigo-100">{{ closingCta()?.eyebrow || 'İlk adımını bugün at' }}</p><h2 id="coaching-cta-title" class="mt-1 text-2xl font-semibold text-white">{{ closingCta()?.title || 'Koçluk alanında hedeflerini düzenle.' }}</h2>@if (closingCta()?.summary) {<p class="mt-2 text-sm text-indigo-100">{{ closingCta()?.summary }}</p>}</div>
        <a [href]="closingCta()?.linkUrl || '/auth/login?returnUrl=%2Fcoaching-portal'" class="button-primary">{{ closingCta()?.linkLabel || 'Giriş yap veya kayıt ol' }} <span aria-hidden="true">→</span></a>
      </section>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .coaching-home { color: var(--ui-text); }
    .hero-panel { position: relative; isolation: isolate; background: radial-gradient(ellipse at 78% 18%, rgba(129, 140, 248, .42), transparent 35%), linear-gradient(125deg, #101a36 0%, #242965 52%, #4338a2 100%); }
    .hero-grid { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(19rem, .8fr); align-items: center; gap: 2rem; }
    .eyebrow, .section-kicker, .plan-audience { font-size: .75rem; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }
    .button-primary, .button-quiet { display: inline-flex; align-items: center; justify-content: center; gap: .55rem; border-radius: .85rem; padding: .85rem 1.1rem; font-size: .875rem; font-weight: 700; transition: transform .18s ease, background .18s ease; }
    .button-primary { background: white; color: #242965; box-shadow: 0 8px 22px rgba(8, 14, 40, .18); }
    .button-primary:hover { transform: translateY(-2px); background: #f2f1ff; }
    .button-quiet { color: white; border: 1px solid rgba(255,255,255,.32); }
    .button-quiet:hover { background: rgba(255,255,255,.1); }
    .visual-card { width: min(100%, 23rem); border: 1px solid rgba(255,255,255,.2); border-radius: 1.35rem; padding: 1.1rem; background: rgba(19, 26, 67, .74); box-shadow: 0 24px 55px rgba(8, 12, 36, .32); backdrop-filter: blur(14px); transform: rotate(1.5deg); }
    .visual-card-header { display: flex; align-items: center; gap: .55rem; padding: .15rem .25rem .85rem; color: #c7d2fe; font-size: .75rem; font-weight: 700; }
    .visual-dot { width: .55rem; height: .55rem; border-radius: 50%; background: #a5f3fc; box-shadow: 0 0 0 .25rem rgba(165,243,252,.12); }
    .visual-step { display: flex; align-items: center; gap: .8rem; margin-top: .5rem; border: 1px solid rgba(199,210,254,.13); border-radius: .9rem; padding: .8rem; background: rgba(255,255,255,.06); }
    .visual-step strong, .visual-step small { display: block; }
    .visual-step strong { color: #fff; font-size: .8rem; }
    .visual-step small { margin-top: .18rem; color: #c7d2fe; font-size: .68rem; }
    .step-number { color: #a5b4fc; font-size: .68rem; font-weight: 700; }
    .step-arrow { margin-left: auto; color: #a5f3fc; font-size: .9rem; }
    .visual-note { display: flex; align-items: center; gap: .65rem; margin-top: .85rem; color: #c7d2fe; font-size: .7rem; line-height: 1.4; }
    .note-mark { display: grid; width: 1.5rem; height: 1.5rem; place-items: center; border-radius: .5rem; color: #312e81; background: #a5f3fc; }
    .section-kicker { color: #4f46e5; letter-spacing: .1em; }
    .section-link { flex-shrink: 0; color: #4338ca; font-size: .875rem; font-weight: 700; }
    .audience-card, .plan-card, .blog-card { display: flex; flex-direction: column; border: 1px solid var(--ui-border); border-radius: 1.25rem; background: var(--ui-surface); padding: 1.35rem; box-shadow: 0 8px 24px rgba(21, 34, 67, .045); transition: transform .2s ease, border-color .2s ease, box-shadow .2s ease; }
    .audience-card:hover, .plan-card:hover, .blog-card:hover { transform: translateY(-3px); border-color: #a5b4fc; box-shadow: 0 14px 28px rgba(21, 34, 67, .09); }
    .card-mark { display: grid; width: 2.35rem; height: 2.35rem; place-items: center; border-radius: .75rem; background: #eef2ff; color: #4338ca; font-size: .7rem; font-weight: 800; }
    .process-panel { border: 1px solid var(--ui-border); background: linear-gradient(135deg, rgba(238,242,255,.78), var(--ui-surface) 60%); }
    .process-steps article { position: relative; min-height: 9rem; border-radius: 1rem; padding: 1.1rem; background: var(--ui-surface); border: 1px solid var(--ui-border); }
    .process-steps article > span { display: grid; width: 1.7rem; height: 1.7rem; place-items: center; border-radius: 50%; background: #e0e7ff; color: #3730a3; font-size: .75rem; font-weight: 800; }
    .process-steps h3 { margin-top: .75rem; font-size: .95rem; font-weight: 700; }
    .process-steps p { margin-top: .35rem; color: var(--ui-text-muted, #64748b); font-size: .8rem; line-height: 1.5; }
    .plan-card { min-height: 20rem; }
    .plan-card-featured { border-color: #818cf8; box-shadow: 0 10px 30px rgba(79,70,229,.12); }
    .testimonial-card { border: 1px solid var(--ui-border); border-radius: 1.25rem; background: var(--ui-surface); padding: 1.35rem; box-shadow: 0 8px 24px rgba(21, 34, 67, .045); }
    .plan-audience { color: #4f46e5; font-size: .65rem; letter-spacing: .1em; }
    @media (max-width: 1023px) { .hero-grid { grid-template-columns: minmax(0, 1fr); } }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { transition-duration: .01ms !important; scroll-behavior: auto !important; } }
  `]
})
export class CoachingPublicHomeComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
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

  isCoachingMediaUrl(value: string | null | undefined): value is string {
    return !!value && /^\/api\/coaching\/cms\/media\/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
  }

  ngOnInit(): void {
    this.service.getPublicCmsBlocks('HomeHero').subscribe({
      next: blocks => {
        const hero = blocks[0] ?? null;
        this.hero.set(hero);
        this.title.setTitle(hero?.seoTitle || `${hero?.title || 'Öğrenci Koçluğu'} | Eduİvme`);
        if (hero?.seoDescription) this.meta.updateTag({ name: 'description', content: hero.seoDescription });
      },
      error: () => this.hero.set(null)
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
    this.service.getPublicSubscriptionPlans().subscribe({ next: plans => this.plans.set(plans.filter(plan => plan.isActive && plan.isPublic)), error: () => this.plans.set([]) });
    this.service.getPublicBankTransferSettings().subscribe({ next: settings => this.currency.set(settings.currency), error: () => this.currency.set(null) });
    this.service.getPublicCmsBlog(1, 3).subscribe({ next: page => this.blog.set(page), error: () => this.blog.set({ items: [], pageNumber: 1, pageSize: 3, totalCount: 0 }) });
  }

  private loadSingleBlock(group: string, target: { set(value: CoachingCmsEntry | null): void }): void {
    this.service.getPublicCmsBlocks(group).subscribe({ next: blocks => target.set(blocks[0] ?? null), error: () => target.set(null) });
  }
}
