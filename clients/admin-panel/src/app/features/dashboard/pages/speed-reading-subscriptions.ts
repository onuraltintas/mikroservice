import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, firstValueFrom } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingBankTransferRequest,
  SpeedReadingBankTransferRequestPage,
  SpeedReadingBankTransferSettings,
  SpeedReadingBankTransferSettingsRequest,
  SpeedReadingInstitutionAccessRequest,
  SpeedReadingManualSubscriptionRequest,
  SpeedReadingPayment,
  SpeedReadingPlan,
  SpeedReadingPlanRequest,
  SpeedReadingProduct,
  SpeedReadingProductRequest,
  SpeedReadingSubscription,
  SpeedReadingSubscriptionUpdateRequest
} from '../../../core/services/speed-reading-admin.service';
import { IdentityService, UserDto } from '../../../core/services/identity.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';
import { ToasterService } from '../../../core/services/toaster.service';

type SubscriptionTab = 'products' | 'plans' | 'bankSettings' | 'transferRequests' | 'subscriptions' | 'institutions' | 'payments';

@Component({
  selector: 'app-speed-reading-subscriptions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="subscriptions-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p>
        <h1 id="subscriptions-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Erişim ve ödeme onayı yönetimi</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Banka bilgisini yayınlayın, öğrencilerin EFT taleplerini doğrulayın ve erişimi güvenli biçimde açın. Bireysel planlar 6 ay, kurum planları 1 yıl olarak tanımlanır; plan süresi bitiş tarihini otomatik belirler.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="Abonelik yönetim sekmeleri">
        @for (tab of tabs; track tab.value) {
          <button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value" [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value" class="ui-tab rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button>
        }
      </nav>

      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div> }

      @if (selectedTab() === 'products') {
        <section class="space-y-4" aria-labelledby="products-title">
          <div class="flex items-center justify-between"><h2 id="products-title" class="text-lg font-semibold text-gray-900 dark:text-white">Erişim ürünleri</h2><button type="button" (click)="startProductCreate()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni ürün</button></div>
          @if (productEditing()) { <form (ngSubmit)="saveProduct()" class="form-card"><h3>{{ productEditingId ? 'Ürünü düzenle' : 'Yeni ürün' }}</h3><div class="form-grid"><label>Slug<input [(ngModel)]="productDraft.slug" name="productSlug" required maxlength="80" [disabled]="!!productEditingId" /></label><label>Ad<input [(ngModel)]="productDraft.name" name="productName" required maxlength="150" /></label><label class="wide">Açıklama<textarea [(ngModel)]="productDraft.description" name="productDescription" required maxlength="1000"></textarea></label><label>İçerilen ürün slug’ları<input [(ngModel)]="productIncluded" name="productIncluded" placeholder="kocluk, hizliokuma" /></label><label>Sıra<input type="number" [(ngModel)]="productDraft.sortOrder" name="productSortOrder" min="0" max="10000" /></label><label class="check"><input type="checkbox" [(ngModel)]="productDraft.isActive" name="productActive" /> Aktif</label><label class="check"><input type="checkbox" [(ngModel)]="productDraft.isPublic" name="productPublic" /> Herkese açık</label></div><div class="form-actions"><button type="button" (click)="cancelProductEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form> }
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Ürün</th><th>Slug</th><th>Durum</th><th>Görünürlük</th><th>Sıra</th><th></th></tr></thead><tbody>@for (product of products(); track product.id) {<tr><td><strong>{{ product.name }}</strong><div class="muted">{{ product.description }}</div></td><td class="font-mono">{{ product.slug }}</td><td>{{ product.isActive ? 'Aktif' : 'Pasif' }}</td><td>{{ product.isPublic ? 'Açık' : 'Gizli' }}</td><td>{{ product.sortOrder }}</td><td class="actions"><button type="button" (click)="startProductEdit(product)">Düzenle</button><button type="button" (click)="deactivateProduct(product)" [disabled]="!product.isActive">Pasifleştir</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Ürün bulunamadı.</td></tr>}</tbody></table></div></div>
        </section>
      }

      @if (selectedTab() === 'plans') {
        <section class="space-y-4" aria-labelledby="plans-title">
          <div class="flex items-center justify-between"><h2 id="plans-title" class="text-lg font-semibold text-gray-900 dark:text-white">Erişim planları</h2><button type="button" (click)="startPlanCreate()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni plan</button></div>
          <p class="muted">Bireysel planı 180 gün ve herkese açık, kurum planını 365 gün ve gizli tanımlayın. Ödeme, havale/EFT ile doğrulandıktan sonra erişim onayından açılır.</p>
          @if (planEditing()) { <form (ngSubmit)="savePlan()" class="form-card"><h3>{{ planEditingId ? 'Planı düzenle' : 'Yeni plan' }}</h3><div class="form-grid"><label class="wide">Ad<input [(ngModel)]="planDraft.name" name="planName" required maxlength="150" /></label><label>Slug<input [(ngModel)]="planDraft.slug" name="planSlug" required maxlength="80" [disabled]="!!planEditingId" /></label><label>Ürün<select [(ngModel)]="planDraft.productId" name="planProductId" required [disabled]="!!planEditingId"><option value="">Seçin</option>@for (product of products(); track product.id) {<option [value]="product.id">{{ product.name }}</option>}</select></label><label>Fiyat<input type="number" [(ngModel)]="planDraft.price" name="planPrice" min="0" step="0.01" required /></label><label>Satış biçimi<select [(ngModel)]="planDraft.billingPeriod" name="planBillingPeriod"><option value="OneTime">Tek seferlik erişim</option><option value="Monthly">Aylık</option><option value="Quarterly">Üç aylık</option><option value="Annual">Yıllık</option></select></label><label>Erişim süresi (gün)<input type="number" [(ngModel)]="planDraft.durationDays" name="planDurationDays" min="1" required /></label><label>Sıra<input type="number" [(ngModel)]="planDraft.sortOrder" name="planSortOrder" min="0" max="10000" /></label><label class="wide">Açıklama<textarea [(ngModel)]="planDraft.description" name="planDescription" required maxlength="1000"></textarea></label><label class="wide">Özellikler (virgülle)<input [(ngModel)]="planFeatures" name="planFeatures" placeholder="Günlük egzersiz, Raporlar" /></label><label class="check"><input type="checkbox" [(ngModel)]="planDraft.isActive" name="planActive" /> Aktif</label><label class="check"><input type="checkbox" [(ngModel)]="planDraft.isPublic" name="planPublic" /> Herkese açık</label></div><div class="form-actions"><button type="button" (click)="cancelPlanEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form> }
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Plan</th><th>Ürün</th><th>Fiyat</th><th>Dönem</th><th>Durum</th><th></th></tr></thead><tbody>@for (plan of plans(); track plan.id) {<tr><td><strong>{{ plan.name }}</strong><div class="muted">{{ plan.slug }}</div></td><td>{{ plan.productName }}</td><td>{{ plan.price | number:'1.2-2' }}</td><td>{{ plan.billingPeriod }}</td><td>{{ plan.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="startPlanEdit(plan)">Düzenle</button><button type="button" (click)="deactivatePlan(plan)" [disabled]="!plan.isActive">Pasifleştir</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Plan bulunamadı.</td></tr>}</tbody></table></div></div>
        </section>
      }

      @if (selectedTab() === 'bankSettings') {
        <section class="space-y-4" aria-labelledby="bank-settings-title">
          <div><h2 id="bank-settings-title" class="text-lg font-semibold text-gray-900 dark:text-white">Havale/EFT banka ayarı</h2><p class="muted">Bu bilgiler yalnızca etkin olduğunda ödeme sayfasında görünür. IBAN ve hesap sahibi bilgilerini yazmadan etkinleştiremezsiniz.</p></div>
          <form (ngSubmit)="saveBankTransferSettings()" class="form-card"><div class="form-grid"><label>Hesap sahibi<input [(ngModel)]="bankTransferSettingsDraft.accountHolder" name="bankAccountHolder" required maxlength="200" autocomplete="organization" /></label><label>Banka adı<input [(ngModel)]="bankTransferSettingsDraft.bankName" name="bankName" required maxlength="200" /></label><label class="wide">IBAN<input [(ngModel)]="bankTransferSettingsDraft.iban" name="bankIban" required maxlength="34" placeholder="TR00 0000 0000 0000 0000 0000 00" autocomplete="off" /></label><label class="wide">Ödeme yönergesi<textarea [(ngModel)]="bankTransferSettingsDraft.instructions" name="bankInstructions" maxlength="2000" placeholder="Örn. Açıklamaya öğrenci adını ve işlem referansını yazın."></textarea></label><label class="check"><input type="checkbox" [(ngModel)]="bankTransferSettingsDraft.isEnabled" name="bankTransferEnabled" /> Ödeme sayfasında yayınla</label></div><p class="muted">Durum: {{ bankTransferSettings()?.isPubliclyAvailable ? 'Yayında' : 'Yayında değil' }}</p><div class="form-actions"><button type="submit" class="primary" [disabled]="saving()">Banka ayarını kaydet</button></div></form>
        </section>
      }

      @if (selectedTab() === 'transferRequests') {
        <section class="space-y-4" aria-labelledby="transfer-requests-title">
          <div><h2 id="transfer-requests-title" class="text-lg font-semibold text-gray-900 dark:text-white">EFT talep kuyruğu</h2><p class="muted">Öğrencinin referansını banka hareketiyle doğrulayın. Onay işlemi, planın erişimini bir kez açar.</p></div>
          <form (ngSubmit)="loadBankTransferRequests()" class="inline-filter"><input [(ngModel)]="bankTransferSearch" name="bankTransferSearch" placeholder="Ad, e-posta veya referans ara" maxlength="100" /><select [(ngModel)]="bankTransferStatus" name="bankTransferStatus"><option value="">Tüm durumlar</option><option value="Pending">İnceleniyor</option><option value="Approved">Onaylandı</option><option value="Rejected">Reddedildi</option></select><button type="submit" class="secondary">Filtrele</button></form>
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Talep</th><th>Plan / tutar</th><th>Referans</th><th>Durum</th><th>Tarih</th><th></th></tr></thead><tbody>@for (request of bankTransferRequests().items; track request.id) {<tr><td><strong>{{ request.userName }}</strong><div class="muted">{{ request.userEmail }}</div>@if (request.payerName) {<div class="muted">Gönderen: {{ request.payerName }}</div>}@if (request.note) {<div class="muted">{{ request.note }}</div>}</td><td>{{ request.planName }}<div class="muted">{{ request.amount | number:'1.2-2' }} {{ request.currency }}</div></td><td class="font-mono">{{ request.paymentReference }}</td><td>{{ bankTransferStatusLabel(request.status) }}@if (request.reviewNote) {<div class="muted">{{ request.reviewNote }}</div>}</td><td>{{ request.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td class="actions">@if (request.status === 'Pending') {<button type="button" (click)="approveBankTransferRequest(request)">Onayla</button><button type="button" (click)="rejectBankTransferRequest(request)">Reddet</button>}</td></tr>} @empty {<tr><td colspan="6" class="empty">EFT talebi bulunamadı.</td></tr>}</tbody></table></div><div class="pager"><span>Toplam {{ bankTransferRequests().totalCount }}</span><button type="button" (click)="changeBankTransferPage(bankTransferPage - 1)" [disabled]="bankTransferPage <= 1 || loading()">Önceki</button><button type="button" (click)="changeBankTransferPage(bankTransferPage + 1)" [disabled]="bankTransferPage >= bankTransferTotalPages() || loading()">Sonraki</button></div></div>
        </section>
      }

      @if (selectedTab() === 'institutions') {
        <section class="space-y-4" aria-labelledby="institution-access-title">
          <div><h2 id="institution-access-title" class="text-lg font-semibold text-gray-900 dark:text-white">Kurum için toplu erişim onayı</h2><p class="muted">Kurum ödemesini banka hesabında teyit ettikten sonra 365 günlük gizli planı seçin ve öğrencileri tek işlemle etkinleştirin. Mevcut aktif erişimler tekrar oluşturulmaz.</p></div>
          <form (ngSubmit)="approveInstitutionAccess()" class="form-card"><div class="form-grid"><label>Kurum<select [(ngModel)]="institutionId" (ngModelChange)="selectInstitution()" name="institutionId" required><option value="">Kurum seçin</option>@for (institution of institutions(); track institution.id) {<option [value]="institution.id">{{ institution.name }} · {{ institution.studentCount }} öğrenci</option>}</select></label><label>1 yıllık kurum planı<select [(ngModel)]="institutionPlanId" name="institutionPlanId" required><option value="">Plan seçin</option>@for (plan of plans(); track plan.id) {@if (plan.durationDays === 365 && plan.isActive && !plan.isPublic) {<option [value]="plan.id">{{ plan.name }}</option>}}</select></label><label>Onay tarihi<input [(ngModel)]="institutionStartDate" name="institutionStartDate" type="date" required /></label><label>Havale / EFT referansı<input [(ngModel)]="institutionPaymentReference" name="institutionPaymentReference" maxlength="200" required placeholder="Banka dekont veya işlem no" /></label><label class="wide">Ödeme / kurum notu<textarea [(ngModel)]="institutionNotes" name="institutionNotes" maxlength="1000" placeholder="Sözleşme, ödeme veya kontenjan notu"></textarea></label></div>
            @if (institutionId) {<div class="mt-4"><div class="mb-2 flex items-center justify-between gap-3"><strong>Öğrenciler</strong><button type="button" class="secondary" (click)="selectAllInstitutionStudents()">Tümünü seç</button></div><div class="student-grid">@for (student of institutionStudents(); track student.userId) {<label class="student-option"><input type="checkbox" [checked]="selectedInstitutionStudentIds.has(student.userId)" (change)="toggleInstitutionStudent(student.userId)" /><span>{{ student.fullName }}<small>{{ student.email }}</small></span></label>} @empty {<p class="muted">Bu kurumda aktif öğrenci bulunamadı.</p>}</div><p class="muted">Seçili öğrenci: {{ selectedInstitutionStudentIds.size }}</p></div>}
            <div class="form-actions"><button type="submit" class="primary" [disabled]="saving()">Seçilen öğrencilere 1 yıllık erişim aç</button></div></form>
        </section>
      }

      @if (selectedTab() === 'subscriptions') {
        <section class="space-y-4" aria-labelledby="user-subscriptions-title"><div class="flex flex-col justify-between gap-3 sm:flex-row sm:items-end"><div><h2 id="user-subscriptions-title" class="text-lg font-semibold text-gray-900 dark:text-white">Havale/EFT erişim onayları</h2><p class="muted">Banka hesabındaki ödeme teyit edildikten sonra burada erişim açılır. Planın süresi, bitiş tarihini otomatik hesaplar.</p></div><button type="button" (click)="startSubscriptionCreate()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Ödeme sonrası erişim aç</button></div>
          @if (subscriptionEditing()) { <form (ngSubmit)="saveSubscription()" class="form-card"><h3>{{ subscriptionEditingId ? 'Erişimi düzenle' : 'Havale/EFT sonrası erişim aç' }}</h3><div class="form-grid">@if (!subscriptionEditingId) {<label class="wide">Öğrenci ara<input [(ngModel)]="studentSearch" (ngModelChange)="searchStudents()" name="studentSearch" placeholder="Ad veya e-posta yazın" maxlength="100" /></label><label>Öğrenci<select [(ngModel)]="subscriptionDraft.userId" name="subscriptionUserId" required><option value="">Öğrenci seçin</option>@for (student of students(); track student.userId) {<option [value]="student.userId">{{ student.fullName }} · {{ student.email }}</option>}</select></label>} @else {<label>Öğrenci<input [value]="subscriptionDraft.userId" disabled /></label>}<label>Plan<select [(ngModel)]="subscriptionDraft.planId" name="subscriptionPlanId" required [disabled]="!!subscriptionEditingId"><option value="">Seçin</option>@for (plan of plans(); track plan.id) {<option [value]="plan.id">{{ plan.name }}</option>}</select></label><label>Onay tarihi<input type="date" [(ngModel)]="subscriptionDraft.startDate" name="subscriptionStartDate" required [disabled]="!!subscriptionEditingId" /></label><label>Bitiş (isteğe bağlı düzeltme)<input type="date" [(ngModel)]="subscriptionDraft.endDate" name="subscriptionEndDate" /></label>@if (subscriptionEditingId) {<label>Durum<select [(ngModel)]="subscriptionUpdateStatus" name="subscriptionUpdateStatus"><option value="Active">Aktif</option><option value="Cancelled">İptal</option><option value="Expired">Süresi dolmuş</option></select></label>}<label class="wide">Ödeme / kurum notu<textarea [(ngModel)]="subscriptionDraft.notes" name="subscriptionNotes" maxlength="1000" placeholder="Ödeme tarihi, banka referansı veya kurum adı"></textarea></label></div><div class="form-actions"><button type="button" (click)="cancelSubscriptionEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Erişimi onayla</button></div></form> }
          <form (ngSubmit)="loadSubscriptions()" class="inline-filter"><input [(ngModel)]="subscriptionSearch" name="subscriptionSearch" placeholder="Kullanıcı ara" maxlength="100" /><select [(ngModel)]="subscriptionStatus" name="subscriptionStatus"><option value="">Tüm durumlar</option><option value="Active">Aktif</option><option value="Cancelled">İptal</option><option value="Expired">Süresi dolmuş</option></select><button type="submit" class="secondary">Filtrele</button></form>
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Kullanıcı</th><th>Plan</th><th>Durum</th><th>Başlangıç</th><th>Bitiş</th><th></th></tr></thead><tbody>@for (subscription of subscriptions().items; track subscription.id) {<tr><td>{{ subscription.userName || subscription.userEmail || subscription.userId }}</td><td>{{ subscription.plan.name }}</td><td>{{ subscription.status }}</td><td>{{ subscription.startDate | date:'dd.MM.yyyy' }}</td><td>{{ subscription.endDate ? (subscription.endDate | date:'dd.MM.yyyy') : '—' }}</td><td class="actions"><button type="button" (click)="editSubscription(subscription)">Düzenle</button><button type="button" (click)="cancelSubscription(subscription)">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Abonelik bulunamadı.</td></tr>}</tbody></table></div><div class="pager"><span>Toplam {{ subscriptions().totalCount }}</span><button type="button" (click)="changeSubscriptionPage(subscriptionPage - 1)" [disabled]="subscriptionPage <= 1 || loading()">Önceki</button><button type="button" (click)="changeSubscriptionPage(subscriptionPage + 1)" [disabled]="subscriptionPage >= subscriptionTotalPages() || loading()">Sonraki</button></div></div>
        </section>
      }

      @if (selectedTab() === 'payments') {
        <section class="space-y-4" aria-labelledby="payments-title"><div class="flex items-end justify-between gap-3"><div><h2 id="payments-title" class="text-lg font-semibold text-gray-900 dark:text-white">Ödeme geçmişi</h2><p class="muted">Bu liste geçmiş işlemleri gösterir; İyzico akışı bu ekrandan çalıştırılmaz.</p></div><form (ngSubmit)="loadPayments()" class="inline-filter"><input [(ngModel)]="paymentSearch" name="paymentSearch" placeholder="Kullanıcı ara" maxlength="100" /><select [(ngModel)]="paymentStatus" name="paymentStatus"><option value="">Tüm durumlar</option><option value="success">Başarılı</option><option value="failure">Başarısız</option></select><button type="submit" class="secondary">Filtrele</button></form></div><div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Tarih</th><th>Kullanıcı</th><th>Plan</th><th>Tutar</th><th>Durum</th><th>Sağlayıcı</th><th>Hata</th></tr></thead><tbody>@for (payment of payments().items; track payment.id) {<tr><td>{{ payment.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td>{{ payment.userName }}<div class="muted">{{ payment.userEmail }}</div></td><td>{{ payment.planName }}</td><td>{{ payment.amount | number:'1.2-2' }} {{ payment.currency }}</td><td>{{ payment.status }}</td><td>{{ payment.provider }}</td><td>{{ payment.errorMessage || '—' }}</td></tr>} @empty {<tr><td colspan="7" class="empty">Ödeme kaydı bulunamadı.</td></tr>}</tbody></table></div><div class="pager"><span>Toplam {{ payments().totalCount }}</span><button type="button" (click)="changePaymentPage(paymentPage - 1)" [disabled]="paymentPage <= 1 || loading()">Önceki</button><button type="button" (click)="changePaymentPage(paymentPage + 1)" [disabled]="paymentPage >= paymentTotalPages() || loading()">Sonraki</button></div></div></section>
      }

      @if (loading()) { <div role="status" class="text-center text-sm text-gray-500">Yükleniyor…</div> }
    </main>
  `,
  styles: [`
    .data-card, .form-card { border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface); padding: 1rem; }
    .form-card h3 { margin-bottom: .75rem; font-weight: 600; color: var(--ui-text); }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
    .form-grid label { display: flex; flex-direction: column; gap: .25rem; font-size: .875rem; color: var(--ui-text); }
    .form-grid .wide { grid-column: 1 / -1; }
    .form-grid .check { flex-direction: row; align-items: center; padding-top: 1.5rem; }
    input, select, textarea { border: 1px solid var(--ui-border-strong); border-radius: .5rem; background: transparent; padding: .5rem .75rem; color: inherit; }
    textarea { min-height: 4rem; }
    .form-actions, .pager, .inline-filter { display: flex; align-items: center; justify-content: flex-end; gap: .5rem; }
    .form-actions { margin-top: 1rem; }
    .primary, .secondary, .actions button, .pager button { border-radius: .5rem; padding: .5rem .75rem; font-size: .875rem; }
    .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); }
    .secondary, .actions button, .pager button { border: 1px solid var(--ui-border-strong); }
    .data-table { width: 100%; text-align: left; font-size: .875rem; }
    .data-table th { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; font-size: .7rem; text-transform: uppercase; color: var(--ui-text-muted); }
    .data-table td { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; color: var(--ui-text); vertical-align: top; }
    .actions { white-space: normal; }
    .actions button + button { margin-left: .35rem; }
    .muted { color: var(--ui-text-muted); font-size: .8rem; }
    .empty { padding: 2rem; text-align: center; color: var(--ui-text-muted); }
    .pager { justify-content: space-between; margin-top: .75rem; font-size: .8rem; color: var(--ui-text-muted); }
    .student-grid { display: grid; max-height: 22rem; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem; overflow-y: auto; border: 1px solid var(--ui-border); border-radius: .5rem; padding: .75rem; }
    .student-option { display: flex; flex-direction: row !important; align-items: flex-start; gap: .5rem; padding: .35rem; }
    .student-option small { display: block; color: var(--ui-text-muted); }
    @media (max-width: 640px) { .form-grid, .student-grid { grid-template-columns: 1fr; } .form-grid .wide { grid-column: auto; } .inline-filter { flex-wrap: wrap; justify-content: stretch; } .inline-filter input, .inline-filter select { min-width: 0; width: 100%; } }
  `]
})
export class SpeedReadingSubscriptionsComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly identity = inject(IdentityService);
  private readonly institutionService = inject(InstitutionService);
  private readonly toaster = inject(ToasterService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly tabs: ReadonlyArray<{ value: SubscriptionTab; label: string }> = [
    { value: 'products', label: 'Ürünler' }, { value: 'plans', label: 'Planlar' },
    { value: 'bankSettings', label: 'Banka ayarı' }, { value: 'transferRequests', label: 'EFT talepleri' },
    { value: 'subscriptions', label: 'Bireysel onaylar' }, { value: 'institutions', label: 'Kurum toplu onayı' },
    { value: 'payments', label: 'Ödeme geçmişi' }
  ];
  readonly selectedTab = signal<SubscriptionTab>('products');
  readonly products = signal<SpeedReadingProduct[]>([]);
  readonly plans = signal<SpeedReadingPlan[]>([]);
  readonly subscriptions = signal<{ items: SpeedReadingSubscription[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly payments = signal<{ items: SpeedReadingPayment[]; totalCount: number; pageNumber: number; pageSize: number }>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly bankTransferSettings = signal<SpeedReadingBankTransferSettings | null>(null);
  readonly bankTransferRequests = signal<SpeedReadingBankTransferRequestPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly students = signal<UserDto[]>([]);
  readonly institutions = signal<InstitutionDto[]>([]);
  readonly institutionStudents = signal<UserDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly productEditing = signal(false);
  readonly planEditing = signal(false);
  readonly subscriptionEditing = signal(false);
  productEditingId: string | null = null;
  planEditingId: string | null = null;
  subscriptionEditingId: string | null = null;
  productIncluded = '';
  planFeatures = '';
  subscriptionSearch = '';
  subscriptionStatus = '';
  subscriptionUpdateStatus = 'Active';
  paymentSearch = '';
  paymentStatus = '';
  bankTransferSearch = '';
  bankTransferStatus = '';
  studentSearch = '';
  institutionId = '';
  institutionPlanId = '';
  institutionStartDate = new Date().toISOString().slice(0, 10);
  institutionPaymentReference = '';
  institutionNotes = '';
  readonly selectedInstitutionStudentIds = new Set<string>();
  subscriptionPage = 1;
  paymentPage = 1;
  bankTransferPage = 1;
  readonly pageSize = 25;
  productDraft: SpeedReadingProductRequest = this.emptyProduct();
  planDraft: SpeedReadingPlanRequest = this.emptyPlan();
  subscriptionDraft: SpeedReadingManualSubscriptionRequest = this.emptySubscription();
  bankTransferSettingsDraft: SpeedReadingBankTransferSettingsRequest = this.emptyBankTransferSettings();

  ngOnInit(): void { if (isPlatformBrowser(this.platformId)) this.loadProducts(); }

  selectTab(tab: SubscriptionTab): void {
    if (this.selectedTab() === tab) return;
    this.selectedTab.set(tab);
    if (tab === 'products') this.loadProducts();
    if (tab === 'plans') this.loadPlans();
    if (tab === 'bankSettings') this.loadBankTransferSettings();
    if (tab === 'transferRequests') this.loadBankTransferRequests();
    if (tab === 'subscriptions') { this.loadPlans(); this.loadSubscriptions(); }
    if (tab === 'institutions') { this.loadPlans(); this.loadInstitutions(); }
    if (tab === 'payments') this.loadPayments();
  }

  startProductCreate(): void { this.productEditingId = null; this.productDraft = this.emptyProduct(); this.productIncluded = ''; this.productEditing.set(true); }
  startProductEdit(product: SpeedReadingProduct): void { this.productEditingId = product.id; this.productDraft = { ...product }; this.productIncluded = product.includedProductSlugs.join(', '); this.productEditing.set(true); }
  cancelProductEdit(): void { this.productEditing.set(false); }

  saveProduct(): void {
    const request = { ...this.productDraft, includedProductSlugs: this.csv(this.productIncluded) };
    const operation = this.productEditingId ? this.service.updateSubscriptionProduct(this.productEditingId, request) : this.service.createSubscriptionProduct(request);
    this.saveRequest(operation, () => { this.productEditing.set(false); this.loadProducts(); });
  }

  async deactivateProduct(product: SpeedReadingProduct): Promise<void> {
    if (!product.isActive || !await this.toaster.confirm(`“${product.name}” pasifleştirilsin mi?`, { title: 'Ürünü pasifleştir' })) return;
    this.saveRequest(this.service.deactivateSubscriptionProduct(product.id), () => this.loadProducts());
  }

  startPlanCreate(): void { this.planEditingId = null; this.planDraft = this.emptyPlan(); this.planFeatures = ''; this.planEditing.set(true); this.loadProducts(); }
  startPlanEdit(plan: SpeedReadingPlan): void { this.planEditingId = plan.id; this.planDraft = { ...plan, features: [...plan.features] }; this.planFeatures = plan.features.join(', '); this.planEditing.set(true); }
  cancelPlanEdit(): void { this.planEditing.set(false); }

  savePlan(): void {
    const features = this.csv(this.planFeatures);
    const operation = this.planEditingId
      ? this.service.updateSubscriptionPlan(this.planEditingId, { name: this.planDraft.name, description: this.planDraft.description, price: Number(this.planDraft.price), billingPeriod: this.planDraft.billingPeriod, durationDays: this.planDraft.durationDays, isActive: this.planDraft.isActive, isPublic: this.planDraft.isPublic, sortOrder: Number(this.planDraft.sortOrder), features })
      : this.service.createSubscriptionPlan({ ...this.planDraft, productId: this.planDraft.productId, price: Number(this.planDraft.price), sortOrder: Number(this.planDraft.sortOrder), features });
    this.saveRequest(operation, () => { this.planEditing.set(false); this.loadPlans(); });
  }

  async deactivatePlan(plan: SpeedReadingPlan): Promise<void> {
    if (!plan.isActive || !await this.toaster.confirm(`“${plan.name}” pasifleştirilsin mi?`, { title: 'Planı pasifleştir' })) return;
    this.saveRequest(this.service.deactivateSubscriptionPlan(plan.id), () => this.loadPlans());
  }

  startSubscriptionCreate(): void {
    this.subscriptionEditingId = null;
    this.subscriptionUpdateStatus = 'Active';
    this.subscriptionDraft = this.emptySubscription();
    this.subscriptionEditing.set(true);
    if (!this.plans().length) this.loadPlans();
    this.searchStudents();
  }

  editSubscription(subscription: SpeedReadingSubscription): void {
    this.subscriptionEditingId = subscription.id;
    this.subscriptionUpdateStatus = subscription.status;
    this.subscriptionDraft = {
      userId: subscription.userId,
      planId: subscription.plan.id,
      startDate: subscription.startDate.slice(0, 10),
      endDate: subscription.endDate?.slice(0, 10) ?? null,
      notes: subscription.notes
    };
    this.subscriptionEditing.set(true);
    if (!this.plans().length) this.loadPlans();
  }

  cancelSubscriptionEdit(): void {
    this.subscriptionEditingId = null;
    this.subscriptionEditing.set(false);
  }

  saveSubscription(): void {
    if (this.subscriptionEditingId) {
      const request: SpeedReadingSubscriptionUpdateRequest = {
        status: this.subscriptionUpdateStatus,
        endDate: this.subscriptionDraft.endDate || null,
        notes: this.subscriptionDraft.notes || null
      };
      this.saveRequest(this.service.updateUserSubscription(this.subscriptionEditingId, request), () => {
        this.cancelSubscriptionEdit();
        this.loadSubscriptions();
      });
      return;
    }

    const student = this.students().find(item => item.userId === this.subscriptionDraft.userId);
    const request = {
      ...this.subscriptionDraft,
      userName: student?.fullName ?? null,
      userEmail: student?.email ?? null,
      endDate: this.subscriptionDraft.endDate || null,
      notes: this.subscriptionDraft.notes || null
    };
    this.saveRequest(this.service.createManualSubscription(request), () => {
      this.cancelSubscriptionEdit();
      this.loadSubscriptions();
    });
  }

  async cancelSubscription(subscription: SpeedReadingSubscription): Promise<void> {
    if (!await this.toaster.confirm('Bu abonelik kaydı silinsin mi?', { title: 'Abonelik kaydını sil' })) return;
    this.saveRequest(this.service.deleteUserSubscription(subscription.id), () => this.loadSubscriptions());
  }

  loadProducts(): void { this.loadRequest(this.service.getSubscriptionProducts(), value => this.products.set(value)); }
  loadPlans(): void { this.loadRequest(this.service.getSubscriptionPlans(), value => this.plans.set(value)); }
  loadSubscriptions(): void { this.loadRequest(this.service.getUserSubscriptions(this.subscriptionPage, this.pageSize, this.subscriptionStatus || undefined, this.subscriptionSearch), value => this.subscriptions.set(value)); }
  loadPayments(): void { this.loadRequest(this.service.getPaymentHistory(this.paymentPage, this.pageSize, this.paymentStatus || undefined, this.paymentSearch), value => this.payments.set(value)); }
  loadBankTransferSettings(): void {
    this.loadRequest(this.service.getBankTransferSettings(), value => {
      this.bankTransferSettings.set(value);
      this.bankTransferSettingsDraft = value
        ? {
            accountHolder: value.accountHolder,
            bankName: value.bankName,
            iban: value.iban,
            instructions: value.instructions,
            isEnabled: value.isEnabled
          }
        : this.emptyBankTransferSettings();
    });
  }
  loadBankTransferRequests(): void {
    this.loadRequest(
      this.service.getBankTransferRequests(this.bankTransferPage, this.pageSize, this.bankTransferStatus || undefined, this.bankTransferSearch),
      value => this.bankTransferRequests.set(value)
    );
  }

  saveBankTransferSettings(): void {
    const request: SpeedReadingBankTransferSettingsRequest = {
      ...this.bankTransferSettingsDraft,
      accountHolder: this.bankTransferSettingsDraft.accountHolder.trim(),
      bankName: this.bankTransferSettingsDraft.bankName.trim(),
      iban: this.bankTransferSettingsDraft.iban.trim(),
      instructions: this.bankTransferSettingsDraft.instructions?.trim() || null
    };
    this.saveRequest(this.service.updateBankTransferSettings(request), value => {
      this.bankTransferSettings.set(value);
      this.bankTransferSettingsDraft = {
        accountHolder: value.accountHolder,
        bankName: value.bankName,
        iban: value.iban,
        instructions: value.instructions,
        isEnabled: value.isEnabled
      };
      this.toaster.success(value.isPubliclyAvailable ? 'Banka bilgisi ödeme sayfasında yayınlandı.' : 'Banka ayarı kaydedildi ancak yayınlanmadı.');
    });
  }

  async approveBankTransferRequest(request: SpeedReadingBankTransferRequest): Promise<void> {
    const accepted = await this.toaster.confirm(
      `${request.userName} için ${request.amount.toLocaleString('tr-TR')} ${request.currency} tutarındaki EFT talebi onaylansın mı? Erişim bir kez açılacaktır.`,
      { title: 'EFT talebini onayla', confirmText: 'Onayla' }
    );
    if (!accepted) return;
    this.saveRequest(this.service.reviewBankTransferRequest(request.id, 'Approved', null), () => {
      this.toaster.success('EFT talebi onaylandı ve erişim açıldı.');
      this.loadBankTransferRequests();
      this.loadSubscriptions();
    });
  }

  async rejectBankTransferRequest(request: SpeedReadingBankTransferRequest): Promise<void> {
    const reviewNote = await this.toaster.prompt(
      'Öğrencinin göreceği reddetme gerekçesini yazın.',
      '',
      { title: 'EFT talebini reddet', confirmText: 'Reddet' }
    );
    if (!reviewNote?.trim()) return;
    this.saveRequest(this.service.reviewBankTransferRequest(request.id, 'Rejected', reviewNote.trim()), () => {
      this.toaster.success('EFT talebi reddedildi.');
      this.loadBankTransferRequests();
    });
  }

  bankTransferStatusLabel(status: SpeedReadingBankTransferRequest['status']): string {
    return status === 'Approved' ? 'Onaylandı' : status === 'Rejected' ? 'Reddedildi' : 'İnceleniyor';
  }

  async searchStudents(): Promise<void> {
    try {
      const page = await firstValueFrom(this.identity.getAllUsers(1, 100, this.studentSearch.trim(), 'Student', true));
      this.students.set(page.items ?? []);
    } catch {
      this.students.set([]);
    }
  }

  async loadInstitutions(): Promise<void> {
    try {
      const result = await firstValueFrom(this.institutionService.getAll(1, 100, '', true));
      this.institutions.set(result.items ?? []);
    } catch {
      this.institutions.set([]);
    }
  }

  async selectInstitution(): Promise<void> {
    this.selectedInstitutionStudentIds.clear();
    this.institutionStudents.set([]);
    if (!this.institutionId) return;
    try {
      const students: UserDto[] = [];
      let pageNumber = 1;
      let totalCount = 0;
      do {
        const result = await firstValueFrom(this.identity.getAllUsers(pageNumber, 100, '', 'Student', true));
        students.push(...(result.items ?? []));
        totalCount = result.totalCount ?? students.length;
        pageNumber += 1;
      } while (students.length < totalCount && pageNumber <= 50);
      this.institutionStudents.set(students.filter(student => student.studentDetails?.institutionId === this.institutionId));
    } catch {
      this.error.set('Kurum öğrencileri yüklenemedi.');
    }
  }

  toggleInstitutionStudent(userId: string): void {
    if (this.selectedInstitutionStudentIds.has(userId)) this.selectedInstitutionStudentIds.delete(userId);
    else this.selectedInstitutionStudentIds.add(userId);
  }

  selectAllInstitutionStudents(): void {
    this.institutionStudents().forEach(student => this.selectedInstitutionStudentIds.add(student.userId));
  }

  approveInstitutionAccess(): void {
    const recipients = this.institutionStudents()
      .filter(student => this.selectedInstitutionStudentIds.has(student.userId))
      .map(student => ({ userId: student.userId, userName: student.fullName, userEmail: student.email }));
    if (!this.institutionId || !this.institutionPlanId || !this.institutionPaymentReference.trim() || recipients.length === 0) {
      this.error.set('Kurum, 1 yıllık plan, EFT referansı ve en az bir öğrenci seçin.');
      return;
    }
    const institution = this.institutions().find(item => item.id === this.institutionId);
    const request: SpeedReadingInstitutionAccessRequest = {
      institutionId: this.institutionId,
      planId: this.institutionPlanId,
      startDate: this.institutionStartDate,
      recipients,
      paymentReference: this.institutionPaymentReference.trim(),
      notes: [institution?.name, this.institutionNotes.trim()].filter(Boolean).join(' · ') || null
    };
    this.saveRequest(this.service.createInstitutionAccess(request), result => {
      this.toaster.success(`${result.createdCount} öğrenciye erişim açıldı${result.existingCount ? `; ${result.existingCount} öğrencinin aktif erişimi zaten vardı.` : '.'}`);
      this.selectedInstitutionStudentIds.clear();
      this.institutionPaymentReference = '';
      this.loadSubscriptions();
    });
  }

  changeSubscriptionPage(page: number): void { if (page < 1 || page > this.subscriptionTotalPages()) return; this.subscriptionPage = page; this.loadSubscriptions(); }
  changePaymentPage(page: number): void { if (page < 1 || page > this.paymentTotalPages()) return; this.paymentPage = page; this.loadPayments(); }
  changeBankTransferPage(page: number): void { if (page < 1 || page > this.bankTransferTotalPages()) return; this.bankTransferPage = page; this.loadBankTransferRequests(); }
  subscriptionTotalPages(): number { return Math.max(1, Math.ceil(this.subscriptions().totalCount / this.pageSize)); }
  paymentTotalPages(): number { return Math.max(1, Math.ceil(this.payments().totalCount / this.pageSize)); }
  bankTransferTotalPages(): number { return Math.max(1, Math.ceil(this.bankTransferRequests().totalCount / this.pageSize)); }

  private loadRequest<T>(request: import('rxjs').Observable<T>, apply: (value: T) => void): void {
    this.loading.set(true); this.error.set('');
    request.pipe(finalize(() => this.loading.set(false))).subscribe({ next: apply, error: () => this.error.set('Abonelik verisi yüklenemedi.') });
  }

  private saveRequest<T>(request: import('rxjs').Observable<T>, afterSave: (value: T) => void): void {
    this.saving.set(true); this.error.set('');
    request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: afterSave, error: () => this.error.set('Abonelik değişikliği kaydedilemedi.') });
  }

  private csv(value: string): string[] { return value.split(',').map(item => item.trim()).filter(Boolean); }
  private emptyProduct(): SpeedReadingProductRequest { return { slug: '', name: '', description: '', includedProductSlugs: [], isActive: true, isPublic: true, sortOrder: 0 }; }
  private emptyPlan(): SpeedReadingPlanRequest { return { name: '', description: '', slug: '', productId: '', price: 0, billingPeriod: 'OneTime', durationDays: 180, isActive: true, isPublic: false, sortOrder: 0, features: [] }; }
  private emptySubscription(): SpeedReadingManualSubscriptionRequest { return { userId: '', planId: '', startDate: new Date().toISOString().slice(0, 10), endDate: null, notes: null }; }
  private emptyBankTransferSettings(): SpeedReadingBankTransferSettingsRequest { return { accountHolder: '', bankName: '', iban: '', instructions: null, isEnabled: false }; }
}
