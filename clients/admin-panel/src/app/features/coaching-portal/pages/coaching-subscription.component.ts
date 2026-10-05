import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CoachingBankTransferRequest,
  CoachingManagementService,
  CoachingSubscriptionAccess,
  CoachingSubscriptionPlan,
  CoachingSubscriptionSettings
} from '../../../core/services/coaching-management.service';

@Component({
  selector: 'app-coaching-subscription',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="coaching-subscription-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-300">Koçluk hesabım</p>
        <h1 id="coaching-subscription-title" class="mt-1 text-2xl font-bold">Plan ve abonelik</h1>
        <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">Koçluk planlarını, erişim durumunu ve ödeme bildirimlerini bu Koçluk hesabından takip edebilirsin.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</div> }
      @if (success()) { <div role="status" class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-200">{{ success() }}</div> }

      @if (access()) {
        <section class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900" aria-labelledby="access-heading">
          <h2 id="access-heading" class="font-semibold">Mevcut Koçluk erişimin</h2>
          @if (access()?.subscriptions?.length) {
            <div class="mt-3 space-y-2">@for (subscription of access()?.subscriptions; track subscription.id) {
              <div class="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-slate-50 p-3 dark:bg-slate-800/70">
                <div><strong>{{ subscription.plan.name }}</strong><p class="text-sm text-slate-600 dark:text-slate-300">{{ subscription.status === 'Active' ? 'Aktif' : subscription.status }} · {{ subscription.startDate | date:'dd.MM.yyyy' }} – {{ subscription.endDate | date:'dd.MM.yyyy' }}</p></div>
                <span class="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-900/50 dark:text-emerald-200">Erişim açık</span>
              </div>
            }</div>
          } @else if (access()?.enforcementEnabled) {
            <p class="mt-2 text-sm text-amber-800 dark:text-amber-200">Şu anda etkin bir Koçluk aboneliğin yok. Erişim için aşağıdaki planlardan birini seçebilirsin.</p>
          } @else {
            <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">Koçluk erişimin açık. Abonelik zorunluluğu şu an etkin değil.</p>
          }
        </section>
      }

      <section class="space-y-3" aria-labelledby="plans-heading">
        <div><h2 id="plans-heading" class="text-lg font-semibold">Bireysel planlar</h2><p class="text-sm text-slate-600 dark:text-slate-300">Kurum aboneliği gerekiyorsa kurum yöneticinle iletişime geç.</p></div>
        @if (loading()) { <div role="status" class="rounded-xl bg-slate-100 p-6 text-center text-sm text-slate-600 dark:bg-slate-800">Planlar yükleniyor…</div> }
        @if (!loading() && plans().length === 0) { <div class="rounded-xl border border-slate-200 p-6 text-sm text-slate-600 dark:border-slate-800 dark:text-slate-300">Şu anda satışa açık bireysel plan bulunmuyor.</div> }
        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          @for (plan of plans(); track plan.id) {
            <article class="rounded-2xl border p-5 shadow-sm" [class.border-indigo-500]="selectedPlanId() === plan.id" [class.bg-indigo-50]="selectedPlanId() === plan.id" [class.dark:bg-indigo-950\/20]="selectedPlanId() === plan.id">
              <h3 class="text-lg font-semibold">{{ plan.name }}</h3><p class="mt-2 min-h-10 text-sm text-slate-600 dark:text-slate-300">{{ plan.description }}</p>
              <p class="mt-4 text-2xl font-bold">{{ plan.isContactOnly ? 'İletişime geçin' : (plan.price | number:'1.2-2') + ' ' + (settings()?.currency || 'TRY') }}</p>
              <p class="mt-1 text-sm text-slate-500">{{ plan.durationDays }} gün erişim · {{ plan.billingPeriod }}</p>
              @if (plan.features.length) { <ul class="mt-4 list-disc space-y-1 pl-5 text-sm">@for (feature of plan.features; track feature) { <li>{{ feature }}</li> }</ul> }
              @if (plan.isContactOnly) { <p class="mt-4 text-sm text-slate-500">Bu plan için fiyat bilgisi Koçluk yönetiminden alınır.</p> }
              <button type="button" class="mt-5 w-full rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white disabled:cursor-not-allowed disabled:opacity-50" [disabled]="plan.isContactOnly" (click)="selectPlan(plan)">{{ plan.isContactOnly ? 'Şu an çevrimiçi satın alınamıyor' : (selectedPlanId() === plan.id ? 'Seçildi' : 'Bu planı seç') }}</button>
            </article>
          }
        </div>
      </section>

      @if (selectedPlan() && !selectedPlan()?.isContactOnly) {
        <section class="grid gap-4 lg:grid-cols-2" aria-labelledby="payment-heading">
          <div class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
            <h2 id="payment-heading" class="font-semibold">Banka / EFT bilgileri</h2>
            @if (settings()) {
              <dl class="mt-3 space-y-2 text-sm"><div><dt class="text-slate-500">Hesap sahibi</dt><dd class="font-medium">{{ settings()?.accountHolder }}</dd></div><div><dt class="text-slate-500">Banka</dt><dd class="font-medium">{{ settings()?.bankName }}</dd></div><div><dt class="text-slate-500">IBAN</dt><dd class="break-all font-mono font-medium">{{ settings()?.iban }}</dd></div><div><dt class="text-slate-500">Ödeme açıklaması</dt><dd class="whitespace-pre-wrap">{{ settings()?.paymentInstructions || 'Ödeme açıklamasına işlem referansını yaz.' }}</dd></div></dl>
            } @else { <p class="mt-3 text-sm text-amber-800 dark:text-amber-200">Havale/EFT şu anda kullanılamıyor. Lütfen daha sonra tekrar dene.</p> }
          </div>
          <form class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" (ngSubmit)="submitRequest()">
            <h2 class="font-semibold">Ödeme bildirimi</h2>
            <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Seçilen plan: {{ selectedPlan()?.name }} · {{ selectedPlan()?.price | number:'1.2-2' }} {{ settings()?.currency || 'TRY' }}</p>
            <label class="mt-4 block text-sm">Banka işlem referansı<input class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="paymentReference" name="paymentReference" maxlength="120" required autocomplete="off" /></label>
            <label class="mt-3 block text-sm">Ödemeyi yapan kişi (isteğe bağlı)<input class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="payerName" name="payerName" maxlength="200" autocomplete="name" /></label>
            <label class="mt-3 block text-sm">Not (isteğe bağlı)<textarea class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="note" name="paymentNote" maxlength="1000" rows="3"></textarea></label>
            <label class="mt-4 flex items-start gap-2 text-sm"><input type="checkbox" [(ngModel)]="adultPayerDeclaration" name="adultPayerDeclaration" required />Ödemeyi yapan kişi olarak 18 yaş ve üzeri olduğumu beyan ediyorum. Bu beyan yaş doğrulaması değildir.</label>
            <button class="mt-4 w-full rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white disabled:opacity-50" type="submit" [disabled]="submitting() || !settings() || !adultPayerDeclaration">{{ submitting() ? 'Gönderiliyor…' : 'Ödeme yaptım, bildir' }}</button>
            <p class="mt-2 text-xs text-slate-500">Erişim, yönetici banka hareketini doğruladıktan sonra açılır. Bu ekran kart bilgisi almaz.</p>
          </form>
        </section>
      }

      <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="requests-heading">
        <h2 id="requests-heading" class="font-semibold">Ödeme bildirimlerim</h2>
        @for (request of requests(); track request.id) {
          <div class="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-3 text-sm dark:border-slate-800"><div><strong>{{ request.plan.name }}</strong><p class="text-slate-500">{{ request.paymentReference }} · {{ request.createdAt | date:'dd.MM.yyyy HH:mm' }}</p>@if (request.reviewNote) {<p class="text-slate-500">İnceleme notu: {{ request.reviewNote }}</p>}</div><span>{{ request.status === 'Pending' ? 'İnceleniyor' : request.status === 'Approved' ? 'Onaylandı' : request.status === 'Rejected' ? 'Reddedildi' : request.status }}</span></div>
        } @empty { <p class="mt-2 text-sm text-slate-500">Henüz ödeme bildirimin yok.</p> }
      </section>
    </main>
  `
})
export class CoachingSubscriptionComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly plans = signal<CoachingSubscriptionPlan[]>([]);
  readonly settings = signal<CoachingSubscriptionSettings | null>(null);
  readonly access = signal<CoachingSubscriptionAccess | null>(null);
  readonly requests = signal<CoachingBankTransferRequest[]>([]);
  readonly selectedPlanId = signal<string | null>(null);
  paymentReference = '';
  payerName = '';
  note = '';
  adultPayerDeclaration = false;

  ngOnInit(): void {
    this.service.getPublicSubscriptionPlans().subscribe({
      next: plans => { this.plans.set(plans.filter(plan => plan.audience === 'Individual' && plan.isActive && plan.isPublic)); this.loading.set(false); },
      error: () => { this.error.set('Koçluk planları yüklenemedi. Lütfen yeniden dene.'); this.loading.set(false); }
    });
    this.service.getPublicBankTransferSettings().subscribe({ next: settings => this.settings.set(settings), error: () => this.settings.set(null) });
    this.service.getMyCoachingSubscriptionAccess().subscribe({ next: access => this.access.set(access), error: () => this.error.set('Koçluk erişim durumun yüklenemedi.') });
    this.loadRequests();
  }

  selectedPlan(): CoachingSubscriptionPlan | null { return this.plans().find(plan => plan.id === this.selectedPlanId()) ?? null; }

  selectPlan(plan: CoachingSubscriptionPlan): void {
    if (plan.isContactOnly) return;
    this.selectedPlanId.set(plan.id);
    this.error.set(null);
    this.success.set(null);
  }

  submitRequest(): void {
    const plan = this.selectedPlan();
    const reference = this.paymentReference.trim();
    if (!plan || plan.isContactOnly || !this.settings()) { this.error.set('Ödeme bildirimi için satışa açık plan ve yayınlanmış banka bilgisi gereklidir.'); return; }
    if (!reference || reference.length > 120) { this.error.set('Banka işlem referansını 1–120 karakter arasında gir.'); return; }
    if (!this.adultPayerDeclaration) { this.error.set('Ödeme için 18 yaş ve üzeri olduğunuzu beyan etmeniz gerekir.'); return; }

    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.service.createMyCoachingBankTransferRequest({
      planId: plan.id,
      paymentReference: reference,
      payerName: this.payerName.trim() || null,
      note: this.note.trim() || null,
      adultPayerDeclaration: this.adultPayerDeclaration
    }).subscribe({
      next: request => {
        this.requests.update(items => [request, ...items.filter(item => item.id !== request.id)]);
        this.success.set(request.status === 'Pending'
          ? 'Ödeme bildirimin incelemeye alındı. Banka hareketi doğrulandıktan sonra erişimin açılacak.'
          : 'Ödeme bildirimin zaten kayıtlı.');
        this.paymentReference = '';
        this.payerName = '';
        this.note = '';
        this.adultPayerDeclaration = false;
        this.submitting.set(false);
      },
      error: response => {
        this.error.set(response?.error?.message ?? 'Ödeme bildirimi gönderilemedi. Bilgileri kontrol edip yeniden dene.');
        this.submitting.set(false);
      }
    });
  }

  private loadRequests(): void {
    this.service.getMyCoachingBankTransferRequests().subscribe({
      next: requests => this.requests.set(requests),
      error: () => this.error.set('Ödeme bildirim geçmişin yüklenemedi.')
    });
  }
}
