import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable, finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { getAdminErrorMessage } from '../../../core/auth/admin-error-message';
import { ToasterService } from '../../../core/services/toaster.service';
import {
  SpeedReadingAdminService,
  SpeedReadingAnnouncement,
  SpeedReadingAnnouncementRequest,
  SpeedReadingAnnouncementStats,
  SpeedReadingCmsBlogPost,
  SpeedReadingCmsBlogPostRequest,
  SpeedReadingCmsContactMessage,
  SpeedReadingCmsContentBlock,
  SpeedReadingCmsContentBlockRequest,
  SpeedReadingCmsPage,
  SpeedReadingCmsPageRequest,
  SpeedReadingCmsNewsletterSubscriber,
  SpeedReadingCmsMediaAsset,
  SpeedReadingCmsNavigationItem,
  SpeedReadingCmsNavigationItemRequest,
  SpeedReadingCmsRevision,
  SpeedReadingEmailCampaign,
  SpeedReadingEmailCampaignRequest,
  SpeedReadingEmailCampaignStats,
  SpeedReadingEmailTemplate,
  SpeedReadingEmailTemplateRequest,
  SpeedReadingCmsSeoSettings,
  SpeedReadingAdminNotificationPage,
  SpeedReadingBulkNotificationRequest,
  SpeedReadingBulkNotificationResult
} from '../../../core/services/speed-reading-admin.service';
import {
  buildHomePageConfig,
  DEFAULT_HOME_PAGE_CARDS,
  DEFAULT_HOME_PAGE_DRAFT,
  HomeCardsDraft,
  HomePageDraft,
  hydrateHomePageDraft
} from './speed-reading-home-page-cms';

type CommunicationTab = 'cms' | 'announcements' | 'email-templates' | 'campaigns' | 'notifications';
type CmsTab = 'homepage' | 'evidence' | 'pages' | 'blog' | 'media' | 'navigation' | 'contacts' | 'subscribers';

interface EvidenceMetricDraft {
  title: string;
  value: string;
  description: string;
  source: string;
  period: string;
  sampleSize: number | null;
  icon: string;
  isVisible: boolean;
  verified: boolean;
}

interface EvidenceMetric extends EvidenceMetricDraft {
  id: string;
  key: string;
}

const evidenceMetricsGroup = 'EvidenceMetrics';
const evidenceMetricIcons = ['insights', 'groups', 'quiz', 'speed', 'trending_up', 'school'];

