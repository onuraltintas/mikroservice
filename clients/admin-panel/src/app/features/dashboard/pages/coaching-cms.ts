import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  CoachingCmsEntry,
  CoachingCmsEntryRequest,
  CoachingCmsKind,
  CoachingCmsMediaAsset,
  CoachingCmsNavigationItem,
  CoachingCmsNavigationRequest,
  CoachingCmsRevision,
  CoachingNewsletterSubscriber,
  CoachingManagementPage,
  CoachingManagementService
} from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../../coaching-cms/coaching-cms-content';
import { ToasterService } from '../../../core/services/toaster.service';

type CmsTab = 'home' | 'faq' | 'testimonials' | 'entries' | 'navigation' | 'media' | 'newsletter';

@Component({
  selector: 'app-coaching-cms',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="coaching-cms-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Koçluk servisi</p>
        <h1 id="coaching-cms-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Koçluk içerik yönetimi</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Koçluk ana sayfa bloklarını, yayın sayfalarını, blog yazılarını ve menüleri yalnız Koçluk platformu için yönetin.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="Koçluk CMS sekmeleri">
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'home'" (click)="selectTab('home')">Ana sayfa</button>
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'faq'" (click)="selectTab('faq')">SSS</button>
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'testimonials'" (click)="selectTab('testimonials')">Yorumlar</button>
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'entries'" (click)="selectTab('entries')">Sayfalar ve blog</button>
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'navigation'" (click)="selectTab('navigation')">Menüler</button>
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'media'" (click)="selectTab('media')">Görsel kütüphanesi</button>
        <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === 'newsletter'" (click)="selectTab('newsletter')">Bülten aboneleri</button>
      </nav>

      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }

      @if (tab() === 'home') {
        <section class="space-y-4" aria-labelledby="home-cms-heading">
          <header><h2 id="home-cms-heading" class="text-lg font-semibold">Koçluk ana sayfası</h2><p class="muted">Bölüm başlıklarını, metinleri, görselleri ve bağlantıları; tekrar eden alan kartlarını ve süreç adımlarını buradan yönetin.</p></header>
          <label class="group-picker">Düzenlenecek bölüm<select [(ngModel)]="selectedHomeGroup" name="homeGroup" (ngModelChange)="loadHomeGroup($event)">@for (section of homeSections; track section.group) {<option [value]="section.group">{{ section.label }}</option>}</select></label>
          <div class="flex flex-wrap items-center justify-between gap-3"><div><h3 class="font-semibold">{{ homeGroupConfig().label }}</h3><p class="muted">{{ homeGroupConfig().description }}</p></div><button type="button" class="primary" (click)="startHomeCreate()" [disabled]="homeGroupConfig().single && homeEntries().length > 0">{{ homeGroupConfig().single ? 'Bölüm oluştur' : 'Yeni içerik ekle' }}</button></div>
          @if (homeEditing()) {
            <form class="form-card space-y-3" (ngSubmit)="saveHomeEntry()">
              <h3 class="font-semibold">{{ homeEditingId ? 'Bölümü düzenle' : 'Bölüm oluştur' }}</h3>
              <div class="form-grid">
                <label>Üst etiket<input [(ngModel)]="homeDraft.eyebrow" name="homeEyebrow" maxlength="120" /></label>
                <label>Başlık<input [(ngModel)]="homeDraft.title" name="homeTitle" maxlength="200" required /></label>
                <label class="wide">Kısa açıklama<textarea [(ngModel)]="homeDraft.summary" name="homeSummary" maxlength="500" rows="2"></textarea></label>
                <label class="wide">İçerik<textarea [(ngModel)]="homeDraft.content" name="homeContent" maxlength="200000" rows="6"></textarea></label>
                <label>Görsel adresi<input [(ngModel)]="homeDraft.imageUrl" name="homeImageUrl" maxlength="500" placeholder="Koçluk medya kütüphanesinden seçin" /></label>
                <label>Medya kütüphanesinden seç<select [ngModel]="homeDraft.imageUrl" (ngModelChange)="homeDraft.imageUrl = $event || null" name="homeImageAsset"><option value="">Görsel seçilmedi</option>@for (asset of media().items; track asset.id) {<option [value]="asset.url">{{ asset.altText || asset.fileName }}</option>}</select></label>
                <label>Birincil bağlantı etiketi<input [(ngModel)]="homeDraft.linkLabel" name="homeLinkLabel" maxlength="150" /></label>
                <label>Birincil bağlantı adresi<input [(ngModel)]="homeDraft.linkUrl" name="homeLinkUrl" maxlength="500" placeholder="/coaching/pages/nasil-calisir" /></label>
                <label>İkincil bağlantı etiketi<input [(ngModel)]="homeDraft.secondaryLinkLabel" name="homeSecondaryLinkLabel" maxlength="150" /></label>
                <label>İkincil bağlantı adresi<input [(ngModel)]="homeDraft.secondaryLinkUrl" name="homeSecondaryLinkUrl" maxlength="500" /></label>
                <label>SEO başlığı<input [(ngModel)]="homeDraft.seoTitle" name="homeSeoTitle" maxlength="200" /></label>
                <label>SEO açıklaması<input [(ngModel)]="homeDraft.seoDescription" name="homeSeoDescription" maxlength="500" /></label>
                <label>Sıralama<input type="number" [(ngModel)]="homeDraft.sortOrder" name="homeOrder" min="0" max="10000" /></label>
                <label>Planlı yayın zamanı<input type="datetime-local" [(ngModel)]="homeScheduledLocal" name="homeScheduledAt" /></label>
                <label class="check"><input type="checkbox" [(ngModel)]="homeDraft.isPublished" name="homePublished" /> Yayında</label>
              </div>
              <div class="form-actions"><button type="button" class="secondary" (click)="showPreview(homeDraft)">Önizle</button><button type="button" class="secondary" (click)="cancelHomeEdit()">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
            </form>
          }
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>İçerik</th><th>Yayın</th><th></th></tr></thead><tbody>@for (entry of homeEntries(); track entry.id) {<tr><td><strong>{{ entry.title }}</strong><div class="muted">{{ entry.eyebrow }} · {{ entry.summary }}</div></td><td>{{ entry.isPublished ? 'Yayında' : 'Taslak' }}</td><td class="actions"><button type="button" (click)="startHomeEdit(entry)">Düzenle</button><button type="button" (click)="showPreview(toRequest(entry))">Önizle</button><button type="button" (click)="showRevisions(entry)">Sürümler</button><button type="button" (click)="deleteHomeEntry(entry)">Sil</button></td></tr>} @empty {<tr><td colspan="3" class="empty">Bu bölümde içerik yok.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ homePage().totalCount }}</span><button type="button" (click)="changeHomePage(-1)" [disabled]="homePage().pageNumber <= 1">Önceki</button><button type="button" (click)="changeHomePage(1)" [disabled]="homePage().pageNumber >= homeTotalPages()">Sonraki</button></div></div>
        </section>
      } @else if (tab() === 'faq' || tab() === 'testimonials') {
        <section class="space-y-4" [attr.aria-labelledby]="tab() === 'faq' ? 'faq-cms-heading' : 'testimonials-cms-heading'">
          <div class="flex flex-wrap items-center justify-between gap-3"><div><h2 [id]="tab() === 'faq' ? 'faq-cms-heading' : 'testimonials-cms-heading'" class="text-lg font-semibold">{{ tab() === 'faq' ? 'Sıkça sorulan sorular' : 'Koçluk kullanıcı yorumları' }}</h2><p class="muted">{{ tab() === 'faq' ? 'Her soru ve yanıtı ayrı kaydedin, sıralayın ve yayın durumunu belirleyin.' : 'Yalnızca gerçek ve yayın izni alınmış kullanıcı geri bildirimlerini ekleyin.' }}</p></div><button type="button" class="primary" (click)="startSpecialCreate()">{{ tab() === 'faq' ? 'Soru ekle' : 'Yorum ekle' }}</button></div>
          @if (specialEditing()) {
            <form class="form-card space-y-3" (ngSubmit)="saveSpecialEntry()"><h3 class="font-semibold">{{ specialEditingId ? 'Kaydı düzenle' : (tab() === 'faq' ? 'Yeni soru' : 'Yeni kullanıcı yorumu') }}</h3><div class="form-grid">
              @if (tab() === 'faq') { <label class="wide">Soru<input [(ngModel)]="specialDraft.title" name="faqQuestion" maxlength="200" required /></label><label class="wide">Yanıt<textarea [(ngModel)]="specialDraft.content" name="faqAnswer" maxlength="200000" rows="6" required></textarea></label> }
              @else { <label>Kullanıcı adı<input [(ngModel)]="specialDraft.title" name="testimonialName" maxlength="200" required /></label><label>Rol / kısa tanım<input [(ngModel)]="specialDraft.summary" name="testimonialRole" maxlength="500" placeholder="Öğrenci, veli veya öğretmen" /></label><label class="wide">Yorum<textarea [(ngModel)]="specialDraft.content" name="testimonialQuote" maxlength="4000" rows="5" required></textarea></label><label>Fotoğraf adresi<input [(ngModel)]="specialDraft.imageUrl" name="testimonialImageUrl" maxlength="500" /></label><label>Medya kütüphanesinden seç<select [ngModel]="specialDraft.imageUrl" (ngModelChange)="specialDraft.imageUrl = $event || null" name="testimonialImageAsset"><option value="">Fotoğraf seçilmedi</option>@for (asset of media().items; track asset.id) {<option [value]="asset.url">{{ asset.altText || asset.fileName }}</option>}</select></label><label class="check wide"><input type="checkbox" [(ngModel)]="specialDraft.testimonialConsentConfirmed" name="testimonialConsent" /> Bu yorumun Koçluk sitesinde yayımlanması için kullanıcıdan açık izin alındı.</label> }
              <label>Sıralama<input type="number" [(ngModel)]="specialDraft.sortOrder" name="specialOrder" min="0" max="10000" /></label><label>Planlı yayın zamanı<input type="datetime-local" [(ngModel)]="specialScheduledLocal" name="specialScheduledAt" /></label><label class="check"><input type="checkbox" [(ngModel)]="specialDraft.isPublished" name="specialPublished" /> Yayında</label>
            </div><div class="form-actions"><button type="button" class="secondary" (click)="showPreview(specialDraft)">Önizle</button><button type="button" class="secondary" (click)="cancelSpecialEdit()">İptal</button><button type="submit" class="primary" [disabled]="!canSaveSpecialEntry()">Kaydet</button></div></form>
          }
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>{{ tab() === 'faq' ? 'Soru' : 'Kullanıcı' }}</th><th>{{ tab() === 'faq' ? 'Yanıt / özet' : 'Rol / yorum' }}</th><th>Durum</th><th></th></tr></thead><tbody>@for (entry of specialEntries(); track entry.id) {<tr><td><strong>{{ entry.title }}</strong></td><td>{{ entry.summary || entry.content }}</td><td>{{ entry.isPublished ? 'Yayında' : 'Taslak' }}</td><td class="actions"><button type="button" (click)="startSpecialEdit(entry)">Düzenle</button><button type="button" (click)="showPreview(toRequest(entry))">Önizle</button><button type="button" (click)="deleteSpecialEntry(entry)">Sil</button></td></tr>} @empty {<tr><td colspan="4" class="empty">{{ tab() === 'faq' ? 'Henüz soru eklenmedi.' : 'Henüz yayın izni alınmış bir kullanıcı yorumu eklenmedi.' }}</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ specialPage().totalCount }}</span><button type="button" (click)="changeSpecialPage(-1)" [disabled]="specialPage().pageNumber <= 1">Önceki</button><button type="button" (click)="changeSpecialPage(1)" [disabled]="specialPage().pageNumber >= specialTotalPages()">Sonraki</button></div></div>
        </section>
      }

      @if (tab() === 'entries') {
        <section class="space-y-4" aria-labelledby="entries-heading">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div><h2 id="entries-heading" class="text-lg font-semibold">İçerik kayıtları</h2><p class="muted">Yayın tarihi gelecekte olan içerik, tarihi geldiğinde otomatik görünür.</p></div>
            <button type="button" class="primary" (click)="startCreate()">Yeni içerik</button>
          </div>
          <div class="flex flex-wrap gap-2">
            @for (kindOption of kinds; track kindOption.value) {
              <button type="button" class="secondary" [attr.aria-pressed]="kind() === kindOption.value" (click)="selectKind(kindOption.value)">{{ kindOption.label }}</button>
            }
            <form class="ml-auto flex gap-2" (ngSubmit)="loadEntries()"><input [(ngModel)]="search" name="search" maxlength="100" placeholder="Başlık veya adres ara" /><button class="secondary" type="submit">Ara</button></form>
          </div>

          @if (editing()) {
            <form class="form-card space-y-3" (ngSubmit)="saveEntry()">
              <h3 class="font-semibold">{{ editingId ? 'İçeriği düzenle' : 'Yeni içerik' }}</h3>
              <div class="form-grid">
                <label>Tür<select [(ngModel)]="draft.kind" name="entryKind" (ngModelChange)="onKindChanged()"><option value="Page">Sayfa</option><option value="Blog">Blog yazısı</option><option value="Block">Ana sayfa bloğu</option></select></label>
                <label>Başlık<input [(ngModel)]="draft.title" name="entryTitle" maxlength="200" required /></label>
                <label>Adres / anahtar<input [(ngModel)]="draft.slug" name="entrySlug" maxlength="160" placeholder="Boş bırakılırsa başlıktan üretilir" /></label>
                @if (draft.kind === 'Block') { <label>Grup<input [(ngModel)]="draft.group" name="entryGroup" maxlength="80" required placeholder="HomePage" /></label> }
                <label class="wide">Özet<textarea [(ngModel)]="draft.summary" name="entrySummary" maxlength="500" rows="2"></textarea></label>
                <label class="wide">İçerik<textarea [(ngModel)]="draft.content" name="entryContent" maxlength="200000" rows="8" required></textarea><span class="muted">Görsel eklemek için Görsel Kütüphanesi’ndeki “İçeriğe ekleme kodu”nu yeni bir satıra yapıştırın.</span></label>
                @if (draft.kind === 'Blog') {
                  <label>Yazar<input [(ngModel)]="draft.author" name="entryAuthor" maxlength="150" /></label>
                  <label>Yayın tarihi<input type="datetime-local" [(ngModel)]="publishedAtLocal" name="entryPublishedAt" /></label>
                  <label>Kapak görseli adresi<input [(ngModel)]="draft.coverImageUrl" name="entryCoverImage" maxlength="500" placeholder="Koçluk medya kütüphanesinden seçin" /></label>
                  <label>Medya kütüphanesinden seç<select [ngModel]="draft.coverImageUrl" (ngModelChange)="draft.coverImageUrl = $event || null" name="entryCoverAsset"><option value="">Kapak görseli seçilmedi</option>@for (asset of media().items; track asset.id) {<option [value]="asset.url">{{ asset.altText || asset.fileName }}</option>}</select></label>
                }
                <aside class="wide content-guide" aria-label="İçerik biçimlendirme rehberi"><strong>İçerik biçimlendirme rehberi</strong><p>Güvenli ve düzenli görünüm için biçimleri kullanın. HTML kodları çalıştırılmaz, metin olarak gösterilir.</p><p><code>## Bölüm başlığı</code> bölüm başlığı · boş satır yeni paragraf · <code>- Madde</code> liste öğesi. Görseli, kütüphanedeki “İçeriğe ekleme kodu” düğmesiyle ekleyin.</p></aside>
                <label>SEO başlığı<input [(ngModel)]="draft.seoTitle" name="seoTitle" maxlength="200" /></label>
                <label>SEO açıklaması<input [(ngModel)]="draft.seoDescription" name="seoDescription" maxlength="500" /></label>
                <label>Etiketler (virgülle)<input [ngModel]="draft.tags.join(', ')" (ngModelChange)="setTags($event)" name="entryTags" maxlength="500" /></label>
                <label>Sıralama<input type="number" [(ngModel)]="draft.sortOrder" name="entryOrder" min="0" max="10000" /></label>
                <label>Planlı yayın zamanı<input type="datetime-local" [ngModel]="scheduledLocal" (ngModelChange)="scheduledLocal = $event" name="scheduledPublishAt" /></label>
                <label class="check"><input type="checkbox" [(ngModel)]="draft.isPublished" name="entryPublished" /> Yayında</label>
              </div>
              <div class="form-actions"><button type="button" class="secondary" (click)="showPreview(draft)">Önizle</button><button type="button" class="secondary" (click)="cancelEdit()">İptal</button><button type="submit" class="primary" [disabled]="saving()">{{ saving() ? 'Kaydediliyor…' : 'Kaydet' }}</button></div>
            </form>
          }

          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Başlık</th><th>Adres / grup</th><th>Durum</th><th>Son güncelleme</th><th></th></tr></thead><tbody>
            @for (entry of entries(); track entry.id) {
              <tr><td><strong>{{ entry.title }}</strong><div class="muted">{{ entry.summary || entry.content.slice(0, 120) }}</div></td><td class="font-mono">{{ entry.slug }}<div class="muted">{{ entry.group || entry.kind }}</div></td><td>{{ entry.isPublished ? 'Yayında' : 'Taslak' }}<div class="muted">{{ entry.scheduledPublishAt ? (entry.scheduledPublishAt | date:'dd.MM.yyyy HH:mm') : '' }}</div></td><td>{{ (entry.updatedAt || entry.createdAt) | date:'dd.MM.yyyy HH:mm' }}</td><td class="actions"><button type="button" (click)="startEdit(entry)">Düzenle</button><button type="button" (click)="showRevisions(entry)">Sürümler ({{ entry.version }})</button><button type="button" (click)="deleteEntry(entry)">Sil</button></td></tr>
            } @empty { <tr><td colspan="5" class="empty">Bu türde içerik yok.</td></tr> }
          </tbody></table>
            <div class="pager"><span>Toplam {{ totalCount() }}</span><button type="button" (click)="changePage(-1)" [disabled]="pageNumber() <= 1">Önceki</button><button type="button" (click)="changePage(1)" [disabled]="pageNumber() >= totalPages()">Sonraki</button></div>
          </div>

          @if (revisionEntry()) {
            <section class="data-card space-y-3" aria-label="İçerik sürüm geçmişi"><div class="flex items-center justify-between"><h3 class="font-semibold">{{ revisionEntry()?.title }} — sürüm geçmişi</h3><button type="button" class="secondary" (click)="revisionEntry.set(null)">Kapat</button></div>
              @for (revision of revisions(); track revision.id) { <div class="flex items-center justify-between gap-3 border-t py-2"><span>Sürüm {{ revision.version }} · {{ revision.createdAt | date:'dd.MM.yyyy HH:mm' }}</span><button type="button" class="secondary" (click)="restoreRevision(revision)">Geri yükle</button></div> }
              @if (revisions().length === 0) { <p class="muted">Henüz geri yüklenebilir bir sürüm yok.</p> }
            </section>
          }
        </section>
      } @else if (tab() === 'navigation') {
        <section class="space-y-4" aria-labelledby="navigation-heading">
          <div class="flex items-center justify-between"><div><h2 id="navigation-heading" class="text-lg font-semibold">Koçluk menüleri</h2><p class="muted">Menü bağlantıları yalnız Koçluk CMS kayıtlarında saklanır.</p></div><button type="button" class="primary" (click)="startNavigationCreate()">Menü öğesi ekle</button></div>
          <label class="group-picker">Düzenlenecek menü<select [(ngModel)]="selectedMenuValue" name="selectedMenu" (ngModelChange)="onMenuChanged($event)"><option value="Main">Üst menü</option><option value="Footer">Alt menü</option></select></label>
          @if (navigationEditing()) {
            <form class="form-card" (ngSubmit)="saveNavigation()"><h3 class="font-semibold">{{ navigationEditingId ? 'Menü öğesini düzenle' : 'Yeni menü öğesi' }}</h3><div class="form-grid">
              <label>Menü<select [(ngModel)]="navigationDraft.menu" name="menuName"><option value="Main">Üst menü</option><option value="Footer">Alt menü</option></select></label><label>Etiket<input [(ngModel)]="navigationDraft.label" name="menuLabel" maxlength="150" required /></label><label class="wide">Bağlantı<input [(ngModel)]="navigationDraft.url" name="menuUrl" maxlength="500" required placeholder="/coaching/pages/nasil-calisir veya https://…" /></label><label>İkon anahtarı<input [(ngModel)]="navigationDraft.icon" name="menuIcon" maxlength="80" /></label><label>Sıra<input type="number" [(ngModel)]="navigationDraft.sortOrder" name="menuOrder" min="0" max="10000" /></label><label class="check"><input type="checkbox" [(ngModel)]="navigationDraft.isVisible" name="menuVisible" /> Görünür</label><label class="check"><input type="checkbox" [(ngModel)]="navigationDraft.openInNewTab" name="menuNewTab" /> Yeni sekmede aç</label>
            </div><div class="form-actions"><button type="button" class="secondary" (click)="cancelNavigationEdit()">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>
          }
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Etiket</th><th>Menü</th><th>Bağlantı</th><th>Durum</th><th></th></tr></thead><tbody>@for (item of navigation(); track item.id) {<tr><td>{{ item.label }}</td><td>{{ item.menu }}</td><td class="font-mono">{{ item.url }}</td><td>{{ item.isVisible ? 'Görünür' : 'Gizli' }}</td><td class="actions"><button type="button" (click)="editNavigation(item)">Düzenle</button><button type="button" (click)="deleteNavigation(item)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Menü öğesi yok.</td></tr>}</tbody></table></div>
        </section>
      } @else if (tab() === 'media') {
        <section class="space-y-4" aria-labelledby="media-heading">
          <div><h2 id="media-heading" class="text-lg font-semibold">Koçluk görsel kütüphanesi</h2><p class="muted">Görseller Koçluk servisinin VPS üzerindeki ayrı kalıcı dosya alanında tutulur. PNG, JPEG, WebP ve GIF; en fazla 10 MB.</p></div>
          <form class="form-card" (ngSubmit)="uploadMedia()"><div class="form-grid"><label class="wide">Görsel dosyası<input type="file" accept="image/png,image/jpeg,image/webp,image/gif" (change)="selectMediaFile($event)" required /></label><label class="wide">Alternatif metin<input [(ngModel)]="mediaAltText" name="mediaAltText" maxlength="300" placeholder="Ekran okuyucular için kısa açıklama" /></label></div><div class="form-actions"><button type="submit" class="primary" [disabled]="saving() || !selectedMediaFile">{{ saving() ? 'Yükleniyor…' : 'Görseli yükle' }}</button></div></form>
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Önizleme</th><th>Dosya</th><th>Adres</th><th>Tarih</th><th></th></tr></thead><tbody>@for (asset of media().items; track asset.id) {<tr><td><img [src]="asset.url" [alt]="asset.altText || asset.fileName" class="h-16 w-24 rounded object-cover" /></td><td><strong>{{ asset.fileName }}</strong><div class="muted">{{ asset.contentType }} · {{ asset.sizeBytes / 1024 | number:'1.0-0' }} KB</div><div class="muted">{{ asset.altText }}</div></td><td class="font-mono"><a [href]="asset.url" target="_blank" rel="noopener noreferrer">{{ asset.url }}</a></td><td>{{ asset.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td class="actions"><button type="button" (click)="copyMediaEmbed(asset)">İçeriğe ekleme kodu</button><button type="button" (click)="deleteMedia(asset)">Sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Henüz Koçluk görseli yüklenmedi.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ media().totalCount }}</span><button type="button" (click)="changeMediaPage(-1)" [disabled]="media().pageNumber <= 1">Önceki</button><button type="button" (click)="changeMediaPage(1)" [disabled]="media().pageNumber >= mediaPageCount()">Sonraki</button></div></div>
        </section>
      } @else if (tab() === 'newsletter') {
        <section class="space-y-4" aria-labelledby="newsletter-heading">
          <div><h2 id="newsletter-heading" class="text-lg font-semibold">Koçluk bülteni aboneleri</h2><p class="muted">Yalnız Koçluk sitesinden açık onay vererek kayıt olan adresler. Abonelik durumu ve onay zamanları Koçluk veritabanında tutulur.</p></div>
          <form class="form-card" (ngSubmit)="loadNewsletter(1)"><div class="form-grid"><label>E-posta ara<input [(ngModel)]="newsletterSearch" name="newsletterSearch" maxlength="100" placeholder="ornek@eposta.com" /></label><label>Abonelik durumu<select [(ngModel)]="newsletterStatus" name="newsletterStatus"><option value="">Tüm durumlar</option><option value="PendingConfirmation">Onay bekliyor</option><option value="Active">Onaylandı</option><option value="Unsubscribed">İptal edildi</option></select></label></div><div class="form-actions"><button type="submit" class="secondary">Filtrele</button><button type="button" class="primary" (click)="exportNewsletter()" [disabled]="saving()">CSV indir</button></div></form>
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>E-posta</th><th>Durum</th><th>Onay ve izin kaydı</th><th>Kayıt tarihi</th><th>İşlemler</th></tr></thead><tbody>@for (subscriber of newsletterPage().items; track subscriber.id) {<tr><td><strong>{{ subscriber.email }}</strong><div class="muted">Kaynak: {{ subscriber.source }}</div></td><td>{{ newsletterStatusLabel(subscriber.status) }}</td><td><div>Onay metni: {{ subscriber.consentTextVersion }}</div><div class="muted">İzin: {{ subscriber.consentedAt | date:'dd.MM.yyyy HH:mm' }}</div><div class="muted">Onay zamanı: {{ subscriber.confirmedAt ? (subscriber.confirmedAt | date:'dd.MM.yyyy HH:mm') : 'Henüz onaylanmadı' }}</div></td><td>{{ subscriber.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td class="actions">@if (subscriber.status !== 'Unsubscribed') {<button type="button" (click)="unsubscribeNewsletter(subscriber)">Aboneliği iptal et</button>}<button type="button" (click)="deleteNewsletterSubscriber(subscriber)">Kalıcı sil</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Bu filtrelerle eşleşen bülten abonesi yok.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ newsletterPage().totalCount }}</span><button type="button" (click)="changeNewsletterPage(-1)" [disabled]="newsletterPage().pageNumber <= 1">Önceki</button><button type="button" (click)="changeNewsletterPage(1)" [disabled]="newsletterPage().pageNumber >= newsletterPageCount()">Sonraki</button></div></div>
        </section>
      }
      @if (previewEntry(); as preview) {
        <div class="dialog-backdrop-shield">
          <section class="preview-dialog" role="dialog" aria-modal="true" aria-label="Koçluk içerik önizlemesi">
            <header class="flex items-center justify-between gap-3"><div><p class="muted">Kaydedilmemiş taslak · public görünüm önizlemesi</p><h2 class="text-lg font-semibold">{{ preview.title || 'Başlıksız içerik' }}</h2></div><button type="button" class="secondary" (click)="previewEntry.set(null)">Kapat</button></header>
            <article class="preview-body">
              @if (preview.eyebrow) { <p class="muted">{{ preview.eyebrow }}</p> }
              @if (isCoachingMediaUrl(preview.coverImageUrl || preview.imageUrl)) { <img [src]="preview.coverImageUrl || preview.imageUrl" [alt]="preview.title || ''" class="preview-image" /> }
              <h1>{{ preview.title || 'Başlıksız içerik' }}</h1>
              @if (preview.author || preview.publishedAt) { <p class="muted">{{ preview.author || '' }} {{ preview.publishedAt ? ('· ' + (preview.publishedAt | date:'dd.MM.yyyy')) : '' }}</p> }
              @if (preview.summary) { <p class="preview-summary">{{ preview.summary }}</p> }
              <div class="cms-article">@for (segment of contentSegments(preview.content); track $index) { @if (segment.type === 'heading') { <h2>{{ segment.value }}</h2> } @else if (segment.type === 'list') { <ul>@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> } @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" class="preview-image" /> } @else { <p class="whitespace-pre-wrap">{{ segment.value }}</p> } }</div>
              @if (preview.linkLabel && preview.linkUrl) { <a class="preview-link" [href]="preview.linkUrl">{{ preview.linkLabel }}</a> }
              @if (preview.secondaryLinkLabel && preview.secondaryLinkUrl) { <a class="preview-link secondary-link" [href]="preview.secondaryLinkUrl">{{ preview.secondaryLinkLabel }}</a> }
            </article>
          </section>
        </div>
      }
      @if (loading()) { <div role="status" class="text-center text-sm text-gray-500">Yükleniyor…</div> }
    </main>
  `,
  styles: [`
    .data-card, .form-card { border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface); padding: 1rem; }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; margin-top: .75rem; }
    .form-grid label { display: flex; flex-direction: column; gap: .25rem; font-size: .875rem; }
    .form-grid .wide { grid-column: 1 / -1; }
    .form-grid .check { flex-direction: row; align-items: center; padding-top: 1.5rem; }
    .content-guide { border: 1px solid var(--ui-border); border-radius: .65rem; padding: .85rem; background: color-mix(in srgb, var(--ui-surface) 86%, #6366f1); font-size: .8rem; }
    .content-guide p { margin-top: .3rem; color: var(--ui-text-muted, #64748b); line-height: 1.55; }
    .content-guide code { border-radius: .25rem; background: rgba(15, 23, 42, .08); padding: .1rem .25rem; }
    input, select, textarea { border: 1px solid var(--ui-border-strong); border-radius: .5rem; background: transparent; padding: .5rem .75rem; color: inherit; }
    .form-actions, .pager { display: flex; justify-content: flex-end; align-items: center; gap: .5rem; margin-top: 1rem; }
    .primary, .secondary, .actions button, .pager button { border-radius: .5rem; padding: .5rem .75rem; font-size: .875rem; }
    .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); }
    .secondary, .actions button, .pager button { border: 1px solid var(--ui-border-strong); }
    .data-table { width: 100%; text-align: left; font-size: .875rem; }
    .data-table th, .data-table td { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; vertical-align: top; }
    .actions { white-space: nowrap; } .actions button + button { margin-left: .25rem; }
    .muted { color: var(--ui-text-muted); font-size: .8rem; } .empty { padding: 2rem; text-align: center; color: var(--ui-text-muted); }
    .pager { justify-content: space-between; }
    .group-picker { display: flex; max-width: 28rem; flex-direction: column; gap: .25rem; font-size: .875rem; }
    .dialog-backdrop-shield { position: fixed; inset: 0; z-index: 100; display: grid; place-items: center; overflow-y: auto; padding: 1rem; background: rgba(15, 23, 42, .6); }
    .preview-dialog { width: min(100%, 56rem); max-height: min(90vh, 60rem); overflow-y: auto; border: 1px solid var(--ui-border); border-radius: 1rem; background: var(--ui-surface); padding: 1.25rem; box-shadow: 0 24px 60px rgba(15, 23, 42, .28); }
    .preview-body { max-width: 46rem; margin: 1.5rem auto .5rem; line-height: 1.7; }
    .preview-body h1 { margin: .5rem 0 1rem; font-size: 2rem; font-weight: 700; line-height: 1.15; }
    .preview-body h2 { margin: 1.5rem 0 .5rem; font-size: 1.35rem; font-weight: 650; }
    .preview-body ul { margin: .75rem 0; list-style: disc; padding-left: 1.5rem; }
    .preview-summary { margin: .75rem 0 1.5rem; color: var(--ui-text-muted); font-size: 1.1rem; }
    .preview-image { display: block; max-height: 24rem; max-width: 100%; margin: 1rem auto; border-radius: .75rem; object-fit: cover; }
    .preview-link { display: inline-flex; margin: 1rem .5rem 0 0; border-radius: .5rem; background: var(--ui-brand); padding: .55rem .8rem; color: var(--ui-brand-contrast); font-weight: 600; }
    .preview-link.secondary-link { border: 1px solid var(--ui-border-strong); background: transparent; color: inherit; }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } .form-grid .wide { grid-column: auto; } }
  `]
})
export class CoachingCmsComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly toaster = inject(ToasterService);
  readonly contentSegments = parseCoachingCmsContent;

  readonly homeSections = [
    { group: 'HomeHero', label: 'Kapak alanı', description: 'Ana başlık, açıklama ve ilk çağrı bağlantıları.', single: true },
    { group: 'HomeAreaHeading', label: 'Kullanıcı alanları başlığı', description: 'Öğrenci, öğretmen ve kurum kartlarının bölüm başlığı.', single: true },
    { group: 'HomePage', label: 'Öğrenci / öğretmen / kurum kartları', description: 'Ana sayfada gösterilen rol kartları.', single: false },
    { group: 'HomeProcessHeading', label: 'Süreç bölümü başlığı', description: 'Koçluk süreci adımlarının bölüm başlığı.', single: true },
    { group: 'HomeHowItWorks', label: 'Koçluk süreç adımları', description: 'Ana sayfada gösterilen süreç adımları.', single: false },
    { group: 'HomePlansHeading', label: 'Planlar bölümü başlığı', description: 'Abonelik planlarının üzerinde gösterilen açıklama.', single: true },
    { group: 'HomeBlogHeading', label: 'Blog bölümü başlığı', description: 'Ana sayfadaki son yazılar bölümünün başlığı.', single: true },
    { group: 'HomeClosingCta', label: 'Son çağrı alanı', description: 'Ana sayfanın altındaki giriş/kayıt çağrısı.', single: true },
    { group: 'HomeFaqHeading', label: 'SSS bölümü başlığı', description: 'Ana sayfa SSS alanının başlığı.', single: true },
    { group: 'HomeTestimonialsHeading', label: 'Yorumlar bölümü başlığı', description: 'Yayın izni alınmış yorumların başlığı.', single: true },
    { group: 'BlogLanding', label: 'Blog liste sayfası', description: 'Blog arşiv sayfasının başlık ve açıklaması.', single: true },
    { group: 'HomeBranding', label: 'Site markası ve alt bilgi', description: 'Koçluk CMS alanının marka alt başlığı ve footer açıklaması.', single: true }
  ] as const;

  readonly kinds: ReadonlyArray<{ value: CoachingCmsKind; label: string }> = [
    { value: 'Page', label: 'Sayfalar' }, { value: 'Blog', label: 'Blog yazıları' }, { value: 'Block', label: 'Ana sayfa blokları' }
  ];
  readonly tab = signal<CmsTab>('home');
  readonly kind = signal<CoachingCmsKind>('Page');
  readonly entries = signal<CoachingCmsEntry[]>([]);
  readonly homePage = signal<CoachingManagementPage<CoachingCmsEntry>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly specialPage = signal<CoachingManagementPage<CoachingCmsEntry>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly homeEntries = computed(() => this.homePage().items);
  readonly specialEntries = computed(() => this.specialPage().items);
  readonly navigation = signal<CoachingCmsNavigationItem[]>([]);
  readonly media = signal<CoachingManagementPage<CoachingCmsMediaAsset>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly newsletterPage = signal<CoachingManagementPage<CoachingNewsletterSubscriber>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly revisions = signal<CoachingCmsRevision[]>([]);
  readonly revisionEntry = signal<CoachingCmsEntry | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly editing = signal(false);
  readonly homeEditing = signal(false);
  readonly specialEditing = signal(false);
  readonly navigationEditing = signal(false);
  readonly previewEntry = signal<CoachingCmsEntryRequest | null>(null);
  readonly totalCount = signal(0);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly error = signal<string | null>(null);
  editingId: string | null = null;
  homeEditingId: string | null = null;
  specialEditingId: string | null = null;
  navigationEditingId: string | null = null;
  selectedMediaFile: File | null = null;
  mediaAltText = '';
  newsletterSearch = '';
  newsletterStatus = '';
  search = '';
  scheduledLocal = '';
  publishedAtLocal = '';
  homeScheduledLocal = '';
  specialScheduledLocal = '';
  selectedHomeGroup = 'HomeHero';
  selectedMenuValue: 'Main' | 'Footer' = 'Main';
  draft = this.emptyDraft();
  homeDraft = this.emptyDraft('Block');
  specialDraft = this.emptyDraft('Block');
  navigationDraft = this.emptyNavigationDraft();

  ngOnInit() { this.loadHomeGroup('HomeHero'); }

  selectTab(value: CmsTab) {
    this.tab.set(value); this.error.set(null);
    if (value === 'home') this.loadHomeGroup(this.selectedHomeGroup);
    else if (value === 'faq' || value === 'testimonials') {
      this.loadSpecialEntries(1);
      this.loadMediaChoices();
    }
    else if (value === 'entries') this.loadEntries();
    else if (value === 'navigation') this.loadNavigation();
    else if (value === 'media') this.loadMedia();
    else this.loadNewsletter(1);
  }
  selectKind(value: CoachingCmsKind) { this.kind.set(value); this.pageNumber.set(1); this.cancelEdit(); this.loadEntries(); }

  loadEntries() {
    this.loading.set(true); this.error.set(null);
    this.service.getCmsEntries(this.kind(), this.pageNumber(), 25, this.search).subscribe({
      next: page => { this.entries.set(page.items); this.totalCount.set(page.totalCount); this.totalPages.set(Math.max(1, Math.ceil(page.totalCount / page.pageSize))); this.loading.set(false); },
      error: () => { this.entries.set([]); this.error.set('Koçluk içerikleri yüklenemedi.'); this.loading.set(false); }
    });
  }

  loadHomeGroup(group: string) {
    this.selectedHomeGroup = group;
    this.homeEditing.set(false); this.homeEditingId = null;
    this.loading.set(true); this.error.set(null);
    this.service.getCmsEntries('Block', 1, 25, '', group).subscribe({
      next: page => { this.homePage.set(page); this.loading.set(false); },
      error: () => { this.homePage.set({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 }); this.error.set('Koçluk ana sayfa içeriği yüklenemedi.'); this.loading.set(false); }
    });
    this.loadMediaChoices();
  }

  loadSpecialEntries(pageNumber = this.specialPage().pageNumber) {
    const group = this.tab() === 'faq' ? 'HomeFaq' : 'HomeTestimonials';
    this.loading.set(true); this.error.set(null);
    this.service.getCmsEntries('Block', pageNumber, 25, '', group).subscribe({
      next: page => { this.specialPage.set(page); this.loading.set(false); },
      error: () => { this.specialPage.set({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 }); this.error.set('Koçluk içeriği yüklenemedi.'); this.loading.set(false); }
    });
  }

  loadMediaChoices() {
    this.service.getCmsMediaAssets(1, 100).subscribe({ next: page => this.media.set(page) });
  }

  homeGroupConfig() { return this.homeSections.find(section => section.group === this.selectedHomeGroup) ?? this.homeSections[0]; }
  homeTotalPages() { return Math.max(1, Math.ceil(this.homePage().totalCount / this.homePage().pageSize)); }
  specialTotalPages() { return Math.max(1, Math.ceil(this.specialPage().totalCount / this.specialPage().pageSize)); }
  changeHomePage(delta: number) {
    const pageNumber = Math.max(1, Math.min(this.homeTotalPages(), this.homePage().pageNumber + delta));
    if (pageNumber !== this.homePage().pageNumber) this.loadHomeGroupPage(pageNumber);
  }
  private loadHomeGroupPage(pageNumber: number) {
    this.loading.set(true);
    this.service.getCmsEntries('Block', pageNumber, 25, '', this.selectedHomeGroup).subscribe({
      next: page => { this.homePage.set(page); this.loading.set(false); },
      error: () => { this.error.set('Koçluk ana sayfa içeriği yüklenemedi.'); this.loading.set(false); }
    });
  }
  changeSpecialPage(delta: number) {
    const pageNumber = Math.max(1, Math.min(this.specialTotalPages(), this.specialPage().pageNumber + delta));
    if (pageNumber !== this.specialPage().pageNumber) this.loadSpecialEntries(pageNumber);
  }

  onMenuChanged(value: string) {
    if (value !== 'Main' && value !== 'Footer') return;
    this.selectedMenuValue = value;
    this.loadNavigation(value);
  }

  loadNavigation(menu: 'Main' | 'Footer' = this.selectedMenuValue) {
    this.loading.set(true); this.error.set(null);
    this.service.getNavigation(menu, true).subscribe({
      next: items => { this.navigation.set(items); this.loading.set(false); },
      error: () => { this.navigation.set([]); this.error.set('Koçluk menüleri yüklenemedi.'); this.loading.set(false); }
    });
  }

  loadMedia() {
    this.loading.set(true); this.error.set(null);
    this.service.getCmsMediaAssets(this.media().pageNumber, 25).subscribe({
      next: page => { this.media.set(page); this.loading.set(false); },
      error: () => { this.error.set('Koçluk görsel kütüphanesi yüklenemedi.'); this.loading.set(false); }
    });
  }

  selectMediaFile(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedMediaFile = input.files?.[0] ?? null;
    if (this.selectedMediaFile && this.selectedMediaFile.size > 10 * 1024 * 1024) {
      this.selectedMediaFile = null;
      input.value = '';
      this.error.set('Görsel 10 MB sınırını aşamaz.');
    }
  }

  async uploadMedia() {
    const file = this.selectedMediaFile;
    if (!file) { this.error.set('Yüklenecek bir görsel seçin.'); return; }
    this.saving.set(true); this.error.set(null);
    try {
      await firstValueFrom(this.service.uploadCmsMedia(file, this.mediaAltText));
      this.toaster.success('Görsel Koçluk CMS kütüphanesine yüklendi.');
      this.selectedMediaFile = null; this.mediaAltText = '';
      this.media.update(page => ({ ...page, pageNumber: 1 }));
      this.loadMedia();
    } catch (error) { this.error.set(this.apiError(error, 'Görsel yüklenemedi. PNG, JPEG, WebP veya GIF ve en fazla 10 MB olmalı.')); }
    finally { this.saving.set(false); }
  }

  async copyMediaEmbed(asset: CoachingCmsMediaAsset) {
    try {
      await navigator.clipboard.writeText(`![${asset.altText || asset.fileName}](${asset.url})`);
      this.toaster.success('Görsel ekleme kodu kopyalandı.');
    } catch { this.error.set('Görsel kodu kopyalanamadı; URL’yi kullanarak ![açıklama](URL) biçiminde ekleyin.'); }
  }

  async deleteMedia(asset: CoachingCmsMediaAsset) {
    if (!await this.toaster.confirm(`“${asset.fileName}” Koçluk görsel kütüphanesinden silinsin mi?`, { title: 'Görseli sil' })) return;
    try { await firstValueFrom(this.service.deleteCmsMedia(asset.id)); this.toaster.success('Görsel silindi.'); this.loadMedia(); }
    catch (error) { this.error.set(this.apiError(error, 'Görsel silinemedi.')); }
  }

  changeMediaPage(delta: number) {
    const pageCount = this.mediaPageCount();
    this.media.update(page => ({ ...page, pageNumber: Math.max(1, Math.min(pageCount, page.pageNumber + delta)) }));
    this.loadMedia();
  }

  mediaPageCount() { return Math.max(1, Math.ceil(this.media().totalCount / this.media().pageSize)); }

  loadNewsletter(pageNumber = this.newsletterPage().pageNumber) {
    this.loading.set(true); this.error.set(null);
    this.service.getCoachingNewsletterSubscribers(pageNumber, 25, this.newsletterSearch, this.newsletterStatus).subscribe({
      next: page => { this.newsletterPage.set(page); this.loading.set(false); },
      error: () => { this.error.set('Koçluk bülten aboneleri yüklenemedi.'); this.loading.set(false); }
    });
  }

  newsletterPageCount() { return Math.max(1, Math.ceil(this.newsletterPage().totalCount / this.newsletterPage().pageSize)); }

  changeNewsletterPage(delta: number) {
    const pageNumber = Math.max(1, Math.min(this.newsletterPageCount(), this.newsletterPage().pageNumber + delta));
    if (pageNumber !== this.newsletterPage().pageNumber) this.loadNewsletter(pageNumber);
  }

  newsletterStatusLabel(status: string) {
    return status === 'Active' ? 'Onaylandı' : status === 'Unsubscribed' ? 'İptal edildi' : 'Onay bekliyor';
  }

  async exportNewsletter() {
    this.saving.set(true); this.error.set(null);
    try {
      const blob = await firstValueFrom(this.service.exportCoachingNewsletterSubscribers(this.newsletterStatus, this.newsletterSearch));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'coaching-newsletter-subscribers.csv';
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      this.toaster.success('Bülten abone listesi indirildi.');
    } catch (error) { this.error.set(this.apiError(error, 'Bülten abone listesi indirilemedi.')); }
    finally { this.saving.set(false); }
  }

  async unsubscribeNewsletter(subscriber: CoachingNewsletterSubscriber) {
    if (!await this.toaster.confirm(`${subscriber.email} adresinin bülten aboneliği iptal edilsin mi?`, { title: 'Aboneliği iptal et' })) return;
    try {
      await firstValueFrom(this.service.unsubscribeCoachingNewsletterSubscriber(subscriber.id));
      this.toaster.success('Abonelik iptal edildi.');
      this.loadNewsletter();
    } catch (error) { this.error.set(this.apiError(error, 'Abonelik iptal edilemedi.')); }
  }

  async deleteNewsletterSubscriber(subscriber: CoachingNewsletterSubscriber) {
    if (!await this.toaster.confirm(`${subscriber.email} adresini kalıcı olarak silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.`, { title: 'Aboneyi kalıcı sil' })) return;
    try {
      await firstValueFrom(this.service.deleteCoachingNewsletterSubscriber(subscriber.id));
      this.toaster.success('Abone kaydı kalıcı olarak silindi.');
      const nextPage = this.newsletterPage().items.length === 1 && this.newsletterPage().pageNumber > 1
        ? this.newsletterPage().pageNumber - 1
        : this.newsletterPage().pageNumber;
      this.loadNewsletter(nextPage);
    } catch (error) { this.error.set(this.apiError(error, 'Abone kaydı silinemedi.')); }
  }

  startCreate() { this.editingId = null; this.kind.set(this.draft.kind); this.draft = this.emptyDraft(this.kind()); this.scheduledLocal = ''; this.publishedAtLocal = ''; this.editing.set(true); this.error.set(null); if (this.kind() === 'Blog') this.loadMediaChoices(); }
  startEdit(entry: CoachingCmsEntry) {
    this.editingId = entry.id; this.kind.set(entry.kind); this.draft = this.toRequest(entry); this.scheduledLocal = entry.scheduledPublishAt?.slice(0, 16) ?? ''; this.publishedAtLocal = entry.publishedAt?.slice(0, 16) ?? ''; this.editing.set(true); this.error.set(null); if (entry.kind === 'Blog') this.loadMediaChoices();
  }

  onKindChanged() { if (this.draft.kind === 'Block' && !this.draft.group) this.draft.group = 'HomePage'; if (this.draft.kind === 'Blog') this.loadMediaChoices(); }
  setTags(value: string) { this.draft.tags = value.split(',').map(tag => tag.trim()).filter(Boolean).slice(0, 30); }
  cancelEdit() { this.editing.set(false); this.editingId = null; this.draft = this.emptyDraft(this.kind()); this.scheduledLocal = ''; this.publishedAtLocal = ''; }

  startHomeCreate() {
    if (this.homeGroupConfig().single && this.homePage().totalCount > 0) return;
    this.homeEditingId = null; this.homeDraft = this.emptyDraft('Block'); this.homeDraft.group = this.selectedHomeGroup;
    this.homeScheduledLocal = ''; this.homeEditing.set(true); this.error.set(null);
  }
  startHomeEdit(entry: CoachingCmsEntry) {
    this.homeEditingId = entry.id; this.homeDraft = this.toRequest(entry); this.homeScheduledLocal = entry.scheduledPublishAt?.slice(0, 16) ?? '';
    this.homeEditing.set(true); this.error.set(null);
  }
  cancelHomeEdit() { this.homeEditing.set(false); this.homeEditingId = null; this.homeDraft = this.emptyDraft('Block'); this.homeScheduledLocal = ''; }
  async saveHomeEntry() {
    const request = { ...this.homeDraft, kind: 'Block' as const, group: this.selectedHomeGroup, scheduledPublishAt: this.homeScheduledLocal ? new Date(this.homeScheduledLocal).toISOString() : null };
    await this.persistEntry(this.homeEditingId, request, 'Ana sayfa içeriği kaydedildi.', 'Ana sayfa içeriği kaydedilemedi; bağlantı ve adres alanlarını kontrol edin.', () => { this.cancelHomeEdit(); this.loadHomeGroup(this.selectedHomeGroup); });
  }
  async deleteHomeEntry(entry: CoachingCmsEntry) {
    if (!await this.toaster.confirm(`“${entry.title}” ana sayfa içeriği silinsin mi?`, { title: 'İçeriği sil' })) return;
    try { await firstValueFrom(this.service.deleteCmsEntry(entry.id)); this.toaster.success('Ana sayfa içeriği silindi.'); this.loadHomeGroup(this.selectedHomeGroup); }
    catch (error) { this.error.set(this.apiError(error, 'Ana sayfa içeriği silinemedi.')); }
  }

  startSpecialCreate() {
    this.specialEditingId = null; this.specialDraft = this.emptyDraft('Block'); this.specialDraft.group = this.tab() === 'faq' ? 'HomeFaq' : 'HomeTestimonials';
    this.specialScheduledLocal = ''; this.specialEditing.set(true); this.error.set(null);
  }
  startSpecialEdit(entry: CoachingCmsEntry) {
    this.specialEditingId = entry.id; this.specialDraft = this.toRequest(entry); this.specialScheduledLocal = entry.scheduledPublishAt?.slice(0, 16) ?? '';
    this.specialEditing.set(true); this.error.set(null);
  }
  cancelSpecialEdit() { this.specialEditing.set(false); this.specialEditingId = null; this.specialDraft = this.emptyDraft('Block'); this.specialScheduledLocal = ''; }
  canSaveSpecialEntry(): boolean {
    return !this.saving() && (this.tab() !== 'testimonials' || !this.specialDraft.isPublished || this.specialDraft.testimonialConsentConfirmed === true);
  }
  async saveSpecialEntry() {
    const group = this.tab() === 'faq' ? 'HomeFaq' : 'HomeTestimonials';
    const request = { ...this.specialDraft, kind: 'Block' as const, group, scheduledPublishAt: this.specialScheduledLocal ? new Date(this.specialScheduledLocal).toISOString() : null };
    const label = this.tab() === 'faq' ? 'SSS kaydı' : 'Kullanıcı yorumu';
    await this.persistEntry(this.specialEditingId, request, `${label} kaydedildi.`, `${label} kaydedilemedi.`, () => { this.cancelSpecialEdit(); this.loadSpecialEntries(); });
  }
  async deleteSpecialEntry(entry: CoachingCmsEntry) {
    const label = this.tab() === 'faq' ? 'SSS kaydı' : 'Kullanıcı yorumu';
    if (!await this.toaster.confirm(`“${entry.title}” ${label.toLowerCase()} silinsin mi?`, { title: 'Kaydı sil' })) return;
    try { await firstValueFrom(this.service.deleteCmsEntry(entry.id)); this.toaster.success(`${label} silindi.`); this.loadSpecialEntries(); }
    catch (error) { this.error.set(this.apiError(error, `${label} silinemedi.`)); }
  }

  showPreview(entry: CoachingCmsEntryRequest) { this.previewEntry.set({ ...entry }); }
  isCoachingMediaUrl(value: string | null | undefined): value is string {
    return !!value && /^\/api\/coaching\/cms\/media\/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
  }

  private async persistEntry(id: string | null, request: CoachingCmsEntryRequest, success: string, fallback: string, afterSave: () => void) {
    this.saving.set(true); this.error.set(null);
    try {
      if (id) await firstValueFrom(this.service.updateCmsEntry(id, request));
      else await firstValueFrom(this.service.createCmsEntry(request));
      this.toaster.success(success); afterSave();
    } catch (error) { this.error.set(this.apiError(error, fallback)); }
    finally { this.saving.set(false); }
  }

  async saveEntry() {
    this.error.set(null); this.saving.set(true);
    const request = { ...this.draft, scheduledPublishAt: this.scheduledLocal ? new Date(this.scheduledLocal).toISOString() : null, publishedAt: this.publishedAtLocal ? new Date(this.publishedAtLocal).toISOString() : null };
    try {
      if (this.editingId) await firstValueFrom(this.service.updateCmsEntry(this.editingId, request));
      else await firstValueFrom(this.service.createCmsEntry(request));
      this.toaster.success('Koçluk içeriği kaydedildi.'); this.cancelEdit(); this.loadEntries();
    } catch (error) { this.error.set(this.apiError(error, 'İçerik kaydedilemedi; adresin benzersiz olduğunu kontrol edin.')); }
    finally { this.saving.set(false); }
  }

  async deleteEntry(entry: CoachingCmsEntry) {
    if (!await this.toaster.confirm(`“${entry.title}” içeriği ve sürüm geçmişi silinsin mi?`, { title: 'İçeriği sil' })) return;
    try { await firstValueFrom(this.service.deleteCmsEntry(entry.id)); this.toaster.success('İçerik silindi.'); this.loadEntries(); }
    catch (error) { this.error.set(this.apiError(error, 'İçerik silinemedi.')); }
  }

  showRevisions(entry: CoachingCmsEntry) {
    this.revisionEntry.set(entry); this.revisions.set([]);
    this.service.getCmsRevisions(entry.id).subscribe({ next: revisions => this.revisions.set(revisions), error: () => this.error.set('İçerik sürümleri yüklenemedi.') });
  }

  async restoreRevision(revision: CoachingCmsRevision) {
    const entry = this.revisionEntry(); if (!entry) return;
    if (!await this.toaster.confirm(`Sürüm ${revision.version} geri yüklensin mi? Mevcut içerik sürüm geçmişinde korunur.`, { title: 'Sürümü geri yükle' })) return;
    try { await firstValueFrom(this.service.restoreCmsRevision(entry.id, revision.id)); this.toaster.success('Önceki sürüm geri yüklendi.'); this.revisionEntry.set(null); this.loadEntries(); }
    catch (error) { this.error.set(this.apiError(error, 'İçerik sürümü geri yüklenemedi.')); }
  }

  startNavigationCreate() { this.navigationEditingId = null; this.navigationDraft = this.emptyNavigationDraft(this.selectedMenuValue); this.navigationEditing.set(true); }
  editNavigation(item: CoachingCmsNavigationItem) { this.navigationEditingId = item.id; this.selectedMenuValue = item.menu === 'Footer' ? 'Footer' : 'Main'; this.navigationDraft = this.toNavigationRequest(item); this.navigationEditing.set(true); }
  cancelNavigationEdit() { this.navigationEditing.set(false); this.navigationEditingId = null; this.navigationDraft = this.emptyNavigationDraft(this.selectedMenuValue); }
  async saveNavigation() {
    this.saving.set(true); this.error.set(null);
    try {
      if (this.navigationEditingId) await firstValueFrom(this.service.updateNavigation(this.navigationEditingId, this.navigationDraft));
      else await firstValueFrom(this.service.createNavigation(this.navigationDraft));
      this.selectedMenuValue = this.navigationDraft.menu === 'Footer' ? 'Footer' : 'Main';
      this.toaster.success('Koçluk menüsü kaydedildi.'); this.cancelNavigationEdit(); this.loadNavigation();
    } catch (error) { this.error.set(this.apiError(error, 'Menü kaydedilemedi. Yalnız / ile başlayan veya HTTPS bağlantılar kullanılabilir.')); }
    finally { this.saving.set(false); }
  }
  async deleteNavigation(item: CoachingCmsNavigationItem) {
    if (!await this.toaster.confirm(`“${item.label}” menü öğesi silinsin mi?`, { title: 'Menü öğesini sil' })) return;
    try { await firstValueFrom(this.service.deleteNavigation(item.id)); this.toaster.success('Menü öğesi silindi.'); this.loadNavigation(); }
    catch (error) { this.error.set(this.apiError(error, 'Menü öğesi silinemedi.')); }
  }

  changePage(delta: number) { this.pageNumber.set(Math.min(this.totalPages(), Math.max(1, this.pageNumber() + delta))); this.loadEntries(); }

  private emptyDraft(kind = this.kind()): CoachingCmsEntryRequest {
    return { kind, group: kind === 'Block' ? 'HomePage' : null, title: '', slug: '', summary: null, content: '', seoTitle: null, seoDescription: null, tags: [], isPublished: false, scheduledPublishAt: null, sortOrder: 0, eyebrow: null, linkLabel: null, linkUrl: null, secondaryLinkLabel: null, secondaryLinkUrl: null, imageUrl: null, author: null, publishedAt: null, coverImageUrl: null, testimonialConsentConfirmed: false };
  }
  private emptyNavigationDraft(menu: 'Main' | 'Footer' = 'Main'): CoachingCmsNavigationRequest { return { menu, label: '', url: '', icon: null, sortOrder: 0, isVisible: true, openInNewTab: false }; }
  toRequest(entry: CoachingCmsEntry): CoachingCmsEntryRequest { return { kind: entry.kind, group: entry.group, title: entry.title, slug: entry.slug, summary: entry.summary, content: entry.content, seoTitle: entry.seoTitle, seoDescription: entry.seoDescription, tags: [...entry.tags], isPublished: entry.isPublished, scheduledPublishAt: entry.scheduledPublishAt, sortOrder: entry.sortOrder, eyebrow: entry.eyebrow ?? null, linkLabel: entry.linkLabel ?? null, linkUrl: entry.linkUrl ?? null, secondaryLinkLabel: entry.secondaryLinkLabel ?? null, secondaryLinkUrl: entry.secondaryLinkUrl ?? null, imageUrl: entry.imageUrl ?? null, author: entry.author ?? null, publishedAt: entry.publishedAt ?? null, coverImageUrl: entry.coverImageUrl ?? null, testimonialConsentConfirmed: entry.testimonialConsentConfirmed ?? false }; }
  private toNavigationRequest(item: CoachingCmsNavigationItem): CoachingCmsNavigationRequest { return { menu: item.menu, label: item.label, url: item.url, icon: item.icon, sortOrder: item.sortOrder, isVisible: item.isVisible, openInNewTab: item.openInNewTab }; }
  private apiError(error: unknown, fallback: string): string {
    const message = (error as { error?: { message?: string } })?.error?.message;
    return typeof message === 'string' && message.trim() ? message : fallback;
  }
}
