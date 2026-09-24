import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService, hasRole } from '../../../core/auth/auth.service';
import { CoachingInvitation, CoachingInvitationsService, SentCoachingInvitation } from '../../../core/services/coaching-invitations.service';

@Component({
  selector: 'app-coaching-invitations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 class="text-2xl font-bold">Koçluk davetleri</h1>
        <p class="mt-1 text-sm text-slate-500">Bağlantı kurmadan önce davetleri inceleyip kabul veya reddedebilirsiniz.</p>
      </div>
      @if (error()) { <p role="alert" class="rounded-lg border border-red-200 bg-red-50 p-3 text-red-700">{{ error() }}</p> }
      @if (success()) { <p role="status" class="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-emerald-700">{{ success() }}</p> }

      @if (isInstitution()) {
        <div class="grid gap-5 md:grid-cols-2">
          <form (ngSubmit)="inviteTeacher()" class="space-y-3 rounded-xl border bg-white p-5 dark:bg-slate-900">
            <h2 class="font-semibold">Öğretmen davet et</h2>
            <label class="block text-sm">Öğretmen e-postası
              <input [(ngModel)]="teacherEmail" name="teacherEmail" type="email" required maxlength="255" class="mt-1 w-full rounded-lg border px-3 py-2 dark:bg-slate-950" />
            </label>
            <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-50">Davet gönder</button>
          </form>
          <form (ngSubmit)="inviteStudent()" class="space-y-3 rounded-xl border bg-white p-5 dark:bg-slate-900">
            <h2 class="font-semibold">Öğrenci davet et</h2>
            <label class="block text-sm">Öğrenci e-postası
              <input [(ngModel)]="studentEmail" name="studentEmail" type="email" required maxlength="255" class="mt-1 w-full rounded-lg border px-3 py-2 dark:bg-slate-950" />
            </label>
            <p class="text-xs text-slate-500">Öğrenci daveti kabul ettikten sonra öğretmen atamasını Koçluk Yönetimi ekranından yapabilirsiniz.</p>
            <button type="submit" [disabled]="saving()" class="rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-50">Davet gönder</button>
          </form>
        </div>
      }

      @if (isInstitution()) {
        <div class="rounded-xl border bg-white p-5 dark:bg-slate-900">
          <h2 class="font-semibold">Gönderdiğim son 50 davet</h2>
          <div class="mt-3 space-y-2">
            @for (invitation of sentInvitations(); track invitation.id) {
              <div class="flex flex-wrap justify-between gap-2 rounded-lg border p-3 text-sm">
                <span>{{ invitation.inviteeEmail }} · {{ invitation.type === 'TeacherToInstitution' ? 'Öğretmen' : 'Öğrenci' }}</span>
                <span>{{ statusLabel(invitation.status) }} · {{ invitation.createdAt | date:'dd.MM.yyyy' }}</span>
              </div>
            } @empty { <p class="text-sm text-slate-500">Gönderilmiş davet bulunamadı.</p> }
          </div>
        </div>
      }

      <div class="rounded-xl border bg-white p-5 dark:bg-slate-900">
        <div class="flex items-center justify-between gap-3">
          <h2 class="font-semibold">Bana gelen bekleyen davetler</h2>
          <button type="button" (click)="load()" [disabled]="loading() || saving()" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Yenile</button>
        </div>
        @if (loading()) { <p class="mt-3 text-sm text-slate-500">Davetler yükleniyor…</p> }
        <div class="mt-3 space-y-3">
          @for (invitation of invitations(); track invitation.id) {
            <div class="rounded-lg border p-4" [class.border-indigo-500]="invitation.id === highlightedId">
              <p class="font-medium">{{ invitationLabel(invitation) }}</p>
              <p class="mt-1 text-sm text-slate-500">Gönderen: {{ invitation.inviterEmail }} · Son gün: {{ invitation.expiresAt | date:'dd.MM.yyyy' }}</p>
              @if (invitation.message) { <p class="mt-2 text-sm">{{ invitation.message }}</p> }
              <div class="mt-3 flex gap-2">
                <button type="button" (click)="accept(invitation.id)" [disabled]="saving()" class="rounded-lg bg-emerald-600 px-3 py-2 text-sm text-white disabled:opacity-50">Kabul et</button>
                <button type="button" (click)="reject(invitation.id)" [disabled]="saving()" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Reddet</button>
              </div>
            </div>
          } @empty { @if (!loading()) { <p class="text-sm text-slate-500">Bekleyen davetiniz yok.</p> } }
        </div>
      </div>
    </section>
  `
})
export class CoachingInvitationsComponent implements OnInit {
  private readonly service = inject(CoachingInvitationsService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly invitations = signal<CoachingInvitation[]>([]);
  readonly sentInvitations = signal<SentCoachingInvitation[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly highlightedId = this.route.snapshot.queryParamMap.get('id');
  teacherEmail = '';
  studentEmail = '';

  isInstitution() {
    const profile = this.auth.userProfile();
    return hasRole(profile, 'InstitutionAdmin') || hasRole(profile, 'InstitutionOwner');
  }

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.invitations.set(await firstValueFrom(this.service.getMyInvitations()));
      if (this.isInstitution()) {
        this.sentInvitations.set(await firstValueFrom(this.service.getSentInvitations()));
      }
    } catch {
      this.error.set('Davetler yüklenemedi. Lütfen yeniden deneyin.');
    } finally {
      this.loading.set(false);
    }
  }

  async inviteTeacher(): Promise<void> {
    const email = this.teacherEmail.trim();
    if (!this.isInstitution() || !this.validEmail(email)) return;
    await this.mutate(() => firstValueFrom(this.service.inviteTeacher(email)), 'Öğretmen daveti gönderildi.', 'Öğretmen daveti gönderilemedi.');
    if (this.success()) { this.teacherEmail = ''; await this.load(); }
  }

  async inviteStudent(): Promise<void> {
    const email = this.studentEmail.trim();
    if (!this.isInstitution() || !this.validEmail(email)) return;
    await this.mutate(() => firstValueFrom(this.service.inviteStudent(email)), 'Öğrenci daveti gönderildi.', 'Öğrenci daveti gönderilemedi.');
    if (this.success()) { this.studentEmail = ''; await this.load(); }
  }

  async accept(id: string): Promise<void> {
    if (!this.invitations().some(invitation => invitation.id === id)) {
      this.error.set('Bu davet hesabınızda bulunamadı.');
      return;
    }
    await this.mutate(() => firstValueFrom(this.service.accept(id)), 'Davet kabul edildi.', 'Davet kabul edilemedi.');
    if (this.success()) await this.load();
  }

  async reject(id: string): Promise<void> {
    if (!this.invitations().some(invitation => invitation.id === id)) return;
    await this.mutate(() => firstValueFrom(this.service.reject(id)), 'Davet reddedildi.', 'Davet reddedilemedi.');
    if (this.success()) await this.load();
  }

  invitationLabel(invitation: CoachingInvitation): string {
    switch (invitation.type) {
      case 'TeacherToInstitution': return 'Kuruma öğretmen olarak katılma daveti';
      case 'StudentToInstitution': return 'Kuruma öğrenci olarak katılma daveti';
      case 'StudentToTeacher': return 'Öğretmenle çalışma daveti';
      default: return 'Koçluk daveti';
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Pending': return 'Bekliyor';
      case 'Accepted': return 'Kabul edildi';
      case 'Rejected': return 'Reddedildi';
      case 'Expired': return 'Süresi doldu';
      default: return status;
    }
  }

  private validEmail(email: string): boolean {
    if (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return true;
    this.error.set('Geçerli bir e-posta adresi girin.');
    return false;
  }

  private async mutate(action: () => Promise<unknown>, success: string, failure: string): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      await action();
      this.success.set(success);
    } catch {
      this.error.set(failure);
    } finally {
      this.saving.set(false);
    }
  }
}
