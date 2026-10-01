import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, Input, OnChanges, OnInit, SimpleChanges, inject, signal } from '@angular/core';
import { StaffInvitationProduct, StaffInvitationsService, StaffPendingInvitation } from './staff-invitations.service';

@Component({
  selector: 'staff-pending-invitations',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './staff-pending-invitations.component.html',
  styleUrl: './staff-pending-invitations.component.scss',
})
export class StaffPendingInvitationsComponent implements OnInit, OnChanges {
  private readonly service = inject(StaffInvitationsService);
  private requestVersion = 0;

  @Input({ required: true }) product!: StaffInvitationProduct;
  @Input() refreshKey = 0;

  readonly invitations = signal<StaffPendingInvitation[]>([]);
  readonly isLoading = signal(true);
  readonly isCancelling = signal(false);
  readonly confirmingInvitationId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly statusMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['refreshKey'] && !changes['refreshKey'].firstChange)
      || (changes['product'] && !changes['product'].firstChange)) {
      this.load();
    }
  }

  load(): void {
    const requestVersion = ++this.requestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.statusMessage.set(null);
    this.confirmingInvitationId.set(null);
    this.service.getPending(this.product).subscribe({
      next: invitations => {
        if (requestVersion === this.requestVersion) this.invitations.set(invitations);
      },
      error: error => {
        if (requestVersion !== this.requestVersion) return;
        this.errorMessage.set(this.getApiMessage(error, 'Bekleyen davetler yüklenemedi. Lütfen tekrar deneyin.'));
        this.isLoading.set(false);
      },
      complete: () => {
        if (requestVersion === this.requestVersion) this.isLoading.set(false);
      },
    });
  }

  requestCancellation(invitation: StaffPendingInvitation): void {
    this.errorMessage.set(null);
    this.statusMessage.set(null);
    this.confirmingInvitationId.set(invitation.invitationId);
  }

  cancelConfirmation(): void {
    if (this.isCancelling()) return;
    this.confirmingInvitationId.set(null);
  }

  cancelInvitation(invitation: StaffPendingInvitation): void {
    if (this.isCancelling() || this.confirmingInvitationId() !== invitation.invitationId) return;
    this.isCancelling.set(true);
    this.errorMessage.set(null);
    this.service.cancel(this.product, invitation.invitationId).subscribe({
      next: () => {
        this.invitations.update(items => items.filter(item => item.invitationId !== invitation.invitationId));
        this.confirmingInvitationId.set(null);
        this.statusMessage.set('Davet iptal edildi. Önceden gönderilmiş e-posta ulaşabilir ancak bağlantı artık kabul edilemez.');
      },
      error: error => {
        this.isCancelling.set(false);
        this.errorMessage.set(this.getApiMessage(
          error,
          'Davet iptal edilemedi. Yenileyip durumunu kontrol edin.',
        ));
      },
      complete: () => this.isCancelling.set(false),
    });
  }

  productLabel(): string {
    return this.product === 'coaching' ? 'Koçluk' : 'Hızlı Okuma';
  }

  roleLabel(role: string): string {
    return role === 'Teacher' || role === 'TeacherToInstitution'
      ? 'Öğretmen daveti'
      : 'Öğrenci daveti';
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (!value || Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    }).format(date);
  }

  trackById(_: number, invitation: StaffPendingInvitation): string {
    return invitation.invitationId;
  }

  private getApiMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;
    if (error.status === 404) return 'Davet bulunamadı veya artık size ait değil.';
    if (error.status === 409) return 'Davet artık beklemede değil. Listeyi yenileyip durumunu kontrol edin.';
    if (!error.error || typeof error.error !== 'object') return fallback;
    const body = error.error as Record<string, unknown>;
    for (const candidate of [body, body['error'], body['Error']]) {
      if (!candidate || typeof candidate !== 'object') continue;
      const payload = candidate as Record<string, unknown>;
      for (const key of ['message', 'description', 'Message', 'Description']) {
        if (typeof payload[key] === 'string') return payload[key] as string;
      }
    }
    return fallback;
  }
}