@Component({
  selector: 'app-speed-reading-communications',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule],
  template: `
    <main class="space-y-6" aria-labelledby="communications-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p>
        <h1 id="communications-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">İletişim ve içerik yönetimi</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">CMS içeriğini, duyuruları, servis e-postalarını ve toplu bildirimleri gerçek API sözleşmeleriyle yönetin.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="İletişim sekmeleri">
        @for (tab of visibleTabs(); track tab.value) { <button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value" [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value" class="ui-tab rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button> }
      </nav>
      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div> }

      @if (selectedTab() === 'cms') {
        <section class="space-y-4" aria-labelledby="cms-title">
          <div class="flex flex-wrap items-end justify-between gap-3"><div><h2 id="cms-title" class="text-lg font-semibold text-gray-900 dark:text-white">CMS</h2><p class="muted">Ana sayfa, içerik sayfaları, blog, medya, menüler, iletişim mesajları ve bülten aboneleri.</p></div><button type="button" class="primary" (click)="startCmsCreate()" [disabled]="loading() || saving()">{{ loading() ? 'İçerik yükleniyor…' : cmsTab() === 'homepage' ? 'Ana sayfayı düzenle' : cmsTab() === 'evidence' ? 'Yeni kanıtlı istatistik' : cmsTab() === 'pages' ? 'Yeni sayfa' : cmsTab() === 'blog' ? 'Yeni blog yazısı' : cmsTab() === 'media' ? 'Medya yükle' : cmsTab() === 'navigation' ? 'Yeni menü öğesi' : 'Yenile' }}</button></div>
          <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="CMS sekmeleri">@for (tab of cmsTabs; track tab.value) {<button type="button" class="ui-tab secondary" [disabled]="loading()" [attr.aria-pressed]="cmsTab() === tab.value" [class.bg-gray-100]="cmsTab() === tab.value" (click)="selectCmsTab(tab.value)">{{ tab.label }}</button>}</nav>

          @if (cmsTab() === 'homepage') {
            @if (homePageEditing()) {
              <div class="dialog-backdrop-shield" aria-hidden="true"></div>
              <form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Ana sayfa düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveHomePage()">
                <h3>Ana sayfa içeriği</h3>
                <p class="muted">Ana sayfa değişiklikleri kaydedildiğinde yayımlanır. Sayısal başarı iddialarını yalnız kaynak bilgisi ve doğrulaması olan istatistiklerde kullanın. Yorumlarda yayımlama iznini doğrulayın.</p>
                <div class="form-grid">
                  <label>SEO başlığı<input [(ngModel)]="homePageDraft.seoTitle" name="homeSeoTitle" required maxlength="60" /></label>
                  <label>SEO açıklaması<input [(ngModel)]="homePageDraft.seoDescription" name="homeSeoDescription" required maxlength="160" /></label>
                  <label class="wide">Anahtar kelimeler<input [(ngModel)]="homePageDraft.seoKeywords" name="homeSeoKeywords" maxlength="250" /></label>
                  <label class="wide">OpenGraph görsel URL<input [(ngModel)]="homePageDraft.seoOgImage" name="homeSeoImage" maxlength="1000" placeholder="https://..." /></label>
                  <label>Ana başlık<input [(ngModel)]="homePageDraft.heroTitle" name="homeHeroTitle" required maxlength="90" /></label>
                  <label class="wide">Ana açıklama<textarea [(ngModel)]="homePageDraft.heroSubtitle" name="homeHeroSubtitle" required maxlength="220"></textarea></label>
                  <label>Birincil buton<input [(ngModel)]="homePageDraft.primaryActionLabel" name="homePrimaryAction" required maxlength="40" /></label>
                  <label>İkincil buton<input [(ngModel)]="homePageDraft.secondaryActionLabel" name="homeSecondaryAction" required maxlength="40" /></label>
                  <label>Özellikler başlığı<input [(ngModel)]="homePageDraft.featuresTitle" name="homeFeaturesTitle" required maxlength="90" /></label>
                  <label>Özellikler açıklaması<input [(ngModel)]="homePageDraft.featuresSubtitle" name="homeFeaturesSubtitle" required maxlength="220" /></label>
                  <label>Yaklaşım başlığı<input [(ngModel)]="homePageDraft.approachTitle" name="homeApproachTitle" required maxlength="90" /></label>
                  <label>Yaklaşım açıklaması<input [(ngModel)]="homePageDraft.approachSubtitle" name="homeApproachSubtitle" required maxlength="220" /></label>
                  <label>Fiyatlandırma başlığı<input [(ngModel)]="homePageDraft.pricingTitle" name="homePricingTitle" required maxlength="90" /></label>
                  <label>Fiyatlandırma açıklaması<input [(ngModel)]="homePageDraft.pricingSubtitle" name="homePricingSubtitle" required maxlength="220" /></label>
                  <label>Blog başlığı<input [(ngModel)]="homePageDraft.blogTitle" name="homeBlogTitle" required maxlength="90" /></label>
                  <label>Blog açıklaması<input [(ngModel)]="homePageDraft.blogSubtitle" name="homeBlogSubtitle" required maxlength="220" /></label>
                  <label>Bülten başlığı<input [(ngModel)]="homePageDraft.newsletterTitle" name="homeNewsletterTitle" required maxlength="90" /></label>
                  <label>Bülten açıklaması<input [(ngModel)]="homePageDraft.newsletterSubtitle" name="homeNewsletterSubtitle" required maxlength="220" /></label>
                  <label>SSS başlığı<input [(ngModel)]="homePageDraft.faqTitle" name="homeFaqTitle" required maxlength="90" /></label>
                  <label>SSS açıklaması<input [(ngModel)]="homePageDraft.faqSubtitle" name="homeFaqSubtitle" required maxlength="220" /></label>
                  <label>Yorumlar başlığı<input [(ngModel)]="homePageDraft.testimonialsTitle" name="homeTestimonialsTitle" required maxlength="90" /></label>
                  <label>Yorumlar açıklaması<input [(ngModel)]="homePageDraft.testimonialsSubtitle" name="homeTestimonialsSubtitle" required maxlength="220" /></label>
                  <label>Son çağrı başlığı<input [(ngModel)]="homePageDraft.ctaTitle" name="homeCtaTitle" required maxlength="90" /></label>
                  <label>Son çağrı açıklaması<input [(ngModel)]="homePageDraft.ctaSubtitle" name="homeCtaSubtitle" required maxlength="220" /></label>
                  <label>Son çağrı butonu<input [(ngModel)]="homePageDraft.ctaActionLabel" name="homeCtaAction" required maxlength="40" /></label>
                  <label>Alt açıklama<input [(ngModel)]="homePageDraft.ctaSmallText" name="homeCtaSmall" maxlength="180" /></label>
                  <fieldset class="wide">
                    <legend>Bölümler</legend>
                    <div class="flex flex-wrap gap-4">
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.featuresVisible" name="homeFeaturesVisible" /> Özellikler</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.approachVisible" name="homeApproachVisible" /> Yaklaşım</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.statsVisible" name="homeStatsVisible" /> Kanıtlı istatistik</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.pricingVisible" name="homePricingVisible" /> Fiyatlandırma</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.blogVisible" name="homeBlogVisible" /> Blog</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.newsletterVisible" name="homeNewsletterVisible" /> Bülten</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.faqVisible" name="homeFaqVisible" /> SSS</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.testimonialsVisible" name="homeTestimonialsVisible" /> Yorumlar</label>
                      <label class="check"><input type="checkbox" [(ngModel)]="homePageDraft.ctaVisible" name="homeCtaVisible" /> Son çağrı</label>
                    </div>
                  </fieldset>
                  <fieldset class="wide content-collection">
                    <legend>Güven noktaları</legend>
                    <p class="muted">Ana başlığın altında kısa ve somut ifadeler olarak görünür.</p>
                    @for (point of homePageCards.trustPoints; track $index) {
                      <div class="collection-row">
                        <input [(ngModel)]="homePageCards.trustPoints[$index]" [name]="'trustPoint' + $index" maxlength="100" required />
                        <button type="button" class="danger" (click)="removeHomeTrustPoint($index)">Kaldır</button>
                      </div>
                    }
                    <button type="button" class="secondary" (click)="addHomeTrustPoint()">Güven noktası ekle</button>
                  </fieldset>
                  <fieldset class="wide content-collection">
                    <legend>Özellik kartları</legend>
                    <p class="muted">Kartlarda ölçülebilir ve doğrulanabilir ürün özelliklerini anlatın.</p>
                    @for (item of homePageCards.features; track $index) {
                      <div class="collection-card"><div class="form-grid">
                        <label>İkon<input [(ngModel)]="item.icon" [name]="'featureIcon' + $index" maxlength="50" placeholder="speed" /></label>
                        <label>Başlık<input [(ngModel)]="item.title" [name]="'featureTitle' + $index" maxlength="90" required /></label>
                        <label class="wide">Açıklama<textarea [(ngModel)]="item.description" [name]="'featureDescription' + $index" maxlength="220" required></textarea></label>
                      </div><button type="button" class="danger" (click)="removeHomeFeature($index)">Kartı kaldır</button></div>
                    }
                    <button type="button" class="secondary" (click)="addHomeFeature()">Özellik kartı ekle</button>
                  </fieldset>
                  <fieldset class="wide content-collection">
                    <legend>Nasıl çalışır adımları</legend>
                    <p class="muted">Öğrencinin programdaki yolculuğunu kısa adımlarla anlatın.</p>
                    @for (item of homePageCards.approach; track $index) {
                      <div class="collection-card"><div class="form-grid">
                        <label>Başlık<input [(ngModel)]="item.title" [name]="'approachTitle' + $index" maxlength="90" required /></label>
                        <label>Üst etiket<input [(ngModel)]="item.role" [name]="'approachRole' + $index" maxlength="50" /></label>
                        <label class="wide">Açıklama<textarea [(ngModel)]="item.description" [name]="'approachDescription' + $index" maxlength="220" required></textarea></label>
                      </div><button type="button" class="danger" (click)="removeHomeApproach($index)">Adımı kaldır</button></div>
                    }
                    <button type="button" class="secondary" (click)="addHomeApproach()">Adım ekle</button>
                  </fieldset>
                  <fieldset class="wide content-collection">
                    <legend>Sık sorulan sorular</legend>
                    <p class="muted">Sorular bu sırayla ana sayfada görünür; en fazla sekiz soru yayımlanır.</p>
                    @for (item of homePageCards.faqItems; track $index) {
                      <div class="collection-card"><div class="form-grid">
                        <label>Kategori<input [(ngModel)]="item.category" [name]="'faqCategory' + $index" maxlength="50" /></label>
                        <label class="wide">Soru<input [(ngModel)]="item.question" [name]="'faqQuestion' + $index" maxlength="180" required /></label>
                        <label class="wide">Yanıt<textarea [(ngModel)]="item.answer" [name]="'faqAnswer' + $index" maxlength="2000" required></textarea></label>
                      </div><button type="button" class="danger" (click)="removeHomeFaq($index)">Soruyu kaldır</button></div>
                    }
                    <button type="button" class="secondary" (click)="addHomeFaq()" [disabled]="homePageCards.faqItems.length >= 8">Soru ekle</button>
                  </fieldset>
                  <fieldset class="wide content-collection">
                    <legend>Katılımcı yorumları</legend>
                    <p class="muted">Yalnızca yayımlama izni doğrulanmış yorumları ekleyin; en fazla sekiz yorum yayımlanır.</p>
                    @for (item of homePageCards.testimonials; track $index) {
                      <div class="collection-card"><div class="form-grid">
                        <label>Ad<input [(ngModel)]="item.name" [name]="'testimonialName' + $index" maxlength="120" required /></label>
                        <label>Rol / ilişki<input [(ngModel)]="item.role" [name]="'testimonialRole' + $index" maxlength="100" /></label>
                        <label>Puan (isteğe bağlı)<input type="number" [(ngModel)]="item.rating" [name]="'testimonialRating' + $index" min="0" max="5" step="1" /></label>
                        <label class="wide">Yorum<textarea [(ngModel)]="item.text" [name]="'testimonialText' + $index" maxlength="1500" required></textarea></label>
                      </div><button type="button" class="danger" (click)="removeHomeTestimonial($index)">Yorumu kaldır</button></div>
                    }
                    <button type="button" class="secondary" (click)="addHomeTestimonial()" [disabled]="homePageCards.testimonials.length >= 8">Yorum ekle</button>
                  </fieldset>
                  <fieldset class="wide content-collection">
                    <legend>Bülten faydaları</legend>
                    <p class="muted">Kayıt alanının yanında gösterilen kısa maddeler.</p>
                    @for (benefit of homePageCards.benefits; track $index) {
                      <div class="collection-row">
                        <input [(ngModel)]="homePageCards.benefits[$index]" [name]="'newsletterBenefit' + $index" maxlength="120" required />
                        <button type="button" class="danger" (click)="removeHomeBenefit($index)">Kaldır</button>
                      </div>
                    }
                    <button type="button" class="secondary" (click)="addHomeBenefit()">Fayda ekle</button>
                  </fieldset>
                </div>
                <div class="form-actions">
                  <button type="button" class="secondary" (click)="homePageEditing.set(false)">İptal</button>
                  <button class="primary" type="submit" [disabled]="saving()">Yayına kaydet</button>
                </div>
              </form>
            } @else {
              <div class="data-card">
                <h3 class="font-semibold">Ana sayfa</h3>
                <p class="muted">Metinler, listeler, bölüm görünürlüğü ve arama motoru bilgisi tek formdan yönetilir. Kaydedilen yapılandırma canlı ana sayfada yayımlanır.</p>
                <button type="button" class="primary" (click)="editHomePage()">Ana sayfayı düzenle</button>
              </div>
            }
          }


          @if (cmsTab() === 'evidence') {
            @if (evidenceEditing()) {
              <div class="dialog-backdrop-shield" aria-hidden="true"></div>
              <form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Kanıtlı istatistik düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveEvidenceMetric()">
                <h3>{{ evidenceEditingId ? 'Kanıtlı istatistiği düzenle' : 'Yeni kanıtlı istatistik' }}</h3>
                <p class="muted">Bu kayıt yalnızca kaynak, dönem ve örneklem bilgisiyle saklanır. Görünürlük kapalıyken public API ve ana sayfa kaydı sunmaz.</p>
                <div class="form-grid">
                  <label>Başlık<input [(ngModel)]="evidenceDraft.title" name="evidenceTitle" required maxlength="150" /></label>
                  <label>Değer<input [(ngModel)]="evidenceDraft.value" name="evidenceValue" required maxlength="60" placeholder="%82 veya 120" /></label>
                  <label>Kaynak<input [(ngModel)]="evidenceDraft.source" name="evidenceSource" required maxlength="500" placeholder="Pilot çalışması veya rapor bağlantısı" /></label>
                  <label>Dönem<input [(ngModel)]="evidenceDraft.period" name="evidencePeriod" required maxlength="150" placeholder="Ocak–Mart 2026" /></label>
                  <label>Örneklem<input type="number" [(ngModel)]="evidenceDraft.sampleSize" name="evidenceSampleSize" required min="1" max="10000000" step="1" /></label>
                  <label>İkon<select [(ngModel)]="evidenceDraft.icon" name="evidenceIcon">@for (icon of evidenceIcons; track icon) {<option [value]="icon">{{ icon }}</option>}</select></label>
                  <label class="wide">Açıklama<textarea [(ngModel)]="evidenceDraft.description" name="evidenceDescription" maxlength="500" placeholder="Ölçütün nasıl hesaplandığını kısaca açıklayın."></textarea></label>
                  <label class="check"><input type="checkbox" [(ngModel)]="evidenceDraft.verified" name="evidenceVerified" /> Kaynak doğrulandı</label>
                  <label class="check"><input type="checkbox" [(ngModel)]="evidenceDraft.isVisible" name="evidenceVisible" /> Son kullanıcıya göster</label>
                </div>
                <div class="form-actions"><button type="button" class="secondary" (click)="cancelEvidenceEdit()">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div>
              </form>
            }
            <div class="data-card">
              <div class="mb-4"><h3 class="font-semibold">Kanıtlı istatistikler</h3><p class="muted">Public görünürlük varsayılan olarak kapalıdır. Yalnız açık ve geçerli kayıtlar ana sayfaya ulaşır.</p></div>
              <table class="data-table"><thead><tr><th>Başlık</th><th>Değer</th><th>Kanıt</th><th>Doğrulama</th><th>Görünürlük</th><th></th></tr></thead><tbody>
                @for (metric of evidenceMetrics(); track metric.id) {<tr><td><strong>{{ metric.title }}</strong><div class="muted">{{ metric.description || '—' }}</div></td><td>{{ metric.value }}</td><td>{{ metric.source }}<div class="muted">{{ metric.period }} · n={{ metric.sampleSize }}</div></td><td>{{ metric.verified ? 'Kaynak doğrulandı' : 'Doğrulama bekliyor' }}</td><td>{{ metric.isVisible ? 'Son kullanıcıya açık' : 'Gizli' }}</td><td class="actions"><button type="button" (click)="editEvidenceMetric(metric)">Düzenle</button><button type="button" class="danger" (click)="deleteEvidenceMetric(metric)">Sil</button></td></tr>}
                @empty {<tr><td colspan="6" class="empty">Henüz kanıtlı istatistik eklenmedi.</td></tr>}
              </tbody></table>
            </div>
          }

          @if (cmsTab() === 'pages') { @if (cmsPageEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveCmsPage()"><h3>{{ cmsPageEditingId ? 'Sayfayı düzenle' : 'Yeni CMS sayfası' }}</h3><div class="form-grid"><label>Başlık<input [(ngModel)]="pageDraft.title" name="pageTitle" required maxlength="200" /></label><label>Slug<input [(ngModel)]="pageDraft.slug" name="pageSlug" required maxlength="200" [disabled]="!!cmsPageEditingId" /></label><label class="wide">İçerik<textarea [(ngModel)]="pageDraft.content" name="pageContent" required maxlength="200000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="pageDraft.isPublished" name="pagePublished" /> Yayında</label><label>Planlı yayın<input type="datetime-local" [(ngModel)]="pageDraft.scheduledPublishAt" name="pageScheduledPublishAt" /><small class="muted">İleri bir tarih seçilirse o zamana kadar public tarafta görünmez.</small></label><fieldset class="wide"><legend>Arama motoru ve paylaşım ayarları</legend><div class="form-grid"><label>Meta başlık<input [(ngModel)]="pageDraft.seoSettings.metaTitle" name="pageMetaTitle" maxlength="60" /></label><label>Meta açıklama<input [(ngModel)]="pageDraft.seoSettings.metaDescription" name="pageMetaDescription" maxlength="160" /></label><label class="wide">Anahtar kelimeler<input [(ngModel)]="pageDraft.seoSettings.metaKeywords" name="pageMetaKeywords" maxlength="250" /></label><label class="wide">Asıl URL<input [(ngModel)]="pageDraft.seoSettings.canonicalUrl" name="pageCanonicalUrl" maxlength="1000" placeholder="https://..." /></label><label>Paylaşım başlığı<input [(ngModel)]="pageDraft.seoSettings.ogTitle" name="pageOgTitle" maxlength="90" /></label><label>Paylaşım açıklaması<input [(ngModel)]="pageDraft.seoSettings.ogDescription" name="pageOgDescription" maxlength="220" /></label><label class="wide">Paylaşım görseli URL<input [(ngModel)]="pageDraft.seoSettings.ogImage" name="pageOgImage" maxlength="1000" placeholder="https://..." /></label><label class="check wide"><input type="checkbox" [(ngModel)]="pageDraft.seoSettings.noIndex" name="pageNoIndex" /> Arama motorlarının bu sayfayı dizine eklemesini engelle</label></div></fieldset></div><div class="form-actions"><button type="button" class="secondary" (click)="cancelCmsEdit()">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>} @else {<div class="data-card"><table class="data-table"><thead><tr><th>Başlık</th><th>Slug</th><th>Yayın</th><th>Güncelleme</th><th></th></tr></thead><tbody>@for (page of pages().items; track page.id) {<tr><td>{{ page.title }}</td><td class="font-mono">{{ page.slug }}</td><td>{{ page.isPublished ? (page.scheduledPublishAt && page.scheduledPublishAt > nowIso() ? 'Planlandı' : 'Yayında') : 'Taslak' }}</td><td>{{ page.updatedAt ? (page.updatedAt | date:'dd.MM.yyyy HH:mm') : '—' }}</td><td class="actions"><button type="button" (click)="editPage(page)">Düzenle</button><button type="button" (click)="previewPage(page)">Önizle</button><button type="button" (click)="showRevisions('Page', page.id)">Geçmiş</button><button type="button" class="danger" (click)="deletePage(page)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">CMS sayfası bulunamadı.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ pages().totalCount }}</span><button type="button" class="secondary" (click)="changePage('pages', -1)" [disabled]="cmsPageNumber <= 1">Önceki</button><button type="button" class="secondary" (click)="changePage('pages', 1)" [disabled]="cmsPageNumber >= cmsTotalPages(pages())">Sonraki</button></div></div>}}

          @if (cmsTab() === 'blog') { @if (cmsBlogEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveCmsBlogPost()"><h3>{{ cmsBlogEditingId ? 'Blog yazısını düzenle' : 'Yeni blog yazısı' }}</h3><div class="form-grid"><label>Başlık<input [(ngModel)]="blogDraft.title" name="blogTitle" required maxlength="200" /></label><label>Slug<input [(ngModel)]="blogDraft.slug" name="blogSlug" required maxlength="200" [disabled]="!!cmsBlogEditingId" /></label><label>Yazar<input [(ngModel)]="blogDraft.author" name="blogAuthor" maxlength="150" /></label><label>Yayın tarihi<input type="datetime-local" [(ngModel)]="blogDraft.publishedAt" name="blogPublishedAt" /></label><label>Planlı yayın<input type="datetime-local" [(ngModel)]="blogDraft.scheduledPublishAt" name="blogScheduledPublishAt" /><small class="muted">İleri bir tarih seçilirse o zamana kadar public tarafta görünmez.</small></label><label class="wide">Özet<textarea [(ngModel)]="blogDraft.summary" name="blogSummary" maxlength="1000"></textarea></label><label class="wide">İçerik<textarea [(ngModel)]="blogDraft.content" name="blogContent" required maxlength="200000"></textarea></label><label>Etiketler (virgülle)<input [(ngModel)]="blogTags" name="blogTags" maxlength="1000" /></label><label class="wide">Kapak görseli URL<input [(ngModel)]="blogDraft.coverImageUrl" name="blogCover" maxlength="1000" /><small class="muted">Güvenli görsel seçimi için medya kütüphanesinden bir görsel kullanabilirsiniz.</small></label><label class="check"><input type="checkbox" [(ngModel)]="blogDraft.isPublished" name="blogIsPublished" /> Yayında</label><fieldset class="wide"><legend>Arama motoru ve paylaşım ayarları</legend><div class="form-grid"><label>Meta başlık<input [(ngModel)]="blogDraft.seoSettings.metaTitle" name="blogMetaTitle" maxlength="60" /></label><label>Meta açıklama<input [(ngModel)]="blogDraft.seoSettings.metaDescription" name="blogMetaDescription" maxlength="160" /></label><label class="wide">Anahtar kelimeler<input [(ngModel)]="blogDraft.seoSettings.metaKeywords" name="blogMetaKeywords" maxlength="250" /></label><label class="wide">Asıl URL<input [(ngModel)]="blogDraft.seoSettings.canonicalUrl" name="blogCanonicalUrl" maxlength="1000" placeholder="https://..." /></label><label>Paylaşım başlığı<input [(ngModel)]="blogDraft.seoSettings.ogTitle" name="blogOgTitle" maxlength="90" /></label><label>Paylaşım açıklaması<input [(ngModel)]="blogDraft.seoSettings.ogDescription" name="blogOgDescription" maxlength="220" /></label><label class="wide">Paylaşım görseli URL<input [(ngModel)]="blogDraft.seoSettings.ogImage" name="blogOgImage" maxlength="1000" placeholder="https://..." /></label><label class="check wide"><input type="checkbox" [(ngModel)]="blogDraft.seoSettings.noIndex" name="blogNoIndex" /> Arama motorlarının bu yazıyı dizine eklemesini engelle</label></div></fieldset></div><div class="form-actions"><button type="button" class="secondary" (click)="cancelCmsEdit()">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>} @else {<div class="data-card"><table class="data-table"><thead><tr><th>Başlık</th><th>Yazar</th><th>Görüntülenme</th><th>Yayın</th><th></th></tr></thead><tbody>@for (post of blogPosts().items; track post.id) {<tr><td><strong>{{ post.title }}</strong><div class="muted">{{ post.slug }}</div></td><td>{{ post.author || '—' }}</td><td>{{ post.viewCount }}</td><td>{{ post.isPublished ? (post.scheduledPublishAt && post.scheduledPublishAt > nowIso() ? 'Planlandı' : 'Yayında') : 'Taslak' }}</td><td class="actions"><button type="button" (click)="editBlogPost(post)">Düzenle</button><button type="button" (click)="previewBlogPost(post)">Önizle</button><button type="button" (click)="showRevisions('Blog', post.id)">Geçmiş</button><button type="button" class="danger" (click)="deleteBlogPost(post)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Blog yazısı bulunamadı.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ blogPosts().totalCount }}</span><button type="button" class="secondary" (click)="changePage('blog', -1)" [disabled]="cmsBlogPageNumber <= 1">Önceki</button><button type="button" class="secondary" (click)="changePage('blog', 1)" [disabled]="cmsBlogPageNumber >= cmsTotalPages(blogPosts())">Sonraki</button></div></div>}}

          @if (cmsTab() === 'media') {@if (mediaEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><div class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Medya yükle" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><h3>Medya yükle</h3><div class="form-grid"><label class="wide">Görsel dosyası<input type="file" accept="image/jpeg,image/png,image/webp,image/gif" (change)="selectMediaFile($event)" /></label><label class="wide">Alternatif metin<input [(ngModel)]="mediaAltText" name="mediaAltText" maxlength="500" /></label></div><div class="form-actions"><button type="button" class="primary" (click)="uploadMedia()" [disabled]="!mediaFile || saving()">Yükle</button><button type="button" class="secondary" (click)="mediaEditing.set(false)">İptal</button></div></div>}<div class="data-card"><div class="media-grid">@for (media of mediaAssets().items; track media.id) {<article class="media-card"><img [src]="media.url" [alt]="media.altText || media.fileName" loading="lazy" /><div><strong>{{ media.fileName }}</strong><div class="muted">{{ media.sizeBytes | number }} bayt</div><div class="actions"><button type="button" class="secondary" (click)="useMediaForBlog(media)">Blog kapağı olarak kullan</button><button type="button" class="danger" (click)="deleteMedia(media)">Sil</button></div></div></article>} @empty {<p class="empty">Medya bulunamadı.</p>}</div><div class="pager"><span>Toplam {{ mediaAssets().totalCount }}</span><button type="button" class="secondary" (click)="changeMediaPage(-1)" [disabled]="cmsMediaPageNumber <= 1">Önceki</button><button type="button" class="secondary" (click)="changeMediaPage(1)" [disabled]="cmsMediaPageNumber >= cmsTotalPages(mediaAssets())">Sonraki</button></div></div>}

          @if (cmsTab() === 'navigation') { @if (navigationEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveNavigation()"><h3>{{ navigationEditingId ? 'Menü öğesini düzenle' : 'Yeni menü öğesi' }}</h3><div class="form-grid"><label>Menü<input [(ngModel)]="navigationDraft.menu" name="navigationMenu" required maxlength="50" /></label><label>Etiket<input [(ngModel)]="navigationDraft.label" name="navigationLabel" required maxlength="100" /></label><label class="wide">URL<input [(ngModel)]="navigationDraft.url" name="navigationUrl" required maxlength="500" placeholder="/blog veya https://ornek.com" /></label><label>Fragment<input [(ngModel)]="navigationDraft.fragment" name="navigationFragment" maxlength="100" placeholder="#fiyatlandirma" /></label><label>İkon<input [(ngModel)]="navigationDraft.icon" name="navigationIcon" maxlength="50" placeholder="menu_book" /></label><label>Sıra<input type="number" [(ngModel)]="navigationDraft.sortOrder" name="navigationSortOrder" min="0" max="10000" /></label><label class="check"><input type="checkbox" [(ngModel)]="navigationDraft.isVisible" name="navigationVisible" /> Görünür</label><label class="check"><input type="checkbox" [(ngModel)]="navigationDraft.openInNewTab" name="navigationNewTab" /> Yeni sekmede aç</label></div><div class="form-actions"><button type="button" class="secondary" (click)="cancelCmsEdit()">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>} @else {<div class="data-card"><div class="inline-filter"><input [(ngModel)]="navigationMenu" name="navigationMenuFilter" maxlength="50" placeholder="Menü (Main)" /><button type="button" class="secondary" (click)="loadNavigation()">Filtrele</button></div><table class="data-table"><thead><tr><th>Sıra</th><th>Etiket</th><th>URL</th><th>Durum</th><th></th></tr></thead><tbody>@for (item of navigationItems(); track item.id) {<tr><td>{{ item.sortOrder }}</td><td>{{ item.label }}</td><td class="font-mono">{{ item.url }}{{ item.fragment ? '#' + item.fragment : '' }}</td><td>{{ item.isVisible ? 'Görünür' : 'Gizli' }}{{ item.openInNewTab ? ' · Yeni sekme' : '' }}</td><td class="actions"><button type="button" (click)="editNavigation(item)">Düzenle</button><button type="button" class="danger" (click)="deleteNavigation(item)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Menü öğesi bulunamadı.</td></tr>}</tbody></table></div>}}

          @if (cmsTab() === 'contacts') {<div class="data-card"><div class="inline-filter"><select [(ngModel)]="contactReadFilter" name="contactReadFilter"><option value="">Okunma: tümü</option><option value="false">Okunmamış</option><option value="true">Okunmuş</option></select><select [(ngModel)]="contactReplyFilter" name="contactReplyFilter"><option value="">Yanıt: tümü</option><option value="false">Yanıtlanmamış</option><option value="true">Yanıtlanmış</option></select><button type="button" class="secondary" (click)="loadContacts()">Filtrele</button><span class="muted">Okunmamış: {{ unreadContacts() }}</span></div><table class="data-table"><thead><tr><th>Gönderen</th><th>Konu</th><th>Mesaj</th><th>Durum</th><th></th></tr></thead><tbody>@for (message of contacts().items; track message.id) {<tr><td>{{ message.name }}<div class="muted">{{ message.email }}</div></td><td>{{ message.subject }}</td><td class="max-w-xl whitespace-pre-wrap">{{ message.message }}</td><td>{{ message.isRead ? 'Okundu' : 'Yeni' }} · {{ message.isReplied ? 'Yanıtlandı' : 'Bekliyor' }}</td><td class="actions"><button type="button" (click)="markContactRead(message)">{{ message.isRead ? 'Okunmadı yap' : 'Okundu yap' }}</button><button type="button" (click)="openContactDialog(message)">Ayrıntı / yanıtla</button><button type="button" class="danger" (click)="deleteContact(message)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">İletişim mesajı bulunamadı.</td></tr>}</tbody></table></div>}

          @if (selectedContact(); as message) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="İletişim mesajı ayrıntısı" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="replyContact()"><h3>{{ message.subject }}</h3><div class="form-grid"><label>Gönderen<input [value]="message.name + ' · ' + message.email" disabled /></label><label>Durum<input [value]="message.isRead ? 'Okundu' : 'Yeni'" disabled /></label><label class="wide">Mesaj<textarea [value]="message.message" readonly></textarea></label><label class="wide">Yanıt<textarea [(ngModel)]="contactReplyDraft" name="contactReply" required maxlength="10000"></textarea></label></div><div class="form-actions"><button type="button" class="secondary" (click)="selectedContact.set(null)">Kapat</button><button type="submit" class="primary" [disabled]="saving() || !contactReplyDraft.trim()">Yanıtı kaydet</button></div></form>}

          @if (cmsTab() === 'subscribers') {<div class="data-card"><div class="inline-filter"><label class="check"><input type="checkbox" [(ngModel)]="includeInactiveSubscribers" name="includeInactiveSubscribers" /> Pasifleri göster</label><button type="button" class="secondary" (click)="loadSubscribers()">Filtrele</button><button type="button" class="secondary" (click)="exportSubscribers()">CSV dışa aktar</button></div><table class="data-table"><thead><tr><th>E-posta</th><th>Kaynak</th><th>Durum</th><th>Kayıt tarihi</th><th></th></tr></thead><tbody>@for (subscriber of subscribers().items; track subscriber.id) {<tr><td>{{ subscriber.email }}</td><td>{{ subscriber.source || '—' }}</td><td>{{ subscriber.isActive ? 'Aktif' : 'Pasif' }}</td><td>{{ subscriber.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td class="actions">@if (!subscriber.isActive) {<button type="button" (click)="restoreSubscriber(subscriber)">Yeniden aktifleştir</button>}<button type="button" class="danger" (click)="deleteSubscriber(subscriber)">Pasifleştir</button><button type="button" class="danger" (click)="hardDeleteSubscriber(subscriber)">Kalıcı sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Bülten abonesi bulunamadı.</td></tr>}</tbody></table></div>}

          @if (cmsPreview(); as preview) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><div class="data-card dialog-form" role="dialog" aria-modal="true" aria-label="İçerik önizlemesi" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><div class="flex items-center justify-between gap-3"><h3>{{ preview.title }} · Önizleme</h3><button type="button" class="secondary" (click)="cmsPreview.set(null)">Kapat</button></div><article class="preview whitespace-pre-wrap" [innerHTML]="preview.content"></article></div>}
          @if (cmsRevisions().length > 0) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><div class="data-card dialog-form" role="dialog" aria-modal="true" aria-label="İçerik sürüm geçmişi" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><div class="flex items-center justify-between gap-3"><h3>{{ cmsRevisionType() }} geçmişi</h3><button type="button" class="secondary" (click)="cmsRevisions.set([])">Kapat</button></div><table class="data-table"><thead><tr><th>Sürüm</th><th>Tarih</th><th>Oluşturan</th><th></th></tr></thead><tbody>@for (revision of cmsRevisions(); track revision.id) {<tr><td>{{ revision.version }}</td><td>{{ revision.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td>{{ revision.createdBy }}</td><td><button type="button" (click)="restoreRevision(revision)">Bu sürüme dön</button></td></tr>}</tbody></table></div>}
        </section>
      }

      @if (selectedTab() === 'announcements') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Duyurular</h2><p class="muted">Hedef kitle, aktiflik, yayın dönemi ve görüntülenme istatistikleri.</p></div><button type="button" class="primary" (click)="announcementEditing.set(true); announcementEditingId = null; announcementDraft = emptyAnnouncement()">Yeni duyuru</button></div>@if (announcementEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveAnnouncement()"><h3>{{ announcementEditingId ? 'Duyuruyu düzenle' : 'Yeni duyuru' }}</h3><div class="form-grid"><label>Başlık<input [(ngModel)]="announcementDraft.title" name="announcementTitle" required maxlength="200" /></label><label>Öncelik<input type="number" [(ngModel)]="announcementDraft.priority" name="announcementPriority" min="0" max="100" /></label><label class="wide">İçerik<textarea [(ngModel)]="announcementDraft.content" name="announcementContent" required maxlength="100000"></textarea></label><label class="wide">Düz metin<textarea [(ngModel)]="announcementDraft.plainTextContent" name="announcementPlainText" maxlength="100000"></textarea></label><label>Hedef kitle<input type="number" [(ngModel)]="announcementDraft.targetAudience" name="announcementAudience" min="0" max="100" /></label><label>Hedef roller (virgülle)<input [(ngModel)]="announcementTargetRoles" name="announcementRoles" maxlength="1000" /></label><label>Başlangıç<input type="datetime-local" [(ngModel)]="announcementDraft.startDate" name="announcementStart" /></label><label>Bitiş<input type="datetime-local" [(ngModel)]="announcementDraft.expiresAt" name="announcementExpires" /></label><label>Görüntü türü<input type="number" [(ngModel)]="announcementDraft.displayType" name="announcementDisplayType" min="0" max="100" /></label><label>Aksiyon metni<input [(ngModel)]="announcementDraft.actionText" name="announcementActionText" maxlength="200" /></label><label>Aksiyon URL<input [(ngModel)]="announcementDraft.actionUrl" name="announcementActionUrl" maxlength="1000" /></label><label>Renk teması<input [(ngModel)]="announcementDraft.colorTheme" name="announcementColor" maxlength="100" /></label><label class="check"><input type="checkbox" [(ngModel)]="announcementDraft.isPinned" name="announcementPinned" /> Sabitle</label>@if (!announcementEditingId) {<label class="check"><input type="checkbox" [(ngModel)]="announcementDraft.sendEmailNotification" name="announcementEmail" /> E-posta bildirimi</label><label class="check"><input type="checkbox" [(ngModel)]="announcementDraft.createInAppNotification" name="announcementInApp" /> Uygulama bildirimi</label>}</div><div class="form-actions"><button type="button" class="secondary" (click)="announcementEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>}<div class="data-card"><div class="inline-filter"><select [(ngModel)]="announcementActiveFilter" name="announcementActiveFilter"><option value="">Aktiflik: tümü</option><option value="true">Aktif</option><option value="false">Pasif</option></select><label class="check"><input type="checkbox" [(ngModel)]="announcementIncludeExpired" name="announcementExpired" /> Süresi geçenleri dahil et</label><button type="button" class="secondary" (click)="loadAnnouncements()">Filtrele</button></div><table class="data-table"><thead><tr><th>Başlık</th><th>Yayın dönemi</th><th>Durum</th><th>Görüntülenme/tıklama</th><th></th></tr></thead><tbody>@for (announcement of announcements(); track announcement.id) {<tr><td><strong>{{ announcement.title }}</strong><div class="muted">{{ announcement.targetRoles.join(', ') || 'Tüm roller' }}</div></td><td>{{ announcement.startDate ? (announcement.startDate | date:'dd.MM.yyyy HH:mm') : 'Hemen' }} – {{ announcement.expiresAt ? (announcement.expiresAt | date:'dd.MM.yyyy HH:mm') : 'Süresiz' }}</td><td>{{ announcement.isActive ? 'Aktif' : 'Pasif' }}{{ announcement.isPinned ? ' · Sabit' : '' }}</td><td>{{ announcement.viewCount }} / {{ announcement.clickCount }}</td><td class="actions"><button type="button" (click)="editAnnouncement(announcement)">Düzenle</button><button type="button" (click)="showAnnouncementStats(announcement)">İstatistik</button><button type="button" class="danger" (click)="deleteAnnouncement(announcement)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Duyuru bulunamadı.</td></tr>}</tbody></table></div>@if (announcementStats(); as stats) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><div class="data-card dialog-form" role="dialog" aria-modal="true" aria-label="Duyuru istatistikleri" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><div class="flex items-center justify-between"><strong>Duyuru istatistikleri</strong><button type="button" class="secondary" (click)="announcementStats.set(null)">Kapat</button></div><p class="muted">Görüntülenme: {{ stats.totalViewCount }} (tekil {{ stats.uniqueViewCount }}) · Tıklama: {{ stats.totalClickCount }} (tekil {{ stats.uniqueClickCount }}) · Kapatma: {{ stats.dismissCount }}</p></div>}</section>}

      @if (selectedTab() === 'email-templates') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Hızlı Okuma e-posta şablonları</h2><p class="muted">Kod, değişkenler, konu ve HTML gövdesi servis içinde yönetilir.</p></div><button type="button" class="primary" (click)="emailTemplateEditing.set(true); emailTemplateEditingId = null; emailTemplateDraft = emptyEmailTemplate()">Yeni şablon</button></div>@if (emailTemplateEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveEmailTemplate()"><h3>{{ emailTemplateEditingId ? 'Şablonu düzenle' : 'Yeni e-posta şablonu' }}</h3><div class="form-grid"><label>Ad<input [(ngModel)]="emailTemplateDraft.name" name="srEmailName" required maxlength="200" /></label><label>Kod<input [(ngModel)]="emailTemplateDraft.code" name="srEmailCode" required maxlength="100" [disabled]="!!emailTemplateEditingId" /></label><label class="wide">Konu<input [(ngModel)]="emailTemplateDraft.subject" name="srEmailSubject" required maxlength="998" /></label><label class="wide">HTML gövdesi<textarea [(ngModel)]="emailTemplateDraft.body" name="srEmailBody" required maxlength="200000"></textarea></label><label>Açıklama<textarea [(ngModel)]="emailTemplateDraft.description" name="srEmailDescription" maxlength="1000"></textarea></label><label>Kullanılabilir değişkenler<textarea [(ngModel)]="emailTemplateDraft.availableVariables" name="srEmailVariables" maxlength="10000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="emailTemplateDraft.isActive" name="srEmailActive" /> Aktif</label></div><div class="form-actions"><button type="button" class="secondary" (click)="emailTemplateEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>}<div class="data-card"><table class="data-table"><thead><tr><th>Ad/kod</th><th>Konu</th><th>Durum</th><th></th></tr></thead><tbody>@for (template of emailTemplates(); track template.id) {<tr><td><strong>{{ template.name }}</strong><div class="muted font-mono">{{ template.code }}</div></td><td>{{ template.subject }}</td><td>{{ template.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editEmailTemplate(template)">Düzenle</button><button type="button" (click)="previewEmailTemplate(template)">Önizleme</button><button type="button" class="danger" (click)="deleteEmailTemplate(template)">Sil</button></td></tr>} @empty {<tr><td colspan="4" class="empty">E-posta şablonu bulunamadı.</td></tr>}</tbody></table></div>@if (emailPreview(); as preview) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><div class="data-card dialog-form" role="dialog" aria-modal="true" aria-label="E-posta önizlemesi" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><div class="flex items-center justify-between"><strong>{{ preview.subject }}</strong><button type="button" class="secondary" (click)="emailPreview.set(null)">Kapat</button></div><div class="preview" [innerHTML]="preview.body"></div></div>}</section>}

      @if (selectedTab() === 'campaigns') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">E-posta kampanyaları</h2><p class="muted">Kampanya içeriklerini ve hedef kapsamını taslak olarak yönetin.</p></div><button type="button" class="primary" (click)="campaignEditing.set(true); campaignEditingId = null; campaignDraft = emptyCampaign()">Yeni kampanya taslağı</button></div><div role="note" class="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-200">E-posta gönderim kuyruğu ve zamanlayıcı henüz yapılandırılmadı. Bu ekranda yalnızca kampanya taslakları, mevcut kayıtlar ve istatistikler yönetilebilir.</div>@if (campaignEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Düzenleme formu" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="saveCampaign()"><h3>{{ campaignEditingId ? 'Kampanyayı düzenle' : 'Yeni kampanya taslağı' }}</h3><div class="form-grid"><label>Ad<input [(ngModel)]="campaignDraft.name" name="campaignName" required maxlength="200" /></label><label>Konu<input [(ngModel)]="campaignDraft.subject" name="campaignSubject" required maxlength="998" /></label><label class="wide">HTML gövdesi<textarea [(ngModel)]="campaignDraft.body" name="campaignBody" required maxlength="200000"></textarea></label><label class="wide">Düz metin<textarea [(ngModel)]="campaignDraft.plainTextBody" name="campaignPlainText" maxlength="200000"></textarea></label><label>Hedef roller<input [(ngModel)]="campaignDraft.targetRoles" name="campaignRoles" maxlength="1000" /></label><label>Kurum ID<input [(ngModel)]="campaignDraft.targetInstitutionId" name="campaignInstitution" /></label><label class="check"><input type="checkbox" [(ngModel)]="campaignDraft.includeAllUsers" name="campaignAll" /> Tüm kullanıcılar</label><label class="check"><input type="checkbox" [(ngModel)]="campaignDraft.includeSubscribers" name="campaignSubscribers" /> Bülten aboneleri</label></div><div class="form-actions"><button type="button" class="secondary" (click)="campaignEditing.set(false)">İptal</button><button class="primary" type="submit" [disabled]="saving()">Taslağı kaydet</button></div></form>}<div class="data-card"><table class="data-table"><thead><tr><th>Kampanya</th><th>Durum</th><th>Alıcılar</th><th>Gönderim</th><th></th></tr></thead><tbody>@for (campaign of campaigns(); track campaign.id) {<tr><td><strong>{{ campaign.name }}</strong><div class="muted">{{ campaign.subject }}</div></td><td>{{ campaign.status }}</td><td>{{ campaign.totalRecipients }} / {{ campaign.sentCount }} gönderildi</td><td>{{ campaign.sentAt ? (campaign.sentAt | date:'dd.MM.yyyy HH:mm') : (campaign.scheduledFor ? 'Zamanlama devre dışı' : 'Taslak') }}</td><td class="actions"><button type="button" (click)="editCampaign(campaign)">Düzenle</button><span class="muted" title="E-posta gönderim kuyruğu yapılandırılmadı">Gönderim kapalı</span><button type="button" (click)="showCampaignStats(campaign)">İstatistik</button><button type="button" class="danger" (click)="deleteCampaign(campaign)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Kampanya bulunamadı.</td></tr>}</tbody></table></div>@if (campaignStats(); as stats) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><div class="data-card dialog-form" role="dialog" aria-modal="true" aria-label="Kampanya istatistikleri" cdkTrapFocus [cdkTrapFocusAutoCapture]="true"><div class="flex items-center justify-between"><strong>Kampanya istatistikleri</strong><button type="button" class="secondary" (click)="campaignStats.set(null)">Kapat</button></div><p class="muted">Toplam: {{ stats.totalRecipients }} · Gönderilen: {{ stats.sentCount }} · Başarısız: {{ stats.failedCount }} · Açılan: {{ stats.openedCount }} · Tıklanan: {{ stats.clickedCount }} · Bekleyen: {{ stats.pendingCount }}</p></div>}</section>}

      @if (selectedTab() === 'notifications') {<section class="space-y-4"><div class="flex items-end justify-between gap-3"><div><h2 class="text-lg font-semibold text-gray-900 dark:text-white">Toplu bildirim ve servis bildirimleri</h2><p class="muted">Hedef kitleye uygulama içi bildirim gönderin ve gönderilmiş bildirimleri sayfalı olarak inceleyin.</p></div><button type="button" class="primary" (click)="bulkEditing.set(true)">Yeni toplu bildirim</button></div>@if (bulkEditing()) {<div class="dialog-backdrop-shield" aria-hidden="true"></div><form class="form-card dialog-form" role="dialog" aria-modal="true" aria-label="Toplu bildirim gönder" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (ngSubmit)="sendBulkNotification()"><h3>Toplu bildirim gönder</h3><div class="form-grid"><label>Hedef tipi<input [(ngModel)]="bulkDraft.targetType" name="bulkTargetType" required maxlength="50" placeholder="All, Role, Institution" /></label><label>Hedef rol<input [(ngModel)]="bulkDraft.targetRole" name="bulkTargetRole" maxlength="100" /></label><label>Başlık<input [(ngModel)]="bulkDraft.title" name="bulkTitle" required maxlength="200" /></label><label>Tür<input type="number" [(ngModel)]="bulkDraft.type" name="bulkType" min="0" max="100" /></label><label class="wide">Mesaj<textarea [(ngModel)]="bulkDraft.message" name="bulkMessage" required maxlength="10000"></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="bulkDraft.sendEmail" name="bulkSendEmail" disabled /> E-posta kanalı yapılandırılmadı</label></div><div class="form-actions"><button type="button" class="secondary" (click)="bulkEditing.set(false)">İptal</button><button type="submit" class="primary" [disabled]="saving()">Gönder</button></div></form>}<div class="data-card"><div class="inline-filter"><input [(ngModel)]="notificationSearch" name="notificationSearch" placeholder="Kullanıcı veya e-posta ara" maxlength="100" /><button type="button" class="secondary" (click)="loadNotifications()">Filtrele</button></div><table class="data-table"><thead><tr><th>Kullanıcı</th><th>Başlık</th><th>Mesaj</th><th>Durum</th><th>Tarih</th></tr></thead><tbody>@for (notification of notifications().items; track notification.id) {<tr><td>{{ notification.userName }}<div class="muted">{{ notification.userEmail }}</div></td><td>{{ notification.title }}</td><td>{{ notification.message }}</td><td>{{ notification.isRead ? 'Okundu' : 'Yeni' }}</td><td>{{ notification.createdAt | date:'dd.MM.yyyy HH:mm' }}</td></tr>} @empty {<tr><td colspan="5" class="empty">Bildirim bulunamadı.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ notifications().totalCount }}</span><button type="button" class="secondary" (click)="changeNotificationPage(-1)" [disabled]="notificationPage <= 1">Önceki</button><button type="button" class="secondary" (click)="changeNotificationPage(1)" [disabled]="notificationPage >= notificationTotalPages()">Sonraki</button></div></div></section>}

      @if (loading()) { <div role="status" class="text-center text-sm text-gray-500">Yükleniyor…</div> }
    </main>
  `,
  styles: [`
    :host { display: block; }
    .muted { color: var(--ui-text-muted); font-size: .85rem; }
    .data-card, .form-card { border: 1px solid var(--ui-border); border-radius: .75rem; padding: 1rem; background: var(--ui-surface); }
    .form-card { display: grid; gap: 1rem; }
    .form-grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); }
    label { display: grid; gap: .35rem; font-size: .875rem; font-weight: 500; color: var(--ui-text); }
    input, textarea, select { width: 100%; border: 1px solid var(--ui-border-strong); border-radius: .5rem; padding: .55rem .7rem; background: transparent; font: inherit; color: inherit; }
    textarea { min-height: 5rem; resize: vertical; }
    .wide { grid-column: 1 / -1; }
    .check { display: flex; align-items: center; gap: .5rem; } .check input { width: auto; }
    .primary, .secondary, .danger { border-radius: .5rem; padding: .55rem .8rem; font-size: .875rem; font-weight: 600; }
    .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); } .secondary { border: 1px solid var(--ui-border-strong); } .danger { color: var(--ui-danger); }
    .actions, .form-actions, .inline-filter, .pager { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; }
    .form-actions { justify-content: flex-end; } .inline-filter { margin-bottom: 1rem; }
    .content-collection { display: grid; gap: .75rem; border: 1px solid var(--ui-border); border-radius: .75rem; padding: 1rem; }
    .collection-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: .75rem; align-items: center; }
    .collection-card { display: grid; gap: .75rem; border: 1px solid var(--ui-border); border-radius: .65rem; padding: .85rem; }
    .data-table { width: 100%; text-align: left; font-size: .875rem; }
    .media-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 1rem; }
    .media-card { overflow: hidden; border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface-muted); }
    .media-card img { display: block; width: 100%; height: 150px; object-fit: cover; background: var(--ui-border); }
    .media-card > div { padding: .75rem; }
    .data-table th { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; font-size: .7rem; text-transform: uppercase; color: var(--ui-text-muted); }
    .data-table td { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; color: var(--ui-text); vertical-align: top; }
    .actions { white-space: normal; } .actions button + button { margin-left: .35rem; }
    .empty { padding: 2rem; text-align: center; color: var(--ui-text-muted); } .pager { justify-content: space-between; margin-top: .75rem; font-size: .8rem; color: var(--ui-text-muted); }
    .preview { margin-top: .75rem; border-top: 1px solid var(--ui-border); padding-top: .75rem; }
    .dialog-backdrop-shield { position: fixed; inset: 0; z-index: 999; background: rgb(15 23 42 / .68); backdrop-filter: blur(4px); }
    .dialog-form { position: fixed; inset: 50% auto auto 50%; z-index: 1000; width: min(68rem, calc(100vw - 2rem)); max-height: calc(100vh - 2rem); translate: -50% -50%; overflow: auto; border-radius: 1rem; padding: 1.25rem; box-shadow: 0 24px 70px rgb(15 23 42 / .28); }
    .dialog-form > h3 { border-bottom: 1px solid var(--ui-border); padding-bottom: .9rem; font-size: 1.15rem; font-weight: 700; }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } .wide { grid-column: auto; } .inline-filter input, .inline-filter select { min-width: 0; width: 100%; } .dialog-form { width: calc(100vw - 1rem); max-height: calc(100vh - 1rem); } }
  `]
})
export class SpeedReadingCommunicationsComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService); private readonly authService = inject(AuthService); private readonly toaster = inject(ToasterService);
  private readonly route = inject(ActivatedRoute, { optional: true });
  readonly tabs: { value: CommunicationTab; label: string }[] = [
    { value: 'cms', label: 'CMS' }, { value: 'announcements', label: 'Duyurular' },
    { value: 'email-templates', label: 'E-posta şablonları' }, { value: 'campaigns', label: 'Kampanyalar' }, { value: 'notifications', label: 'Bildirimler' }
  ];
  readonly canManageContent = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingContentManage));
  readonly canManageCommunications = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingCommunicationsManage));
  readonly visibleTabs = computed(() => this.tabs.filter(tab => tab.value === 'cms' ? this.canManageContent() : this.canManageCommunications()));
  readonly cmsTabs: { value: CmsTab; label: string }[] = [
    { value: 'homepage', label: 'Ana sayfa' }, { value: 'evidence', label: 'Kanıtlı istatistikler' }, { value: 'pages', label: 'Sayfalar' }, { value: 'blog', label: 'Blog' }, { value: 'media', label: 'Medya' }, { value: 'navigation', label: 'Menü' }, { value: 'contacts', label: 'İletişim' }, { value: 'subscribers', label: 'Bülten aboneleri' }
  ];
  readonly selectedTab = signal<CommunicationTab>('cms');
  readonly cmsTab = signal<CmsTab>('homepage');
  readonly loading = signal(false); readonly saving = signal(false); readonly error = signal('');
  readonly evidenceMetrics = signal<EvidenceMetric[]>([]);
  readonly pages = signal<{ items: SpeedReadingCmsPage[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly blogPosts = signal<{ items: SpeedReadingCmsBlogPost[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly contacts = signal<{ items: SpeedReadingCmsContactMessage[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly subscribers = signal<{ items: SpeedReadingCmsNewsletterSubscriber[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly mediaAssets = signal<{ items: SpeedReadingCmsMediaAsset[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 30 });
  readonly navigationItems = signal<SpeedReadingCmsNavigationItem[]>([]);
  readonly cmsPreview = signal<{ title: string; content: string } | null>(null);
  readonly cmsRevisions = signal<SpeedReadingCmsRevision[]>([]);
  readonly cmsRevisionType = signal<'Page' | 'Blog'>('Page');
  readonly announcements = signal<SpeedReadingAnnouncement[]>([]); readonly announcementStats = signal<SpeedReadingAnnouncementStats | null>(null);
  readonly emailTemplates = signal<SpeedReadingEmailTemplate[]>([]); readonly emailPreview = signal<{ subject: string; body: string } | null>(null);
  readonly campaigns = signal<SpeedReadingEmailCampaign[]>([]); readonly campaignStats = signal<SpeedReadingEmailCampaignStats | null>(null);
  readonly notifications = signal<SpeedReadingAdminNotificationPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 }); readonly unreadContacts = signal(0);

  cmsPageNumber = 1; cmsBlogPageNumber = 1; cmsMediaPageNumber = 1; notificationPage = 1; notificationSearch = '';
  navigationMenu = 'Main';
  includeInactiveSubscribers = false;
  mediaFile: File | null = null; mediaAltText = '';
  contactReadFilter = ''; contactReplyFilter = ''; announcementActiveFilter = ''; announcementIncludeExpired = false;
  cmsPageEditingId: string | null = null; cmsBlogEditingId: string | null = null;
  evidenceEditingId: string | null = null;
  navigationEditingId: string | null = null;
  announcementEditingId: string | null = null; emailTemplateEditingId: string | null = null; campaignEditingId: string | null = null;
  pageDraft: SpeedReadingCmsPageRequest = this.emptyPage(); blogDraft: SpeedReadingCmsBlogPostRequest = this.emptyBlog();
  homePageDraft: HomePageDraft = { ...DEFAULT_HOME_PAGE_DRAFT }; homePageCards: HomeCardsDraft = { ...DEFAULT_HOME_PAGE_CARDS };
  evidenceDraft: EvidenceMetricDraft = this.emptyEvidenceMetric();
  navigationDraft: SpeedReadingCmsNavigationItemRequest = this.emptyNavigation();
  blogTags = '';
  announcementDraft: SpeedReadingAnnouncementRequest = this.emptyAnnouncement(); announcementTargetRoles = '';
  emailTemplateDraft: SpeedReadingEmailTemplateRequest = this.emptyEmailTemplate(); campaignDraft: SpeedReadingEmailCampaignRequest = this.emptyCampaign();
  bulkDraft: SpeedReadingBulkNotificationRequest = { targetType: 'All', targetRole: null, title: '', message: '', type: 9, priority: 2, actionUrl: null, sendEmail: false };
  readonly selectedContact = signal<SpeedReadingCmsContactMessage | null>(null); contactReplyDraft = '';
  readonly evidenceEditing = signal(false);
  readonly homePageEditing = signal(false);
  readonly evidenceIcons = evidenceMetricIcons;

  ngOnInit(): void {
    const requestedTab = this.route?.snapshot.queryParamMap.get('tab');
    const requestedTabIsVisible = this.visibleTabs().some(tab => tab.value === requestedTab);
    if (requestedTabIsVisible) this.selectedTab.set(requestedTab as CommunicationTab);
    else if (!this.canManageContent() && this.canManageCommunications()) this.selectedTab.set('announcements');
    this.loadSelectedTab();
  }

  @HostListener('document:keydown.escape')
  closeDialogOnEscape(): void {
    if (this.selectedContact()) this.selectedContact.set(null);
    else if (this.cmsPreview()) this.cmsPreview.set(null);
    else if (this.cmsRevisions().length) this.cmsRevisions.set([]);
    else if (this.announcementStats()) this.announcementStats.set(null);
    else if (this.emailPreview()) this.emailPreview.set(null);
    else if (this.campaignStats()) this.campaignStats.set(null);
    else if (this.homePageEditing()) this.homePageEditing.set(false);
    else if (this.cmsPageEditing() || this.cmsBlogEditing() || this.mediaEditing() || this.navigationEditing()) this.cancelCmsEdit();
    else if (this.evidenceEditing()) this.cancelEvidenceEdit();
    else if (this.announcementEditing()) this.announcementEditing.set(false);
    else if (this.emailTemplateEditing()) this.emailTemplateEditing.set(false);
    else if (this.campaignEditing()) this.campaignEditing.set(false);
    else if (this.bulkEditing()) this.bulkEditing.set(false);
  }

  selectTab(tab: CommunicationTab): void { this.closeAllDialogs(); this.selectedTab.set(tab); this.error.set(''); this.loadSelectedTab(); }
  private loadSelectedTab(): void {
    switch (this.selectedTab()) {
      case 'cms': if (this.canManageContent()) this.loadHomePage(); break;
      case 'announcements': if (this.canManageCommunications()) this.loadAnnouncements(); break;
      case 'email-templates': if (this.canManageCommunications()) this.loadEmailTemplates(); break;
      case 'campaigns': if (this.canManageCommunications()) this.loadCampaigns(); break;
      case 'notifications': if (this.canManageCommunications()) this.loadNotifications(); break;
    }
  }
  selectCmsTab(tab: CmsTab): void { if (this.loading()) return; this.closeAllDialogs(); this.cmsTab.set(tab); this.loadCmsTab(); }
  startCmsCreate(): void { if (this.loading() || this.saving()) return; if (this.cmsTab() === 'homepage') { this.editHomePage(); } else if (this.cmsTab() === 'evidence') { this.evidenceEditingId = null; this.evidenceDraft = this.emptyEvidenceMetric(); this.evidenceEditing.set(true); } else if (this.cmsTab() === 'pages') { this.cmsPageEditingId = null; this.pageDraft = this.emptyPage(); this.cmsPageEditing.set(true); } else if (this.cmsTab() === 'blog') { this.cmsBlogEditingId = null; this.blogDraft = this.emptyBlog(); this.blogTags = ''; this.cmsBlogEditing.set(true); } else if (this.cmsTab() === 'media') { this.mediaFile = null; this.mediaAltText = ''; this.mediaEditing.set(true); } else if (this.cmsTab() === 'navigation') { this.navigationEditingId = null; this.navigationDraft = this.emptyNavigation(); this.navigationEditing.set(true); } else { this.loadCmsTab(); } }
  private loadCmsTab(): void { if (this.cmsTab() === 'homepage') this.loadHomePage(); if (this.cmsTab() === 'evidence') this.loadEvidenceMetrics(); if (this.cmsTab() === 'pages') this.loadPages(); if (this.cmsTab() === 'blog') this.loadBlogPosts(); if (this.cmsTab() === 'media') this.loadMedia(); if (this.cmsTab() === 'navigation') this.loadNavigation(); if (this.cmsTab() === 'contacts') this.loadContacts(); if (this.cmsTab() === 'subscribers') this.loadSubscribers(); }
  loadHomePage(onLoaded?: () => void): void { this.loading.set(true); this.service.getCmsBlocks('HomePage').pipe(finalize(() => this.loading.set(false))).subscribe({ next: blocks => { const content = hydrateHomePageDraft(blocks); this.homePageDraft = content.draft; this.homePageCards = content.cards; onLoaded?.(); }, error: () => this.error.set('Ana sayfa içeriği yüklenemedi.') }); }
  editHomePage(): void { this.loadHomePage(() => this.homePageEditing.set(true)); }
  saveHomePage(): void { const config = buildHomePageConfig(this.homePageDraft, this.homePageCards); this.run(this.service.updateCmsLanding({ group: 'HomePage', blocks: { home_page_config: JSON.stringify(config) } }), () => { this.homePageEditing.set(false); this.loadHomePage(); }, 'Ana sayfa içeriği kaydedilemedi.'); }
  addHomeTrustPoint(): void { this.homePageCards = { ...this.homePageCards, trustPoints: [...this.homePageCards.trustPoints, ''] }; }
  removeHomeTrustPoint(index: number): void { this.homePageCards = { ...this.homePageCards, trustPoints: this.homePageCards.trustPoints.filter((_, itemIndex) => itemIndex !== index) }; }
  addHomeFeature(): void { this.homePageCards = { ...this.homePageCards, features: [...this.homePageCards.features, { icon: 'insights', title: '', description: '' }] }; }
  removeHomeFeature(index: number): void { this.homePageCards = { ...this.homePageCards, features: this.homePageCards.features.filter((_, itemIndex) => itemIndex !== index) }; }
  addHomeApproach(): void { this.homePageCards = { ...this.homePageCards, approach: [...this.homePageCards.approach, { title: '', role: '', description: '' }] }; }
  removeHomeApproach(index: number): void { this.homePageCards = { ...this.homePageCards, approach: this.homePageCards.approach.filter((_, itemIndex) => itemIndex !== index) }; }
  addHomeBenefit(): void { this.homePageCards = { ...this.homePageCards, benefits: [...this.homePageCards.benefits, ''] }; }
  removeHomeBenefit(index: number): void { this.homePageCards = { ...this.homePageCards, benefits: this.homePageCards.benefits.filter((_, itemIndex) => itemIndex !== index) }; }
  addHomeFaq(): void { this.homePageCards = { ...this.homePageCards, faqItems: [...this.homePageCards.faqItems, { question: '', answer: '', category: '' }] }; }
  removeHomeFaq(index: number): void { this.homePageCards = { ...this.homePageCards, faqItems: this.homePageCards.faqItems.filter((_, itemIndex) => itemIndex !== index) }; }
  addHomeTestimonial(): void { this.homePageCards = { ...this.homePageCards, testimonials: [...this.homePageCards.testimonials, { name: '', role: '', rating: 0, text: '' }] }; }
  removeHomeTestimonial(index: number): void { this.homePageCards = { ...this.homePageCards, testimonials: this.homePageCards.testimonials.filter((_, itemIndex) => itemIndex !== index) }; }
  readonly cmsPageEditing = signal(false); readonly cmsBlogEditing = signal(false); readonly mediaEditing = signal(false); readonly navigationEditing = signal(false); readonly announcementEditing = signal(false); readonly emailTemplateEditing = signal(false); readonly campaignEditing = signal(false); readonly bulkEditing = signal(false);

  loadEvidenceMetrics(): void {
    this.service.getCmsBlocks(evidenceMetricsGroup).subscribe({
      next: blocks => this.evidenceMetrics.set(blocks.map(block => this.toEvidenceMetric(block)).filter((metric): metric is EvidenceMetric => metric !== null)),
      error: () => this.error.set('Kanıtlı istatistikler yüklenemedi.')
    });
  }

  editEvidenceMetric(metric: EvidenceMetric): void {
    this.evidenceEditingId = metric.id;
    this.evidenceDraft = { title: metric.title, value: metric.value, description: metric.description, source: metric.source, period: metric.period, sampleSize: metric.sampleSize, icon: metric.icon, isVisible: metric.isVisible, verified: metric.verified };
    this.evidenceEditing.set(true);
  }

  cancelEvidenceEdit(): void {
    this.evidenceEditingId = null;
    this.evidenceDraft = this.emptyEvidenceMetric();
    this.evidenceEditing.set(false);
  }

  saveEvidenceMetric(): void {
    const draft = this.evidenceDraft;
    if (!draft.title.trim() || !draft.value.trim() || !draft.source.trim() || !draft.period.trim() || !draft.sampleSize || !Number.isInteger(draft.sampleSize) || draft.sampleSize < 1) {
      this.error.set('Başlık, değer, kaynak, dönem ve en az 1 kişilik örneklem zorunludur.');
      return;
    }
    if (draft.isVisible && /\d/.test(draft.value) && !draft.verified) {
      this.error.set('Rakam içeren istatistikleri yayımlamak için önce kaynak doğrulamasını işaretleyin.');
      return;
    }

    const existing = this.evidenceMetrics().find(metric => metric.id === this.evidenceEditingId);
    const request: SpeedReadingCmsContentBlockRequest = {
      key: existing?.key ?? `evidence-${Date.now()}`,
      group: evidenceMetricsGroup,
      label: draft.title.trim(),
      type: 0,
      value: JSON.stringify({ title: draft.title.trim(), value: draft.value.trim(), description: draft.description.trim(), source: draft.source.trim(), period: draft.period.trim(), sampleSize: draft.sampleSize, icon: draft.icon, isVisible: draft.isVisible, verified: draft.verified })
    };
    const action = this.evidenceEditingId
      ? this.service.updateCmsBlock(this.evidenceEditingId, request)
      : this.service.createCmsBlock(request);
    this.run(action, () => { this.cancelEvidenceEdit(); this.loadEvidenceMetrics(); }, 'Kanıtlı istatistik kaydedilemedi.');
  }

  async deleteEvidenceMetric(metric: EvidenceMetric): Promise<void> {
    if (!await this.toaster.confirm(`“${metric.title}” kaydını silmek istiyor musunuz?`, { title: 'Kanıtlı istatistiği sil' })) return;
    this.run(this.service.deleteCmsBlock(metric.id), () => this.loadEvidenceMetrics(), 'Kanıtlı istatistik silinemedi.');
  }

  loadMedia(): void { this.service.getCmsMedia(this.cmsMediaPageNumber).subscribe({ next: value => this.mediaAssets.set(value), error: () => this.error.set('CMS medyası yüklenemedi.') }); }
  selectMediaFile(event: Event): void { this.mediaFile = (event.target as HTMLInputElement).files?.[0] ?? null; }
  uploadMedia(): void { if (!this.mediaFile) return; this.run(this.service.uploadCmsMedia(this.mediaFile, this.mediaAltText), () => { this.mediaFile = null; this.mediaAltText = ''; this.mediaEditing.set(false); this.loadMedia(); }, 'Medya yüklenemedi.'); }
  async deleteMedia(media: SpeedReadingCmsMediaAsset): Promise<void> { if (!await this.toaster.confirm('Bu medya silinsin mi?', { title: 'Medyayı sil' })) return; this.run(this.service.deleteCmsMedia(media.id), () => this.loadMedia(), 'Medya silinemedi.'); }
  useMediaForBlog(media: SpeedReadingCmsMediaAsset): void { this.blogDraft = { ...this.blogDraft, coverImageUrl: media.url }; this.cmsTab.set('blog'); this.cmsBlogEditing.set(true); }
  changeMediaPage(delta: number): void { this.cmsMediaPageNumber = Math.max(1, this.cmsMediaPageNumber + delta); this.loadMedia(); }

  loadNavigation(): void { this.service.getCmsNavigation(this.navigationMenu || 'Main').subscribe({ next: value => this.navigationItems.set(value), error: () => this.error.set('CMS menüsü yüklenemedi.') }); }
  editNavigation(item: SpeedReadingCmsNavigationItem): void { this.navigationEditingId = item.id; this.navigationDraft = { menu: item.menu, label: item.label, url: item.url, fragment: item.fragment, icon: item.icon, sortOrder: item.sortOrder, isVisible: item.isVisible, openInNewTab: item.openInNewTab }; this.navigationEditing.set(true); }
  saveNavigation(): void { const action = this.navigationEditingId ? this.service.updateCmsNavigationItem(this.navigationEditingId, this.navigationDraft) : this.service.createCmsNavigationItem(this.navigationDraft); this.run(action, () => { this.cancelCmsEdit(); this.loadNavigation(); }, 'CMS menü öğesi kaydedilemedi.'); }
  async deleteNavigation(item: SpeedReadingCmsNavigationItem): Promise<void> { if (!await this.toaster.confirm('Bu menü öğesi silinsin mi?', { title: 'Menü öğesini sil' })) return; this.run(this.service.deleteCmsNavigationItem(item.id), () => this.loadNavigation(), 'CMS menü öğesi silinemedi.'); }

  loadPages(): void { this.service.getCmsPages(this.cmsPageNumber).subscribe({ next: value => this.pages.set(value), error: () => this.error.set('CMS sayfaları yüklenemedi.') }); }
  editPage(page: SpeedReadingCmsPage): void { this.cmsPageEditingId = page.id; this.cmsPageEditing.set(true); this.pageDraft = { title: page.title, slug: page.slug, content: page.content, isPublished: page.isPublished, scheduledPublishAt: page.scheduledPublishAt?.slice(0, 16) ?? null, seoSettings: { ...page.seoSettings } }; }
  saveCmsPage(): void { const action = this.cmsPageEditingId ? this.service.updateCmsPage(this.cmsPageEditingId, this.pageDraft) : this.service.createCmsPage(this.pageDraft); this.run(action, () => { this.cancelCmsEdit(); this.loadPages(); }, 'CMS sayfası kaydedilemedi.'); }
  async deletePage(page: SpeedReadingCmsPage): Promise<void> { if (!await this.toaster.confirm('Bu CMS sayfası silinsin mi?', { title: 'CMS sayfasını sil' })) return; this.run(this.service.deleteCmsPage(page.id), () => this.loadPages(), 'CMS sayfası silinemedi.'); }
  previewPage(page: SpeedReadingCmsPage): void { this.service.previewCmsPage(page.id).subscribe({ next: value => this.cmsPreview.set({ title: value.title, content: value.content }), error: () => this.error.set('Sayfa önizlemesi yüklenemedi.') }); }

  loadBlogPosts(): void { this.service.getCmsBlogPosts(this.cmsBlogPageNumber).subscribe({ next: value => this.blogPosts.set(value), error: () => this.error.set('Blog yazıları yüklenemedi.') }); }
  editBlogPost(post: SpeedReadingCmsBlogPost): void { this.cmsBlogEditingId = post.id; this.cmsBlogEditing.set(true); this.blogDraft = { title: post.title, slug: post.slug, summary: post.summary, content: post.content, author: post.author, publishedAt: post.publishedAt?.slice(0, 16) ?? null, scheduledPublishAt: post.scheduledPublishAt?.slice(0, 16) ?? null, tags: post.tags, coverImageUrl: post.coverImageUrl, isPublished: post.isPublished, seoSettings: { ...post.seoSettings } }; this.blogTags = post.tags.join(', '); }
  saveCmsBlogPost(): void { const request = { ...this.blogDraft, tags: this.blogTags.split(',').map(tag => tag.trim()).filter(Boolean) }; const action = this.cmsBlogEditingId ? this.service.updateCmsBlogPost(this.cmsBlogEditingId, request) : this.service.createCmsBlogPost(request); this.run(action, () => { this.cancelCmsEdit(); this.loadBlogPosts(); }, 'Blog yazısı kaydedilemedi.'); }
  async deleteBlogPost(post: SpeedReadingCmsBlogPost): Promise<void> { if (!await this.toaster.confirm('Bu blog yazısı silinsin mi?', { title: 'Blog yazısını sil' })) return; this.run(this.service.deleteCmsBlogPost(post.id), () => this.loadBlogPosts(), 'Blog yazısı silinemedi.'); }
  previewBlogPost(post: SpeedReadingCmsBlogPost): void { this.service.previewCmsBlogPost(post.id).subscribe({ next: value => this.cmsPreview.set({ title: value.title, content: value.content }), error: () => this.error.set('Blog önizlemesi yüklenemedi.') }); }
  showRevisions(entityType: 'Page' | 'Blog', entityId: string): void { this.cmsRevisionType.set(entityType); this.service.getCmsRevisions(entityType, entityId).subscribe({ next: value => this.cmsRevisions.set(value), error: () => this.error.set('İçerik geçmişi yüklenemedi.') }); }
  async restoreRevision(revision: SpeedReadingCmsRevision): Promise<void> { if (!await this.toaster.confirm(`Sürüm ${revision.version} geri yüklensin mi? Mevcut içerik de geçmişe kaydedilecek.`, { title: 'İçerik sürümünü geri yükle' })) return; const entityType = this.cmsRevisionType(); this.run(this.service.restoreCmsRevision(entityType, revision.entityId, revision.id), () => { this.cmsRevisions.set([]); entityType === 'Page' ? this.loadPages() : this.loadBlogPosts(); }, 'İçerik sürümü geri yüklenemedi.'); }

  loadContacts(): void { const isRead = this.contactReadFilter === '' ? undefined : this.contactReadFilter === 'true'; const isReplied = this.contactReplyFilter === '' ? undefined : this.contactReplyFilter === 'true'; this.service.getCmsContactMessages(1, 25, isRead, isReplied).subscribe({ next: value => this.contacts.set(value), error: () => this.error.set('İletişim mesajları yüklenemedi.') }); this.service.getCmsUnreadContactMessageCount().subscribe({ next: value => this.unreadContacts.set(value) }); }
  markContactRead(message: SpeedReadingCmsContactMessage): void { this.run(this.service.markCmsContactMessageRead(message.id, !message.isRead), () => this.loadContacts(), 'Mesaj durumu güncellenemedi.'); }
  openContactDialog(message: SpeedReadingCmsContactMessage): void { this.selectedContact.set(message); this.contactReplyDraft = message.replyContent ?? ''; }
  replyContact(): void { const message = this.selectedContact(); const reply = this.contactReplyDraft.trim(); if (!message || !reply) return; this.run(this.service.replyToCmsContactMessage({ messageId: message.id, replyContent: reply }), () => { this.selectedContact.set(null); this.loadContacts(); }, 'Mesaj yanıtlanamadı.'); }
  async deleteContact(message: SpeedReadingCmsContactMessage): Promise<void> { if (!await this.toaster.confirm('Bu iletişim mesajı silinsin mi?', { title: 'İletişim mesajını sil' })) return; this.run(this.service.deleteCmsContactMessage(message.id), () => this.loadContacts(), 'Mesaj silinemedi.'); }
  loadSubscribers(): void { this.service.getCmsSubscribers(1, 25, this.includeInactiveSubscribers).subscribe({ next: value => this.subscribers.set(value), error: () => this.error.set('Bülten aboneleri yüklenemedi.') }); }
  async deleteSubscriber(subscriber: SpeedReadingCmsNewsletterSubscriber): Promise<void> { if (!await this.toaster.confirm('Bu bülten aboneliği silinsin mi?', { title: 'Bülten aboneliğini sil' })) return; this.run(this.service.deleteCmsSubscriber(subscriber.id), () => this.loadSubscribers(), 'Abone silinemedi.'); }
  async hardDeleteSubscriber(subscriber: SpeedReadingCmsNewsletterSubscriber): Promise<void> { if (!await this.toaster.confirm('Bu aboneyi kalıcı olarak silmek istediğinizden emin misiniz?', { title: 'Aboneyi kalıcı sil' })) return; this.run(this.service.deleteCmsSubscriber(subscriber.id, true), () => this.loadSubscribers(), 'Abone kalıcı olarak silinemedi.'); }
  restoreSubscriber(subscriber: SpeedReadingCmsNewsletterSubscriber): void { this.run(this.service.restoreCmsSubscriber(subscriber.id), () => this.loadSubscribers(), 'Abone yeniden aktifleştirilemedi.'); }
  exportSubscribers(): void { this.service.exportCmsSubscribers(this.includeInactiveSubscribers).subscribe({ next: blob => { const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'hizli-okuma-bulten-aboneleri.csv'; anchor.click(); URL.revokeObjectURL(url); }, error: () => this.error.set('Aboneler dışa aktarılamadı.') }); }

  loadAnnouncements(): void { const isActive = this.announcementActiveFilter === '' ? undefined : this.announcementActiveFilter === 'true'; this.service.getAnnouncements({ isActive, includeExpired: this.announcementIncludeExpired, take: 100 }).subscribe({ next: value => this.announcements.set(value), error: () => this.error.set('Duyurular yüklenemedi.') }); }
  editAnnouncement(item: SpeedReadingAnnouncement): void { this.announcementEditingId = item.id; this.announcementEditing.set(true); this.announcementDraft = { title: item.title, content: item.content, plainTextContent: item.plainTextContent, priority: item.priority, targetAudience: item.targetAudience, targetInstitutionId: item.targetInstitutionId, targetRoles: item.targetRoles, isPinned: item.isPinned, startDate: item.startDate?.slice(0, 16) ?? null, expiresAt: item.expiresAt?.slice(0, 16) ?? null, displayType: item.displayType, icon: item.icon, colorTheme: item.colorTheme, actionUrl: item.actionUrl, actionText: item.actionText, sendEmailNotification: false, createInAppNotification: false }; this.announcementTargetRoles = item.targetRoles.join(', '); }
  saveAnnouncement(): void { const draft = { ...this.announcementDraft, targetRoles: this.announcementTargetRoles.split(',').map(role => role.trim()).filter(Boolean) }; const action = this.announcementEditingId ? this.service.updateAnnouncement(this.announcementEditingId, { ...draft, isActive: true }) : this.service.createAnnouncement(draft); this.run(action, () => { this.announcementEditing.set(false); this.loadAnnouncements(); }, 'Duyuru kaydedilemedi.'); }
  async deleteAnnouncement(item: SpeedReadingAnnouncement): Promise<void> { if (!await this.toaster.confirm('Bu duyuru silinsin mi?', { title: 'Duyuruyu sil' })) return; this.run(this.service.deleteAnnouncement(item.id), () => this.loadAnnouncements(), 'Duyuru silinemedi.'); }
  showAnnouncementStats(item: SpeedReadingAnnouncement): void { this.service.getAnnouncementStats(item.id).subscribe({ next: value => this.announcementStats.set(value), error: () => this.error.set('Duyuru istatistiği yüklenemedi.') }); }

  loadEmailTemplates(): void { this.service.getSpeedReadingEmailTemplates().subscribe({ next: value => this.emailTemplates.set(value), error: () => this.error.set('E-posta şablonları yüklenemedi.') }); }
  editEmailTemplate(item: SpeedReadingEmailTemplate): void { this.emailTemplateEditingId = item.id; this.emailTemplateEditing.set(true); this.emailTemplateDraft = { name: item.name, code: item.code, subject: item.subject, body: item.body, description: item.description, availableVariables: item.availableVariables, isActive: item.isActive }; }
  saveEmailTemplate(): void { const action = this.emailTemplateEditingId ? this.service.updateSpeedReadingEmailTemplate(this.emailTemplateEditingId, this.emailTemplateDraft) : this.service.createSpeedReadingEmailTemplate(this.emailTemplateDraft); this.run(action, () => { this.emailTemplateEditing.set(false); this.loadEmailTemplates(); }, 'E-posta şablonu kaydedilemedi.'); }
  async deleteEmailTemplate(item: SpeedReadingEmailTemplate): Promise<void> { if (!await this.toaster.confirm('Bu e-posta şablonu silinsin mi?', { title: 'E-posta şablonunu sil' })) return; this.run(this.service.deleteSpeedReadingEmailTemplate(item.id), () => this.loadEmailTemplates(), 'E-posta şablonu silinemedi.'); }
  previewEmailTemplate(item: SpeedReadingEmailTemplate): void { this.service.previewSpeedReadingEmailTemplate(item.id).subscribe({ next: value => this.emailPreview.set(value), error: () => this.error.set('E-posta önizlemesi yüklenemedi.') }); }

  loadCampaigns(): void { this.service.getSpeedReadingEmailCampaigns().subscribe({ next: value => this.campaigns.set(value), error: () => this.error.set('E-posta kampanyaları yüklenemedi.') }); }
  editCampaign(item: SpeedReadingEmailCampaign): void { this.service.getSpeedReadingEmailCampaign(item.id).subscribe({ next: detail => { this.campaignEditingId = item.id; this.campaignEditing.set(true); this.campaignDraft = { name: detail.campaign.name, subject: detail.campaign.subject, body: detail.body, plainTextBody: detail.plainTextBody, targetRoles: detail.campaign.targetRoles, targetInstitutionId: detail.campaign.targetInstitutionId, includeAllUsers: detail.campaign.includeAllUsers, includeSubscribers: detail.campaign.includeSubscribers, scheduledFor: null }; }, error: () => this.error.set('Kampanya ayrıntısı yüklenemedi.') }); }
  saveCampaign(): void { const request = { ...this.campaignDraft, scheduledFor: null }; const action = this.campaignEditingId ? this.service.updateSpeedReadingEmailCampaign(this.campaignEditingId, request) : this.service.createSpeedReadingEmailCampaign(request); this.run(action, () => { this.campaignEditing.set(false); this.loadCampaigns(); }, 'Kampanya taslağı kaydedilemedi.'); }
  async deleteCampaign(item: SpeedReadingEmailCampaign): Promise<void> { if (!await this.toaster.confirm('Bu kampanya silinsin mi?', { title: 'Kampanyayı sil' })) return; this.run(this.service.deleteSpeedReadingEmailCampaign(item.id), () => this.loadCampaigns(), 'Kampanya silinemedi.'); }
  showCampaignStats(item: SpeedReadingEmailCampaign): void { this.service.getSpeedReadingEmailCampaignStats(item.id).subscribe({ next: value => this.campaignStats.set(value), error: () => this.error.set('Kampanya istatistiği yüklenemedi.') }); }

  loadNotifications(): void { this.service.getSpeedReadingNotifications(this.notificationPage, 25, { searchTerm: this.notificationSearch }).subscribe({ next: value => this.notifications.set(value), error: () => this.error.set('Bildirimler yüklenemedi.') }); }
  sendBulkNotification(): void { this.saving.set(true); this.error.set(''); this.service.sendSpeedReadingBulkNotification(this.bulkDraft).pipe(finalize(() => this.saving.set(false))).subscribe({ next: (result: SpeedReadingBulkNotificationResult) => { this.bulkDraft = { ...this.bulkDraft, title: '', message: '' }; this.bulkEditing.set(false); this.loadNotifications(); if (result.errors.length > 0) this.error.set(result.errors.join(' ')); }, error: () => this.error.set('Toplu bildirim gönderilemedi.') }); }
  changeNotificationPage(delta: number): void { this.notificationPage = Math.max(1, this.notificationPage + delta); this.loadNotifications(); }
  notificationTotalPages(): number { return Math.max(1, Math.ceil(this.notifications().totalCount / this.notifications().pageSize)); }
  changePage(kind: 'pages' | 'blog', delta: number): void { if (kind === 'pages') { this.cmsPageNumber = Math.max(1, this.cmsPageNumber + delta); this.loadPages(); } else { this.cmsBlogPageNumber = Math.max(1, this.cmsBlogPageNumber + delta); this.loadBlogPosts(); } }
  cmsTotalPages(page: { totalCount: number; pageSize: number }): number { return Math.max(1, Math.ceil(page.totalCount / page.pageSize)); }
  nowIso(): string { return new Date().toISOString(); }

  cancelCmsEdit(): void { this.cmsPageEditingId = null; this.cmsBlogEditingId = null; this.navigationEditingId = null; this.cmsPageEditing.set(false); this.cmsBlogEditing.set(false); this.mediaEditing.set(false); this.navigationEditing.set(false); this.homePageEditing.set(false); }
  private closeAllDialogs(): void { this.cancelCmsEdit(); this.cancelEvidenceEdit(); this.announcementEditing.set(false); this.emailTemplateEditing.set(false); this.campaignEditing.set(false); this.bulkEditing.set(false); this.selectedContact.set(null); this.cmsPreview.set(null); this.cmsRevisions.set([]); this.announcementStats.set(null); this.emailPreview.set(null); this.campaignStats.set(null); }
  private run(request: Observable<unknown>, onSuccess: () => void, errorMessage: string): void { this.saving.set(true); this.error.set(''); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: onSuccess, error: err => this.error.set(getAdminErrorMessage(err, errorMessage)) }); }
  private emptySeo(): SpeedReadingCmsSeoSettings { return { metaTitle: null, metaDescription: null, metaKeywords: null, canonicalUrl: null, ogTitle: null, ogDescription: null, ogImage: null, noIndex: false }; }
  private emptyEvidenceMetric(): EvidenceMetricDraft { return { title: '', value: '', description: '', source: '', period: '', sampleSize: null, icon: 'insights', isVisible: false, verified: false }; }
  private toEvidenceMetric(block: SpeedReadingCmsContentBlock): EvidenceMetric | null {
    try {
      const value = JSON.parse(block.value) as Partial<EvidenceMetricDraft>;
      if (!block.label?.trim() || typeof value.value !== 'string' || typeof value.source !== 'string' || typeof value.period !== 'string' || typeof value.sampleSize !== 'number' || !Number.isInteger(value.sampleSize) || value.sampleSize < 1) return null;
      return {
        id: block.id,
        key: block.key,
        title: block.label,
        value: value.value,
        description: typeof value.description === 'string' ? value.description : '',
        source: value.source,
        period: value.period,
        sampleSize: value.sampleSize,
        icon: evidenceMetricIcons.includes(value.icon ?? '') ? value.icon! : 'insights',
        isVisible: value.isVisible === true,
        verified: value.verified === true
      };
    } catch {
      return null;
    }
  }
  private emptyPage(): SpeedReadingCmsPageRequest { return { title: '', slug: '', content: '', isPublished: false, scheduledPublishAt: null, seoSettings: this.emptySeo() }; }
  private emptyBlog(): SpeedReadingCmsBlogPostRequest { return { title: '', slug: '', summary: null, content: '', author: null, publishedAt: null, scheduledPublishAt: null, tags: [], coverImageUrl: null, isPublished: false, seoSettings: this.emptySeo() }; }
  private emptyNavigation(): SpeedReadingCmsNavigationItemRequest { return { menu: 'Main', label: '', url: '/', fragment: null, icon: null, sortOrder: 0, isVisible: true, openInNewTab: false }; }
  emptyAnnouncement(): SpeedReadingAnnouncementRequest { return { title: '', content: '', plainTextContent: null, priority: 2, targetAudience: 0, targetInstitutionId: null, targetRoles: [], isPinned: false, startDate: null, expiresAt: null, displayType: 0, icon: null, colorTheme: null, actionUrl: null, actionText: null, sendEmailNotification: false, createInAppNotification: true }; }
  emptyEmailTemplate(): SpeedReadingEmailTemplateRequest { return { name: '', code: '', subject: '', body: '', description: null, availableVariables: null, isActive: true }; }
  emptyCampaign(): SpeedReadingEmailCampaignRequest { return { name: '', subject: '', body: '', plainTextBody: null, targetRoles: null, targetInstitutionId: null, includeAllUsers: false, includeSubscribers: false, scheduledFor: null }; }
}
