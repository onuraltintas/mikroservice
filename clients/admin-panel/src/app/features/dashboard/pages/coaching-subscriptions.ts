import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { IdentityService, UserDto } from '../../../core/services/identity.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';
import { CoachingAdminService, CoachingStudentRosterItem } from '../../../core/services/coaching-admin.service';
import {
  CoachingBankTransferRequest,
  CoachingManagementPage,
  CoachingManagementService,
  CoachingPayment,
  CoachingSubscription,
  CoachingSubscriptionPlan,
  CoachingSubscriptionPlanRequest,
  CoachingSubscriptionSeat,
  CoachingSubscriptionSettings,
  CoachingSubscriptionSettingsRequest
} from '../../../core/services/coaching-management.service';
import { ToasterService } from '../../../core/services/toaster.service';

type SubscriptionTab = 'plans' | 'requests' | 'subscriptions' | 'settings' | 'payments';

@Component({
  selector: 'app-coaching-subscriptions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="coaching-subscriptions-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Koçluk servisi</p>
        <h1 id="coaching-subscriptions-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Koçluk abonelikleri</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Koçluk planları, banka/EFT onayı, bireysel ve kurum erişimleri bu servisin veritabanında bağımsız yönetilir. Aktif abonelik zorunluluğu başlangıçta kapalıdır.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2" aria-label="Koçluk abonelik sekmeleri">
        @for (tabOption of tabs; track tabOption.value) { <button type="button" class="ui-tab rounded-lg border px-3 py-2" [attr.aria-pressed]="tab() === tabOption.value" (click)="selectTab(tabOption.value)">{{ tabOption.label }}</button> }
      </nav>
      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }

      @if (tab() === 'plans') {
        <section class="space-y-4" aria-labelledby="coaching-plans-heading">
          <div class="flex items-center justify-between"><div><h2 id="coaching-plans-heading" class="text-lg font-semibold">Planlar</h2><p class="muted">Bireysel planlar öğrenciye, öğretmen planları bağımsız öğretmenin kendi öğrencilerine; kurum planları kurum kontenjanına bağlanır.</p></div><button class="primary" type="button" (click)="startPlanCreate()">Yeni plan</button></div>
          @if (planEditing()) {
            <form class="form-card" (ngSubmit)="savePlan()"><h3 class="font-semibold">{{ planEditingId ? 'Planı düzenle' : 'Yeni plan' }}</h3><div class="form-grid">
              <label>Plan adı<input [(ngModel)]="planDraft.name" name="planName" maxlength="150" required /></label><label>Slug<input [(ngModel)]="planDraft.slug" name="planSlug" maxlength="80" placeholder="Boş bırakılırsa addan üretilir" /></label>
              <label>Plan türü<select [(ngModel)]="planDraft.audience" name="planAudience"><option value="Individual">Öğrenci / bireysel</option><option value="Teacher">Bağımsız öğretmen</option><option value="Institution">Kurum</option></select></label><label>Fiyat ({{ settings()?.currency || 'TRY' }})<input type="number" [(ngModel)]="planDraft.price" name="planPrice" min="0" step="0.01" [disabled]="planDraft.isContactOnly" required /></label>
              <label>Ödeme dönemi<select [(ngModel)]="planDraft.billingPeriod" name="planPeriod"><option value="OneTime">Tek seferlik</option><option value="Monthly">Aylık</option><option value="Quarterly">Üç aylık</option><option value="Annual">Yıllık</option></select></label><label>Erişim süresi (gün)<input type="number" [(ngModel)]="planDraft.durationDays" name="planDuration" min="1" max="3650" required /></label>
              @if (planDraft.audience === 'Institution' || planDraft.audience === 'Teacher') { <label>Öğrenci kontenjanı<input type="number" [(ngModel)]="planDraft.includedStudentSeats" name="planSeats" min="1" max="100000" required /></label> }
              <label>Sıralama<input type="number" [(ngModel)]="planDraft.sortOrder" name="planSort" min="0" max="10000" /></label>
              <label class="wide">Açıklama<textarea [(ngModel)]="planDraft.description" name="planDescription" maxlength="1000" rows="3"></textarea></label>
              <label class="wide">Özellikler (virgülle)<input [ngModel]="planDraft.features.join(', ')" (ngModelChange)="setPlanFeatures($event)" name="planFeatures" maxlength="1000" placeholder="Öğrenci raporları, sınırsız ödev" /></label>
              <label class="check"><input type="checkbox" [(ngModel)]="planDraft.isContactOnly" (ngModelChange)="syncContactOnlyPrice()" name="contactOnly" /> Fiyat için iletişime geçin</label><label class="check"><input type="checkbox" [(ngModel)]="planDraft.isActive" name="planActive" /> Aktif</label><label class="check"><input type="checkbox" [(ngModel)]="planDraft.isPublic" name="planPublic" /> Kullanıcı plan listesinde yayınla</label>
            </div><div class="form-actions"><button class="secondary" type="button" (click)="cancelPlanEdit()">İptal</button><button class="primary" type="submit" [disabled]="saving()">Kaydet</button></div></form>
          }
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Plan</th><th>Tür</th><th>Fiyat / süre</th><th>Kontenjan</th><th>Durum</th><th></th></tr></thead><tbody>@for (plan of plans(); track plan.id) {<tr><td><strong>{{ plan.name }}</strong><div class="muted">{{ plan.slug }}</div><div class="muted">{{ plan.description }}</div></td><td>{{ plan.audience === 'Institution' ? 'Kurum' : plan.audience === 'Teacher' ? 'Öğretmen' : 'Öğrenci / bireysel' }}</td><td>{{ plan.isContactOnly ? 'İletişime geçin' : (plan.price | number:'1.2-2') + ' ' + (settings()?.currency || 'TRY') }}<div class="muted">{{ plan.durationDays }} gün · {{ plan.billingPeriod }}</div></td><td>{{ plan.includedStudentSeats ?? '—' }}</td><td>{{ plan.isActive ? (plan.isPublic ? 'Aktif / açık' : 'Aktif / gizli') : 'Pasif' }}</td><td class="actions"><button type="button" (click)="editPlan(plan)">Düzenle</button><button type="button" (click)="deactivatePlan(plan)" [disabled]="!plan.isActive">Pasifleştir</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Henüz Koçluk planı yok.</td></tr>}</tbody></table></div>
        </section>
      }

      @if (tab() === 'requests') {
        <section class="space-y-4" aria-labelledby="coaching-requests-heading">
          <div><h2 id="coaching-requests-heading" class="text-lg font-semibold">Banka/EFT talepleri</h2><p class="muted">Ödeme sağlayıcısı entegrasyonu değil; dekont/refarans banka hareketiyle doğrulanır. Onay aboneliği tek işlemde açar.</p></div>
          <form class="inline-filter" (ngSubmit)="loadRequests()"><input [(ngModel)]="requestSearch" name="requestSearch" maxlength="100" placeholder="Kullanıcı, plan veya referans ara" /><select [(ngModel)]="requestStatus" name="requestStatus"><option value="">Tüm durumlar</option><option value="Pending">Bekleyen</option><option value="Approved">Onaylı</option><option value="Rejected">Reddedilen</option></select><button class="secondary" type="submit">Filtrele</button></form>
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Kullanıcı</th><th>Plan / tutar</th><th>Ödeme referansı</th><th>Durum</th><th>Tarih</th><th></th></tr></thead><tbody>@for (request of requests().items; track request.id) {<tr><td><strong>{{ request.userName }}</strong><div class="muted">{{ request.userEmail }}</div></td><td>{{ request.plan.name }}<div class="muted">{{ request.amount | number:'1.2-2' }} {{ request.currency }}</div></td><td>{{ request.paymentReference }}<div class="muted">{{ request.payerName || '' }} {{ request.note || '' }}</div></td><td>{{ statusLabel(request.status) }}<div class="muted">{{ request.reviewNote }}</div></td><td>{{ request.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td class="actions">@if (request.status === 'Pending') {<button type="button" (click)="reviewRequest(request, 'Approved')">Onayla</button><button type="button" (click)="reviewRequest(request, 'Rejected')">Reddet</button>}<button type="button" (click)="deleteRequest(request)">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">EFT talebi yok.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ requests().totalCount }}</span><button type="button" (click)="changePage('requests', -1)" [disabled]="requestPage <= 1">Önceki</button><button type="button" (click)="changePage('requests', 1)" [disabled]="requestPage >= pageCount(requests())">Sonraki</button></div></div>
        </section>
      }

      @if (tab() === 'subscriptions') {
        <section class="space-y-4" aria-labelledby="coaching-subscriptions-heading">
          <div><h2 id="coaching-subscriptions-heading" class="text-lg font-semibold">Bireysel, öğretmen ve kurum lisansları</h2><p class="muted">Yönetici erişim tanımlayabilir; öğretmen lisansı yalnız bağımsız öğretmenin, kurum lisansı yalnız seçilen kurumun öğrencilerine kontenjan verir.</p></div>
          <form class="form-card" (ngSubmit)="createSubscription()"><h3 class="font-semibold">Yönetici erişimi tanımla</h3><div class="form-grid">
            <label>Plan<select [(ngModel)]="subscriptionDraft.planId" name="subscriptionPlan" required (ngModelChange)="onPlanChanged()"><option value="">Seçin</option>@for (plan of plans(); track plan.id) {<option [value]="plan.id">{{ plan.name }} — {{ plan.audience === 'Institution' ? 'kurum' : plan.audience === 'Teacher' ? 'öğretmen' : 'öğrenci' }}</option>}</select></label>
            @if (selectedPlan?.audience === 'Individual') {
              <label class="wide">Öğrenci ara<input [(ngModel)]="userSearch" name="userSearch" maxlength="100" placeholder="Ad veya e-posta" /></label><div class="wide flex justify-end"><button type="button" class="secondary" (click)="searchStudents()">Öğrencileri ara</button></div>
              <label class="wide">Öğrenci<select [(ngModel)]="subscriptionDraft.userId" name="subscriptionUser" required (ngModelChange)="selectUser($event)"><option value="">Öğrenci seçin</option>@for (user of users(); track user.userId) {<option [value]="user.userId">{{ user.fullName }} · {{ user.email }}</option>}</select></label>
            }
            @if (selectedPlan?.audience === 'Teacher') {
              <label class="wide">Bağımsız öğretmen ara<input [(ngModel)]="userSearch" name="teacherSearch" maxlength="100" placeholder="Ad veya e-posta" /></label><div class="wide flex justify-end"><button type="button" class="secondary" (click)="searchTeachers()">Öğretmenleri ara</button></div>
              <label class="wide">Bağımsız öğretmen<select [(ngModel)]="subscriptionDraft.userId" name="subscriptionTeacher" required (ngModelChange)="selectUser($event)"><option value="">Öğretmen seçin</option>@for (user of users(); track user.userId) {<option [value]="user.userId">{{ user.fullName }} · {{ user.email }}</option>}</select><span class="muted">Kurum profiline bağlı öğretmenler kurum lisansıyla yönetilir.</span></label>
            }
            @if (selectedPlan?.audience === 'Institution') {
              <label class="wide">Kurum<select [(ngModel)]="subscriptionDraft.institutionId" name="subscriptionInstitution" required (ngModelChange)="onInstitutionChanged()"><option value="">Kurum seçin</option>@for (institution of institutions(); track institution.id) {<option [value]="institution.id">{{ institution.name }}</option>}</select></label>
              <label class="wide">Banka/EFT referansı<input [(ngModel)]="subscriptionDraft.paymentReference" name="subscriptionPaymentReference" maxlength="100" placeholder="Banka işlem referansı" /><span class="muted">Aynı kurum ve referansla ikinci bir lisans kaydı oluşturulmaz.</span></label>
              @if (subscriptionDraft.institutionId) {
                  <div class="wide space-y-2"><div class="flex flex-wrap gap-2"><input [(ngModel)]="studentSearch" name="institutionStudentSearch" maxlength="100" placeholder="Kuruma bağlı öğrenci ara" /><button type="button" class="secondary" (click)="loadInstitutionStudents(1)">Ara / yenile</button><span class="muted">Seçilen: {{ selectedStudentIds.size }} / {{ selectedPlan?.includedStudentSeats }}</span></div>
                  <div class="student-grid">@for (student of students(); track student.userId) {<label class="student-option"><input type="checkbox" [checked]="selectedStudentIds.has(student.userId)" (change)="toggleStudent(student.userId)" /><span>{{ student.firstName }} {{ student.lastName }}<small>{{ student.email }}</small></span></label>} @empty {<p class="muted">Bu aramada öğrenci bulunamadı.</p>}</div>
                  <div class="flex justify-end gap-2"><button type="button" class="secondary" (click)="loadInstitutionStudents(studentPage - 1)" [disabled]="studentPage <= 1">Önceki öğrenciler</button><span class="muted self-center">Sayfa {{ studentPage }} / {{ studentTotalPages }}</span><button type="button" class="secondary" (click)="loadInstitutionStudents(studentPage + 1)" [disabled]="studentPage >= studentTotalPages">Sonraki öğrenciler</button></div>
                </div>
              }
            }
            <label>Başlangıç tarihi<input type="date" [(ngModel)]="subscriptionStartDate" name="subscriptionStartDate" required /></label><label class="wide">Yönetici notu<textarea [(ngModel)]="subscriptionDraft.notes" name="subscriptionNotes" maxlength="2000" rows="2"></textarea></label>
          </div><div class="form-actions"><button class="primary" type="submit" [disabled]="saving() || !subscriptionDraft.planId">Erişimi tanımla</button></div></form>

          <form class="inline-filter" (ngSubmit)="loadSubscriptions()"><input [(ngModel)]="subscriptionSearch" name="subscriptionSearch" maxlength="100" placeholder="Kullanıcı, kurum veya plan ara" /><select [(ngModel)]="subscriptionStatus" name="subscriptionStatus"><option value="">Tüm durumlar</option><option value="Active">Aktif</option><option value="Cancelled">İptal</option><option value="Expired">Süresi dolmuş</option></select><button class="secondary" type="submit">Filtrele</button></form>
          <div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Kullanıcı / kurum</th><th>Plan</th><th>Durum</th><th>Başlangıç / bitiş</th><th>Kontenjan</th><th></th></tr></thead><tbody>@for (subscription of subscriptions().items; track subscription.id) {<tr><td>{{ subscription.userName || institutionName(subscription.institutionId) || subscription.userId || subscription.institutionId }}</td><td>{{ subscription.plan.name }}<div class="muted">{{ subscription.paymentReference || '' }}</div></td><td><select [(ngModel)]="subscription.status" [name]="'status-' + subscription.id"><option value="Active">Aktif</option><option value="Cancelled">İptal</option><option value="Expired">Süresi dolmuş</option></select></td><td>{{ subscription.startDate | date:'dd.MM.yyyy' }}<div class="muted">Bitiş: {{ subscription.endDate | date:'dd.MM.yyyy' }}</div></td><td>{{ subscription.usedSeatCount }} / {{ subscription.seatCount }}</td><td class="actions"><button type="button" (click)="saveSubscription(subscription)">Durumu kaydet</button>@if (subscription.institutionId) {<button type="button" (click)="manageSeats(subscription)">Öğrenciler</button>}</td></tr>} @empty {<tr><td colspan="6" class="empty">Abonelik kaydı yok.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ subscriptions().totalCount }}</span><button type="button" (click)="changePage('subscriptions', -1)" [disabled]="subscriptionPage <= 1">Önceki</button><button type="button" (click)="changePage('subscriptions', 1)" [disabled]="subscriptionPage >= pageCount(subscriptions())">Sonraki</button></div></div>
          @if (selectedLicense()) {<section class="data-card space-y-2"><div class="flex items-center justify-between"><h3 class="font-semibold">{{ institutionName(selectedLicense()?.institutionId ?? null) }} — öğrenci erişimleri</h3><button type="button" class="secondary" (click)="selectedLicense.set(null)">Kapat</button></div>@for (seat of seats(); track seat.studentId) {<div class="flex flex-wrap items-center justify-between gap-2 border-t py-2"><span>{{ studentName(seat.studentId) }}<small class="muted block">{{ seat.studentId }} · {{ seat.suspensionReason || '' }}</small></span><button type="button" class="secondary" (click)="changeSeat(seat)">{{ seat.isSuspended ? 'Erişimi aç' : 'Askıya al' }}</button></div>} @if (seats().length === 0) {<p class="muted">Bu lisansa öğrenci atanmadı.</p>}</section>}
        </section>
      }

      @if (tab() === 'settings') {
        <section class="space-y-4" aria-labelledby="coaching-settings-heading"><div><h2 id="coaching-settings-heading" class="text-lg font-semibold">Abonelik ve ödeme ayarları</h2><p class="muted">Erişim zorunluluğu varsayılan olarak kapalıdır. Açıldığında öğrenci uçlarına bireysel, kurum veya atanmış öğrenciler için etkin öğretmen kontenjanı gerekir; öğretmen ve yöneticilerin mevcut erişimi sürer.</p></div>
          <form class="form-card" (ngSubmit)="saveSettings()"><div class="form-grid"><label>Para birimi<select [(ngModel)]="settingsDraft.currency" name="currency"><option value="TRY">TRY</option><option value="EUR">EUR</option><option value="USD">USD</option></select></label><label class="check"><input type="checkbox" [(ngModel)]="settingsDraft.requireActiveSubscription" name="requireSubscription" /> Öğrenciler için aktif abonelik zorunlu olsun</label><label class="check"><input type="checkbox" [(ngModel)]="settingsDraft.bankTransferEnabled" name="bankEnabled" /> Havale/EFT bilgilerini yayınla</label><label>Hesap sahibi<input [(ngModel)]="settingsDraft.accountHolder" name="accountHolder" maxlength="200" /></label><label>Banka<input [(ngModel)]="settingsDraft.bankName" name="bankName" maxlength="200" /></label><label class="wide">IBAN<input [(ngModel)]="settingsDraft.iban" name="iban" maxlength="34" placeholder="TR…" /></label><label class="wide">Ödeme yönergesi<textarea [(ngModel)]="settingsDraft.paymentInstructions" name="paymentInstructions" maxlength="2000" rows="3"></textarea></label></div><p class="muted">Durum: {{ settings()?.isPubliclyAvailable ? 'Banka bilgileri ödeme akışında yayınlanıyor.' : 'Banka bilgileri yayınlanmıyor.' }}</p><div class="form-actions"><button class="primary" type="submit" [disabled]="saving()">Ayarları kaydet</button></div></form>
        </section>
      }

      @if (tab() === 'payments') {
        <section class="space-y-4" aria-labelledby="coaching-payments-heading"><div><h2 id="coaching-payments-heading" class="text-lg font-semibold">Ödeme geçmişi</h2><p class="muted">Bu listede yalnızca onaylanmış EFT bildirimleri ödeme kaydı oluşturur. Otomatik ödeme sağlayıcısı bağlı değildir.</p></div><form class="inline-filter" (ngSubmit)="loadPayments()"><input [(ngModel)]="paymentSearch" name="paymentSearch" maxlength="100" placeholder="Kullanıcı, plan veya referans ara" /><select [(ngModel)]="paymentStatus" name="paymentStatus"><option value="">Tüm durumlar</option><option value="Succeeded">Başarılı</option></select><button class="secondary" type="submit">Filtrele</button></form><div class="data-card overflow-x-auto"><table class="data-table"><thead><tr><th>Tarih</th><th>Kullanıcı</th><th>Plan</th><th>Tutar</th><th>Sağlayıcı / referans</th><th>Durum</th></tr></thead><tbody>@for (payment of payments().items; track payment.id) {<tr><td>{{ payment.createdAt | date:'dd.MM.yyyy HH:mm' }}</td><td>{{ payment.userName }}<div class="muted">{{ payment.userEmail }}</div></td><td>{{ payment.planName }}</td><td>{{ payment.amount | number:'1.2-2' }} {{ payment.currency }}</td><td>{{ payment.provider }}<div class="muted">{{ payment.reference || '—' }}</div></td><td>{{ payment.status }}</td></tr>} @empty {<tr><td colspan="6" class="empty">Ödeme kaydı yok.</td></tr>}</tbody></table><div class="pager"><span>Toplam {{ payments().totalCount }}</span><button type="button" (click)="changePage('payments', -1)" [disabled]="paymentPage <= 1">Önceki</button><button type="button" (click)="changePage('payments', 1)" [disabled]="paymentPage >= pageCount(payments())">Sonraki</button></div></div></section>
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
    input, select, textarea { border: 1px solid var(--ui-border-strong); border-radius: .5rem; background: transparent; padding: .5rem .75rem; color: inherit; }
    .form-actions, .pager, .inline-filter { display: flex; align-items: center; justify-content: flex-end; gap: .5rem; }
    .form-actions { margin-top: 1rem; } .inline-filter { flex-wrap: wrap; }
    .primary, .secondary, .actions button, .pager button { border-radius: .5rem; padding: .5rem .75rem; font-size: .875rem; }
    .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); }
    .secondary, .actions button, .pager button { border: 1px solid var(--ui-border-strong); }
    .data-table { width: 100%; text-align: left; font-size: .875rem; }
    .data-table th, .data-table td { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; vertical-align: top; }
    .actions { white-space: normal; } .actions button + button { margin-left: .25rem; }
    .muted { color: var(--ui-text-muted); font-size: .8rem; } .empty { padding: 2rem; text-align: center; color: var(--ui-text-muted); }
    .pager { justify-content: space-between; margin-top: .75rem; } .student-grid { max-height: 18rem; overflow: auto; border: 1px solid var(--ui-border); border-radius: .5rem; padding: .5rem; }
    .student-option { display: flex; flex-direction: row !important; align-items: flex-start; gap: .5rem; padding: .35rem; } .student-option small { display: block; color: var(--ui-text-muted); }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } .form-grid .wide { grid-column: auto; } .inline-filter input, .inline-filter select { width: 100%; } }
  `]
})
export class CoachingSubscriptionsComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly identity = inject(IdentityService);
  private readonly institutionService = inject(InstitutionService);
  private readonly coachingAdmin = inject(CoachingAdminService);
  private readonly toaster = inject(ToasterService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly tabs: ReadonlyArray<{ value: SubscriptionTab; label: string }> = [
    { value: 'plans', label: 'Planlar' }, { value: 'requests', label: 'EFT talepleri' },
    { value: 'subscriptions', label: 'Erişimler' }, { value: 'settings', label: 'Ödeme ve erişim ayarı' }, { value: 'payments', label: 'Ödeme geçmişi' }
  ];
  readonly tab = signal<SubscriptionTab>('plans');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly plans = signal<CoachingSubscriptionPlan[]>([]);
  readonly requests = signal<CoachingManagementPage<CoachingBankTransferRequest>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly subscriptions = signal<CoachingManagementPage<CoachingSubscription>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly payments = signal<CoachingManagementPage<CoachingPayment>>({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 });
  readonly settings = signal<CoachingSubscriptionSettings | null>(null);
  readonly institutions = signal<InstitutionDto[]>([]);
  readonly users = signal<UserDto[]>([]);
  readonly students = signal<CoachingStudentRosterItem[]>([]);
  readonly seats = signal<CoachingSubscriptionSeat[]>([]);
  readonly selectedLicense = signal<CoachingSubscription | null>(null);
  readonly planEditing = signal(false);
  readonly selectedStudentIds = new Set<string>();
  readonly studentNames = new Map<string, string>();
  planEditingId: string | null = null;
  requestSearch = '';
  requestStatus = '';
  requestPage = 1;
  subscriptionSearch = '';
  subscriptionStatus = '';
  subscriptionPage = 1;
  paymentSearch = '';
  paymentStatus = '';
  paymentPage = 1;
  userSearch = '';
  studentSearch = '';
  studentPage = 1;
  studentTotalPages = 1;
  subscriptionStartDate = new Date().toISOString().slice(0, 10);
  planFeatures = '';
  planDraft = this.emptyPlan();
  settingsDraft: CoachingSubscriptionSettingsRequest = this.emptySettingsDraft();
  subscriptionDraft = { planId: '', userId: null as string | null, userName: null as string | null, userEmail: null as string | null, institutionId: null as string | null, paymentReference: '', notes: '', studentIds: [] as string[] };

  ngOnInit() { if (isPlatformBrowser(this.platformId)) { this.loadPlans(); this.loadInstitutions(); } }

  selectTab(tab: SubscriptionTab) {
    this.tab.set(tab); this.error.set(null);
    if (tab === 'plans') this.loadPlans();
    else if (tab === 'requests') this.loadRequests();
    else if (tab === 'subscriptions') { this.loadPlans(); this.loadSubscriptions(); this.loadInstitutions(); }
    else if (tab === 'settings') this.loadSettings();
    else this.loadPayments();
  }

  loadPlans() { this.loading.set(true); this.service.getPlans(true).subscribe({ next: items => { this.plans.set(items); this.loading.set(false); }, error: () => { this.error.set('Koçluk planları yüklenemedi.'); this.loading.set(false); } }); }
  loadSettings() { this.loading.set(true); this.service.getSubscriptionSettings().subscribe({ next: settings => { this.settings.set(settings); this.settingsDraft = this.toSettingsRequest(settings); this.loading.set(false); }, error: () => { this.error.set('Koçluk ödeme ayarları yüklenemedi.'); this.loading.set(false); } }); }
  loadInstitutions() { this.institutionService.getAll(1, 100, '', true).subscribe({ next: result => this.institutions.set(result.items ?? []), error: () => this.error.set('Kurum listesi yüklenemedi.') }); }
  loadRequests() { this.loading.set(true); this.service.getTransferRequests(this.requestPage, 25, this.requestSearch, this.requestStatus).subscribe({ next: page => { this.requests.set(page); this.loading.set(false); }, error: () => { this.error.set('EFT bildirimleri yüklenemedi.'); this.loading.set(false); } }); }
  loadSubscriptions() { this.loading.set(true); this.service.getSubscriptions(this.subscriptionPage, 25, this.subscriptionSearch, this.subscriptionStatus).subscribe({ next: page => { this.subscriptions.set(page); this.loading.set(false); }, error: () => { this.error.set('Koçluk abonelikleri yüklenemedi.'); this.loading.set(false); } }); }
  loadPayments() { this.loading.set(true); this.service.getPayments(this.paymentPage, 25, this.paymentSearch, this.paymentStatus).subscribe({ next: page => { this.payments.set(page); this.loading.set(false); }, error: () => { this.error.set('Ödeme geçmişi yüklenemedi.'); this.loading.set(false); } }); }

  startPlanCreate() { this.planEditingId = null; this.planDraft = this.emptyPlan(); this.planFeatures = ''; this.planEditing.set(true); }
  editPlan(plan: CoachingSubscriptionPlan) { this.planEditingId = plan.id; this.planDraft = this.toPlanRequest(plan); this.planFeatures = plan.features.join(', '); this.planEditing.set(true); }
  cancelPlanEdit() { this.planEditing.set(false); this.planEditingId = null; }
  setPlanFeatures(value: string) { this.planDraft.features = value.split(',').map(item => item.trim()).filter(Boolean).slice(0, 30); }
  syncContactOnlyPrice() { if (this.planDraft.isContactOnly) this.planDraft.price = 0; }
  async savePlan() {
    this.saving.set(true); this.error.set(null);
    try {
      const request = { ...this.planDraft, includedStudentSeats: this.planDraft.audience === 'Individual' ? null : this.planDraft.includedStudentSeats, features: [...this.planDraft.features] };
      if (this.planEditingId) await firstValueFrom(this.service.updatePlan(this.planEditingId, request)); else await firstValueFrom(this.service.createPlan(request));
      this.toaster.success('Koçluk planı kaydedildi.'); this.cancelPlanEdit(); this.loadPlans();
    } catch (error) { this.error.set(this.apiError(error, 'Plan kaydedilemedi; tür, fiyat, süre ve benzersiz adres bilgilerini kontrol edin.')); }
    finally { this.saving.set(false); }
  }
  async deactivatePlan(plan: CoachingSubscriptionPlan) {
    if (!await this.toaster.confirm(`“${plan.name}” yeni satışlara kapatılsın mı? Var olan abonelikler korunur.`, { title: 'Planı pasifleştir' })) return;
    try { await firstValueFrom(this.service.deactivatePlan(plan.id)); this.toaster.success('Plan pasifleştirildi.'); this.loadPlans(); }
    catch (error) { this.error.set(this.apiError(error, 'Plan pasifleştirilemedi.')); }
  }

  async reviewRequest(request: CoachingBankTransferRequest, status: 'Approved' | 'Rejected') {
    const actionText = status === 'Approved' ? 'onaylayıp aboneliği aç' : 'reddet';
    if (!await this.toaster.confirm(`${request.userName} kullanıcısının ${request.amount} ${request.currency} tutarlı bildirimi ${actionText}?`, { title: 'EFT bildirimini incele' })) return;
    const reviewNote = await this.toaster.prompt(
      status === 'Rejected' ? 'Kullanıcının göreceği ret gerekçesini yazın.' : 'Banka kontrol notu (isteğe bağlı)',
      '',
      { title: status === 'Rejected' ? 'Ret gerekçesi' : 'İnceleme notu' });
    if (reviewNote === null) return;
    if (status === 'Rejected' && !reviewNote.trim()) { this.toaster.warning('EFT bildirimi için ret gerekçesi zorunludur.'); return; }
    try { await firstValueFrom(this.service.reviewTransferRequest(request.id, status, reviewNote.trim() || null)); this.toaster.success(status === 'Approved' ? 'Ödeme doğrulandı ve Koçluk erişimi açıldı.' : 'Ödeme bildirimi reddedildi.'); this.loadRequests(); if (status === 'Approved') this.loadSubscriptions(); }
    catch (error) { this.error.set(this.apiError(error, 'Bildirim işlenemedi; başka bir yönetici işlemiş olabilir.')); }
  }

  async deleteRequest(request: CoachingBankTransferRequest) {
    const approvedWarning = request.status === 'Approved'
      ? 'EFT bildirimi ve inceleme kaydı kalıcı olarak silinecek. Açılmış abonelik ve ödeme geçmişi korunur; erişim kapanmaz. '
      : 'EFT bildirimi ve inceleme kaydı kalıcı olarak silinecek. ';
    if (!await this.toaster.confirm(`${approvedWarning}Devam edilsin mi?`, { title: 'EFT bildirimini sil' })) return;
    try {
      await firstValueFrom(this.service.deleteTransferRequest(request.id));
      this.toaster.success('EFT bildirimi silindi.');
      this.loadRequests();
    } catch (error) {
      this.error.set(this.apiError(error, 'EFT bildirimi silinemedi.'));
    }
  }

  async saveSettings() {
    this.saving.set(true); this.error.set(null);
    try { const result = await firstValueFrom(this.service.updateSubscriptionSettings(this.settingsDraft)); this.settings.set(result); this.settingsDraft = this.toSettingsRequest(result); this.toaster.success('Koçluk abonelik ayarları kaydedildi.'); }
    catch (error) { this.error.set(this.apiError(error, 'Ayarlar kaydedilemedi; yayınlanan banka bilgileri ve IBAN doğruluğunu kontrol edin.')); }
    finally { this.saving.set(false); }
  }

  get selectedPlan(): CoachingSubscriptionPlan | null { return this.plans().find(plan => plan.id === this.subscriptionDraft.planId) ?? null; }
  onPlanChanged() { this.subscriptionDraft.userId = null; this.subscriptionDraft.userName = null; this.subscriptionDraft.userEmail = null; this.subscriptionDraft.institutionId = null; this.subscriptionDraft.paymentReference = ''; this.subscriptionDraft.studentIds = []; this.selectedStudentIds.clear(); this.students.set([]); this.users.set([]); }
  async searchStudents() {
    this.loading.set(true); this.error.set(null);
    try { const result = await firstValueFrom(this.identity.getAllUsers(1, 100, this.userSearch.trim(), 'Student', true, 1)); this.users.set(result.items ?? []); }
    catch (error) { this.error.set(this.apiError(error, 'Koçluk öğrencileri aranamadı.')); }
    finally { this.loading.set(false); }
  }
  async searchTeachers() {
    this.loading.set(true); this.error.set(null);
    try {
      const result = await firstValueFrom(this.identity.getAllUsers(1, 100, this.userSearch.trim(), 'Teacher', true, 1));
      this.users.set((result.items ?? []).filter(user => !user.teacherDetails?.institutionId));
    } catch (error) { this.error.set(this.apiError(error, 'Koçluk öğretmenleri aranamadı.')); }
    finally { this.loading.set(false); }
  }
  selectUser(userId: string) { const user = this.users().find(item => item.userId === userId); this.subscriptionDraft.userName = user?.fullName ?? null; this.subscriptionDraft.userEmail = user?.email ?? null; }
  onInstitutionChanged() { this.selectedStudentIds.clear(); this.subscriptionDraft.studentIds = []; this.studentPage = 1; this.studentSearch = ''; this.loadInstitutionStudents(1); }
  loadInstitutionStudents(page: number) {
    const institutionId = this.subscriptionDraft.institutionId;
    if (!institutionId) return;
    this.studentPage = Math.max(1, page); this.loading.set(true);
    this.coachingAdmin.getStudentRoster(institutionId, this.studentPage, this.studentSearch).subscribe({
      next: result => { this.students.set(result.students ?? []); this.studentTotalPages = Math.max(1, Math.ceil(result.totalCount / 25)); for (const student of result.students ?? []) this.studentNames.set(student.userId, `${student.firstName} ${student.lastName}`); this.loading.set(false); },
      error: () => { this.error.set('Seçilen kurumun öğrencileri yüklenemedi.'); this.loading.set(false); }
    });
  }
  toggleStudent(studentId: string) {
    if (this.selectedStudentIds.has(studentId)) this.selectedStudentIds.delete(studentId);
    else if (this.selectedStudentIds.size < (this.selectedPlan?.includedStudentSeats ?? 0)) this.selectedStudentIds.add(studentId);
    else this.toaster.warning('Plan kontenjanı kadar öğrenci seçebilirsiniz.');
    this.subscriptionDraft.studentIds = [...this.selectedStudentIds];
  }

  async createSubscription() {
    const plan = this.selectedPlan;
    if (!plan) return;
    if ((plan.audience === 'Individual' || plan.audience === 'Teacher') && !this.subscriptionDraft.userId) { this.error.set(plan.audience === 'Teacher' ? 'Öğretmen planı için bağımsız öğretmen seçin.' : 'Abonelik için öğrenci seçin.'); return; }
    if (plan.audience === 'Institution' && !this.subscriptionDraft.institutionId) { this.error.set('Kurum planı için kurum seçin.'); return; }
    this.saving.set(true); this.error.set(null);
    try {
      await firstValueFrom(this.service.createSubscription({
        planId: plan.id,
        userId: plan.audience === 'Institution' ? null : this.subscriptionDraft.userId,
        userName: plan.audience === 'Institution' ? null : this.subscriptionDraft.userName,
        userEmail: plan.audience === 'Institution' ? null : this.subscriptionDraft.userEmail,
        institutionId: plan.audience === 'Institution' ? this.subscriptionDraft.institutionId : null,
        studentIds: plan.audience === 'Institution' ? [...this.selectedStudentIds] : [],
        startDate: new Date(`${this.subscriptionStartDate}T00:00:00`).toISOString(),
        notes: this.subscriptionDraft.notes.trim() || null,
        paymentReference: plan.audience === 'Institution' ? this.subscriptionDraft.paymentReference.trim() || null : null
      }));
      this.toaster.success('Koçluk erişimi tanımlandı.'); this.subscriptionDraft = { planId: '', userId: null, userName: null, userEmail: null, institutionId: null, paymentReference: '', notes: '', studentIds: [] }; this.selectedStudentIds.clear(); this.loadSubscriptions();
    } catch (error) { this.error.set(this.apiError(error, 'Erişim tanımlanamadı; kullanıcı, kurum ve kontenjan bilgilerini kontrol edin.')); }
    finally { this.saving.set(false); }
  }

  async saveSubscription(subscription: CoachingSubscription) {
    const endDate = subscription.endDate;
    try { await firstValueFrom(this.service.updateSubscription(subscription.id, subscription.status, endDate, subscription.notes)); this.toaster.success('Abonelik durumu güncellendi.'); this.loadSubscriptions(); }
    catch (error) { this.error.set(this.apiError(error, 'Abonelik güncellenemedi.')); }
  }
  async manageSeats(subscription: CoachingSubscription) {
    this.selectedLicense.set(subscription);
    this.service.getSubscriptionSeats(subscription.id).subscribe({ next: seats => this.seats.set(seats), error: () => { this.seats.set([]); this.error.set('Kurum lisansı öğrenci listesi yüklenemedi.'); } });
    if (subscription.institutionId) {
      this.coachingAdmin.getStudentRoster(subscription.institutionId, 1, '').subscribe({ next: result => { for (const student of result.students ?? []) this.studentNames.set(student.userId, `${student.firstName} ${student.lastName}`); }, error: () => undefined });
    }
  }
  studentName(id: string) { return this.studentNames.get(id) || 'Koçluk öğrencisi'; }
  institutionName(id: string | null) { return this.institutions().find(institution => institution.id === id)?.name ?? ''; }
  async changeSeat(seat: CoachingSubscriptionSeat) {
    const suspend = !seat.isSuspended;
    if (!await this.toaster.confirm(suspend ? 'Bu öğrenci için kurum aboneliği askıya alınsın mı?' : 'Öğrencinin kurum aboneliği yeniden açılsın mı?', { title: 'Öğrenci erişimi' })) return;
    const reason = suspend ? await this.toaster.prompt('Askıya alma nedeni (isteğe bağlı)', '', { title: 'Erişimi askıya al' }) : null;
    if (suspend && reason === null) return;
    const subscription = this.selectedLicense(); if (!subscription) return;
    try { await firstValueFrom(this.service.changeStudentSeat(subscription.id, seat.studentId, suspend, reason?.trim() || null)); this.toaster.success(suspend ? 'Öğrenci erişimi askıya alındı.' : 'Öğrenci erişimi yeniden açıldı.'); this.manageSeats(subscription); this.loadSubscriptions(); }
    catch (error) { this.error.set(this.apiError(error, 'Öğrenci erişimi güncellenemedi.')); }
  }

  changePage(kind: 'requests' | 'subscriptions' | 'payments', delta: number) {
    if (kind === 'requests') { this.requestPage = Math.max(1, Math.min(this.pageCount(this.requests()) || 1, this.requestPage + delta)); this.loadRequests(); }
    else if (kind === 'subscriptions') { this.subscriptionPage = Math.max(1, Math.min(this.pageCount(this.subscriptions()) || 1, this.subscriptionPage + delta)); this.loadSubscriptions(); }
    else { this.paymentPage = Math.max(1, Math.min(this.pageCount(this.payments()) || 1, this.paymentPage + delta)); this.loadPayments(); }
  }
  pageCount(page: CoachingManagementPage<unknown>) { return Math.max(1, Math.ceil(page.totalCount / page.pageSize)); }
  statusLabel(status: string) { return status === 'Pending' ? 'Bekliyor' : status === 'Approved' ? 'Onaylandı' : status === 'Rejected' ? 'Reddedildi' : status; }

  private emptyPlan(): CoachingSubscriptionPlanRequest { return { slug: '', name: '', description: '', audience: 'Individual', price: 0, isContactOnly: false, billingPeriod: 'Annual', durationDays: 365, includedStudentSeats: null, features: [], isActive: true, isPublic: true, sortOrder: 0 }; }
  private toPlanRequest(plan: CoachingSubscriptionPlan): CoachingSubscriptionPlanRequest { return { slug: plan.slug, name: plan.name, description: plan.description, audience: plan.audience, price: plan.price, isContactOnly: plan.isContactOnly, billingPeriod: plan.billingPeriod, durationDays: plan.durationDays, includedStudentSeats: plan.includedStudentSeats, features: [...plan.features], isActive: plan.isActive, isPublic: plan.isPublic, sortOrder: plan.sortOrder }; }
  private emptySettingsDraft(): CoachingSubscriptionSettingsRequest { return { requireActiveSubscription: false, currency: 'TRY', accountHolder: null, bankName: null, iban: null, paymentInstructions: null, bankTransferEnabled: false }; }
  private toSettingsRequest(value: CoachingSubscriptionSettings): CoachingSubscriptionSettingsRequest { return { requireActiveSubscription: value.requireActiveSubscription, currency: value.currency, accountHolder: value.accountHolder, bankName: value.bankName, iban: value.iban, paymentInstructions: value.paymentInstructions, bankTransferEnabled: value.bankTransferEnabled }; }
  private apiError(error: unknown, fallback: string): string { const message = (error as { error?: { message?: string } })?.error?.message; return typeof message === 'string' && message.trim() ? message : fallback; }
}
