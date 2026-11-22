import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { getAdminErrorMessage } from '../../../core/auth/admin-error-message';
import {
  DataSubjectRequestDto,
  DataSubjectRequestReviewDetailDto,
  DataSubjectRequestStatus,
  IdentityService,
  PersonalDataScope
} from '../../../core/services/identity.service';

@Component({
  selector: 'app-privacy-requests',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="space-y-6" aria-labelledby="privacy-title">
      <header><h1 id="privacy-title" class="text-2xl font-semibold">KVKK ilgili kişi talepleri</h1>
        <p class="text-sm text-(--ui-text-muted)">Silme taleplerini ve servis bazlı hukuki saklama değerlendirmelerini izleyin.</p></header>
      @if (error()) { <div class="rounded border border-(--ui-danger) bg-(--ui-danger-soft) p-3 text-(--ui-danger)" role="alert">{{ error() }}</div> }
      <div class="overflow-x-auto rounded border bg-(--ui-surface)">
        <table class="w-full text-left text-sm"><thead><tr><th class="p-3">Gönderim</th><th class="p-3">Kullanıcı</th><th class="p-3">Kapsam</th><th class="p-3">Durum</th><th class="p-3">İşlem</th></tr></thead>
          <tbody>@for (item of items(); track item.id) { <tr class="border-t"><td class="p-3">{{ item.submittedAt | date:'medium' }}</td><td class="p-3 font-mono">{{ item.requesterUserId }}</td><td class="p-3">{{ scopeLabel(item.scope) }}</td><td class="p-3">{{ item.status }}@if (item.failureReason) { <div class="mt-1 text-xs text-(--ui-danger)">{{ item.failureReason }}</div> }</td><td class="p-3"><button class="rounded bg-(--ui-brand) px-3 py-2 text-(--ui-brand-contrast)" (click)="select(item)">Değerlendirmeyi gör</button></td></tr> }
          @if (!loading() && items().length === 0) { <tr><td colspan="5" class="p-6 text-center text-(--ui-text-muted)">Talep bulunamadı.</td></tr> }</tbody></table>
      </div>
      @if (detail(); as selected) { <section class="rounded border bg-(--ui-surface) p-5" aria-live="polite">
        <h2 class="text-lg font-semibold">Silme hazırlık özeti</h2>
        <p class="mt-1 text-sm">Talep kapsamı: <strong>{{ scopeLabel(selected.request.scope) }}</strong></p>
        <p class="mt-2 font-medium" [class.text-(--ui-success)]="selected.assessment.isReadyForErasure" [class.text-(--ui-danger)]="!selected.assessment.isReadyForErasure">
          {{ selected.assessment.isReadyForErasure ? 'Silmeye hazır' : selected.assessment.hasBlockingLegalHold ? 'Hukuki saklama nedeniyle engelli' : 'Değerlendirme tamamlanmadı' }}
        </p>
        <p class="mt-1 text-sm">Etkilenen toplam kayıt: {{ selected.assessment.totalRecordCount }}</p>
        @if (selected.request.status === 'Approved' && selected.assessment.isReadyForErasure) {
          <button class="mt-3 rounded bg-(--ui-danger-soft) px-3 py-2 text-(--ui-danger) disabled:opacity-60"
            [disabled]="executing()" (click)="executeErasure(selected)">
            {{ executing() ? 'Silme başlatılıyor…' : 'Silmeyi başlat (geri alınamaz)' }}
          </button>
        }
        @if (selected.assessment.missingServices.length) { <p class="mt-2 text-(--ui-warning)">Yanıt beklenen servisler: {{ selected.assessment.missingServices.join(', ') }}</p> }
        <ul class="mt-4 space-y-2">@for (service of selected.assessment.services; track service.serviceName) { <li class="rounded bg-(--ui-surface-muted) p-3"><strong>{{ service.serviceName }}</strong> — {{ service.totalRecordCount }} kayıt — {{ service.hasActiveLegalHold ? 'Hukuki saklama var' : service.canProceed ? 'İşleme uygun' : 'Engelli' }}
          @if (recordCountEntries(service.recordCounts).length) { <ul class="mt-2 list-disc pl-5 text-xs text-(--ui-text)">@for (entry of recordCountEntries(service.recordCounts); track entry[0]) { <li>{{ entry[0] }}: {{ entry[1] }}</li> }</ul> }
        </li> }</ul>
      </section> }
    </section>`
})
export class PrivacyRequestsComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  readonly items = signal<DataSubjectRequestDto[]>([]);
  readonly detail = signal<DataSubjectRequestReviewDetailDto | null>(null);
  readonly loading = signal(false);
  readonly executing = signal(false);
  readonly error = signal('');

  scopeLabel(scope: PersonalDataScope): string {
    return ({ Account: 'Tüm Hesap', Coaching: 'Koçluk', SpeedReading: 'Hızlı Okuma' })[scope];
  }

  recordCountEntries(recordCounts: Record<string, number>): [string, number][] {
    return Object.entries(recordCounts).sort(([left], [right]) => left.localeCompare(right));
  }

  executeErasure(selected: DataSubjectRequestReviewDetailDto): void {
    this.executing.set(true); this.error.set('');
    this.identity.executeDataSubjectRequestErasure(selected.request.id)
      .pipe(finalize(() => this.executing.set(false)))
      .subscribe({
        next: request => this.detail.update(current => current ? { ...current, request } : current),
        error: error => this.error.set(getAdminErrorMessage(error, 'Silme işlemi başlatılamadı.'))
      });
  }

  ngOnInit(): void { this.load(); }
  load(status?: DataSubjectRequestStatus): void {
    this.loading.set(true); this.error.set('');
    this.identity.getDataSubjectRequests(status).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: page => this.items.set(page.items),
      error: error => this.error.set(getAdminErrorMessage(error, 'KVKK talepleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.'))
    });
  }
  select(item: DataSubjectRequestDto): void {
    this.error.set('');
    this.identity.getDataSubjectRequestDetail(item.id).subscribe({
      next: detail => this.detail.set(detail),
      error: error => this.error.set(getAdminErrorMessage(error, 'Değerlendirme ayrıntısı yüklenemedi. Sayfayı yenileyip yeniden deneyin.'))
    });
  }
}
