import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ParentLink, ParentRelationship, CoachingParentService } from './coaching-parent.service';
import { CoachingDatePipe } from '../coaching-labels';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { NotificationPreferencesApi, NotificationPreferencesComponent } from '../../../../../shared/notification-preferences.component';
import { StudentDataExportComponent } from '../pages/student-data-export.component';

/** Roadmap Y7: the parent's start page — linked children, waiting requests and the code form. */
@Component({
  selector: 'app-parent-home',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CoachingDatePipe, NotificationPreferencesComponent, StudentDataExportComponent],
  template: `
    <main class="space-y-6" aria-labelledby="parent-home-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text) ">Veli paneli</p>
        <h1 id="parent-home-title" class="mt-1 text-2xl font-bold">Çocuklarım</h1>
        <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">Çocuğunun çalışmasını, ödevlerini, seanslarını, denemelerini ve ücretlerini buradan izleyebilirsin. Bu ekran yalnızca okumak içindir; değişiklikleri öğrenci ve öğretmeni yapar.</p>
        @if (verified().length) {
          <a routerLink="/coaching-portal/messages" class="mt-3 inline-block rounded-lg bg-(--ui-brand) px-4 py-2 text-sm font-semibold text-white hover:bg-(--ui-brand-strong)">Öğretmenlere mesaj yaz</a>
        }
      </header>
      @if (error()) { <div role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</div> }
      @if (success()) { <div role="status" class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-200">{{ success() }}</div> }

      <section class="grid gap-4 md:grid-cols-2" aria-labelledby="children-heading">
        <h2 id="children-heading" class="sr-only">Bağlı çocuklar</h2>
        @for (link of verified(); track link.id) {
          <a [routerLink]="['/coaching-portal/parent/children', link.studentUserId]" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-(--ui-brand) dark:border-slate-800 dark:bg-slate-900">
            <strong class="text-lg">{{ link.studentName }}</strong>
            <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">{{ relationshipLabel(link.relationship) }} · {{ link.verifiedAt | coachingDate }} tarihinden beri bağlı</p>
            <span class="mt-3 inline-block text-sm font-medium text-(--ui-brand-text) ">Haftayı görüntüle →</span>
          </a>
        } @empty {
          @if (!loading()) { <p class="rounded-xl border border-slate-200 p-5 text-sm text-slate-600 dark:border-slate-800 dark:text-slate-300 md:col-span-2">Henüz bağlı bir çocuğun yok. Çocuğunun Koçluk hesabındaki Ayarlar sayfasından bir veli kodu oluşturmasını iste ve aşağıya gir.</p> }
        }
      </section>

      @if (pending().length) {
        <section class="rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm dark:border-amber-900/50 dark:bg-amber-950/20" aria-labelledby="pending-heading">
          <h2 id="pending-heading" class="font-semibold">Bekleyen istekler</h2>
          @for (link of pending(); track link.id) {
            <p class="mt-2"><strong>{{ link.studentName }}</strong> — öğrencinin onayını bekliyor ({{ link.requestedAt | coachingDate }}).</p>
          }
        </section>
      }

      <form class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" (ngSubmit)="submit()" aria-labelledby="link-heading">
        <h2 id="link-heading" class="font-semibold">Çocuğunla bağlan</h2>
        <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Kod 7 gün geçerlidir ve bir kez kullanılır. Gönderdikten sonra çocuğun isteği kendi hesabından onaylar.</p>
        <div class="mt-4 grid gap-3 sm:grid-cols-3">
          <label class="text-sm sm:col-span-2">Veli kodu<input class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 font-mono uppercase tracking-widest dark:border-slate-700" [(ngModel)]="code" name="code" maxlength="12" required autocomplete="off" placeholder="ABCD-2345" /></label>
          <label class="text-sm">Yakınlığın<select class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="relationship" name="relationship"><option value="Mother">Anne</option><option value="Father">Baba</option><option value="Guardian">Vasi</option></select></label>
        </div>
        <button type="submit" class="mt-4 rounded-lg bg-(--ui-brand) px-4 py-2 font-medium text-white disabled:opacity-50" [disabled]="submitting() || !code.trim()">{{ submitting() ? 'Gönderiliyor…' : 'Bağlantı isteği gönder' }}</button>
      </form>
      <edu-notification-preferences [api]="notificationApi" />
      <!-- Coaching audit K1: the parent downloads the data they created themselves (KVKK). -->
      <app-student-data-export audience="parent" />
    </main>
  `
})
export class ParentHomeComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  // Roadmap O8: the parent chooses which notifications also come by e-mail.
  readonly notificationApi = new NotificationPreferencesApi(inject(HttpClient), environment.apiUrl);
  readonly links = signal<ParentLink[]>([]);
  readonly verified = computed(() => this.links().filter(link => link.status === 'Verified'));
  readonly pending = computed(() => this.links().filter(link => link.status === 'Pending'));
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  code = '';
  relationship: ParentRelationship = 'Mother';

  ngOnInit(): void { this.load(); }

  relationshipLabel(value: string): string { return parentRelationshipLabel(value); }

  async submit(): Promise<void> {
    const code = this.code.trim();
    if (!code || this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.success.set(null);
    try {
      const link = await firstValueFrom(this.service.requestLink(code, this.relationship));
      this.code = '';
      this.success.set(`${link.studentName} için bağlantı isteğin gönderildi. Çocuğun onayladığında burada görünecek.`);
      this.load();
    } catch (response) {
      this.error.set(apiMessage(response, 'Bağlantı isteği gönderilemedi. Kodu kontrol edip yeniden dene.'));
    } finally { this.submitting.set(false); }
  }

  private load(): void {
    this.loading.set(true);
    this.service.getLinks().subscribe({
      next: links => { this.links.set(links); this.loading.set(false); },
      error: () => { this.error.set('Bağlantıların yüklenemedi.'); this.loading.set(false); }
    });
  }
}

export function parentRelationshipLabel(value: string): string {
  return ({ Mother: 'Anne', Father: 'Baba', Guardian: 'Vasi', Other: 'Diğer' } as Record<string, string>)[value] ?? value;
}

/** Message from an API error body ({ error: { description } } from Identity, { message } from Coaching). */
export function apiMessage(response: unknown, fallback: string): string {
  const body = (response as { error?: { message?: string; error?: { description?: string; message?: string } } })?.error;
  return body?.error?.description ?? body?.error?.message ?? body?.message ?? fallback;
}
