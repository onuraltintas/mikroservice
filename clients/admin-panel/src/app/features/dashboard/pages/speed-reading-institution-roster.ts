import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InstitutionService } from '../../../core/services/institution.service';
import {
  SpeedReadingInstitutionMember,
  SpeedReadingInstitutionRosterService
} from '../../../core/services/speed-reading-institution-roster.service';

@Component({
  selector: 'app-speed-reading-institution-roster',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="space-y-5">
      <header class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <a routerLink="/dashboard/identity/institutions" class="text-sm text-(--ui-brand-text)">← Kurumlar</a>
          <h1 class="mt-2 text-2xl font-bold text-(--ui-text)">{{ institutionName() }} · Hızlı Okuma üyeleri</h1>
          <p class="text-sm text-(--ui-text-muted)">Bu liste yalnız kurumun Hızlı Okuma üyeliklerini ve öğretmen–öğrenci bağlarını gösterir.</p>
        </div>
        <button type="button" class="rounded-lg border px-4 py-2 text-sm" (click)="load()" [disabled]="loading()">Yenile</button>
      </header>

      @if (error()) { <p role="alert" class="rounded-lg bg-(--ui-danger-soft) p-3 text-sm text-(--ui-danger)">{{ error() }}</p> }
      @if (notice()) { <p role="status" class="rounded-lg bg-(--ui-success-soft) p-3 text-sm text-(--ui-success)">{{ notice() }}</p> }

      <div class="flex flex-wrap gap-3 rounded-xl border bg-(--ui-surface) p-4">
        <select class="rounded-lg border p-2" [(ngModel)]="role" (ngModelChange)="applyFilters()" aria-label="Üye rolü">
          <option [ngValue]="1">Öğrenciler</option><option [ngValue]="2">Öğretmenler</option>
        </select>
        <input class="min-w-52 rounded-lg border p-2" [(ngModel)]="search"
          (keyup.enter)="applyFilters()" placeholder="Ad veya e-posta ara" aria-label="Üye ara" maxlength="100">
        <button type="button" class="rounded-lg bg-(--ui-brand) px-4 py-2 text-(--ui-brand-contrast)" (click)="applyFilters()">Ara</button>
      </div>

      @if (role === 1) {
        <div class="rounded-xl border bg-(--ui-surface) p-4">
          <label for="teacher-search" class="mb-2 block text-sm font-medium">Atama için öğretmen ara</label>
          <div class="flex gap-2">
            <input id="teacher-search" class="min-w-0 flex-1 rounded-lg border p-2"
              [(ngModel)]="teacherSearch" (keyup.enter)="loadTeachers()" maxlength="100" placeholder="Öğretmen adı veya e-postası">
            <button type="button" class="rounded-lg border px-4 py-2" (click)="loadTeachers()">Öğretmen bul</button>
          </div>
        </div>
      }

      <div class="overflow-x-auto rounded-xl border bg-(--ui-surface)">
        <table class="min-w-full text-left text-sm">
          <thead class="border-b bg-(--ui-surface-muted) text-(--ui-text-muted)"><tr>
            <th class="p-3">Üye</th><th class="p-3">Durum</th>
            <th class="p-3">{{ role === 1 ? 'Sınıf / Öğretmen' : 'Öğrenci sayısı' }}</th>
            <th class="p-3">İşlem</th>
          </tr></thead>
          <tbody>
            @for (member of members(); track member.userId) {
              <tr class="border-b last:border-0">
                <td class="p-3"><strong>{{ member.firstName }} {{ member.lastName }}</strong><span class="block text-xs text-(--ui-text-muted)">{{ member.email || 'E-posta yok' }}</span></td>
                <td class="p-3">{{ member.isActive ? 'Aktif' : 'Pasif' }}</td>
                <td class="p-3">
                  @if (role === 1) { {{ member.gradeLevel ? member.gradeLevel + '. sınıf' : 'Sınıf belirtilmedi' }} · {{ member.teacherName || 'Öğretmen atanmamış' }} }
                  @else { {{ member.studentCount }} öğrenci }
                </td>
                <td class="p-3">
                  <div class="flex flex-wrap items-center gap-2">
                    <button type="button" class="rounded border px-2 py-1 disabled:opacity-40" [disabled]="saving()"
                      (click)="toggleActive(member)">{{ member.isActive ? 'Pasifleştir' : 'Etkinleştir' }}</button>
                    @if (role === 1 && member.isActive) {
                      <select class="max-w-48 rounded border p-1" [(ngModel)]="teacherDraft[member.userId]"
                        [attr.aria-label]="member.firstName + ' için öğretmen seç'">
                        <option [ngValue]="null">Öğretmen seçin</option>
                        @for (teacher of teachers(); track teacher.userId) {
                          <option [ngValue]="teacher.userId">{{ teacher.firstName }} {{ teacher.lastName }}</option>
                        }
                      </select>
                      <button type="button" class="rounded border px-2 py-1 disabled:opacity-40"
                        [disabled]="saving() || !teacherDraft[member.userId]" (click)="assign(member)">Ata / değiştir</button>
                      @if (member.teacherUserId) {
                        <button type="button" class="rounded border px-2 py-1 disabled:opacity-40"
                          [disabled]="saving()" (click)="remove(member)">Bağı kaldır</button>
                      }
                    }
                  </div>
                </td>
              </tr>
            } @empty { <tr><td colspan="4" class="p-8 text-center text-(--ui-text-muted)">{{ loading() ? 'Yükleniyor…' : 'Üye bulunamadı.' }}</td></tr> }
          </tbody>
        </table>
      </div>
      <div class="flex items-center justify-between text-sm text-(--ui-text-muted)">
        <span>Toplam {{ totalCount() }} üye · Sayfa {{ page() }} / {{ totalPages() }}</span>
        <div class="flex gap-2"><button type="button" class="rounded border px-3 py-1 disabled:opacity-40" [disabled]="page() <= 1 || loading()" (click)="changePage(page() - 1)">Önceki</button>
          <button type="button" class="rounded border px-3 py-1 disabled:opacity-40" [disabled]="page() >= totalPages() || loading()" (click)="changePage(page() + 1)">Sonraki</button></div>
      </div>
    </section>
  `
})
export class SpeedReadingInstitutionRosterComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly institutions = inject(InstitutionService);
  private readonly roster = inject(SpeedReadingInstitutionRosterService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly institutionName = signal('Kurum');
  readonly members = signal<SpeedReadingInstitutionMember[]>([]);
  readonly teachers = signal<SpeedReadingInstitutionMember[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly totalPages = () => Math.max(1, Math.ceil(this.totalCount() / 25));
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  institutionId = '';
  role: 1 | 2 = 1;
  search = '';
  teacherSearch = '';
  teacherDraft: Record<string, string | null> = {};
  private memberRequestId = 0;
  private teacherRequestId = 0;

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.route.paramMap.subscribe(params => {
        this.institutionId = params.get('institutionId') || '';
        if (!this.institutionId) return;
        this.institutions.getById(this.institutionId).subscribe({
          next: institution => this.institutionName.set(institution.name),
          error: () => this.error.set('Kurum adı yüklenemedi.')
        });
        this.load();
        this.loadTeachers();
      });
    }
  }

  applyFilters() { this.page.set(1); this.load(); }
  changePage(page: number) { this.page.set(page); this.load(); }

  load() {
    if (!this.institutionId) return;
    const requestId = ++this.memberRequestId;
    this.loading.set(true);
    this.error.set(null);
    this.roster.getMembers(this.institutionId, this.page(), 25, this.role, this.search).subscribe({
      next: result => {
        if (requestId !== this.memberRequestId) return;
        this.members.set(result.items);
        this.totalCount.set(result.totalCount);
        for (const member of result.items) this.teacherDraft[member.userId] = member.teacherUserId;
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.memberRequestId) return;
        this.error.set('Hızlı Okuma üyeleri yüklenemedi.'); this.loading.set(false);
      }
    });
  }

  loadTeachers() {
    if (!this.institutionId) return;
    const requestId = ++this.teacherRequestId;
    this.roster.getMembers(this.institutionId, 1, 100, 2, this.teacherSearch).subscribe({
      next: result => {
        if (requestId === this.teacherRequestId) this.teachers.set(result.items.filter(item => item.isActive));
      },
      error: () => {
        if (requestId === this.teacherRequestId) this.error.set('Kurum öğretmenleri yüklenemedi.');
      }
    });
  }

  toggleActive(member: SpeedReadingInstitutionMember) {
    this.save(() => this.roster.setActive(this.institutionId, member, !member.isActive), 'Üyelik güncellendi.');
  }

  assign(member: SpeedReadingInstitutionMember) {
    const teacherId = this.teacherDraft[member.userId];
    if (!teacherId) return;
    this.save(() => this.roster.assignStudent(this.institutionId, teacherId, member.userId), 'Öğretmen bağlantısı güncellendi.');
  }

  remove(member: SpeedReadingInstitutionMember) {
    if (!member.teacherUserId) return;
    this.save(() => this.roster.removeStudent(this.institutionId, member.teacherUserId!, member.userId), 'Öğretmen bağlantısı kaldırıldı.');
  }

  private save(operation: () => ReturnType<SpeedReadingInstitutionRosterService['assignStudent']>, message: string) {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    operation().subscribe({
      next: () => { this.saving.set(false); this.notice.set(message); this.load(); },
      error: () => { this.saving.set(false); this.error.set('İşlem tamamlanamadı. Üyelik ve yetkileri kontrol edip tekrar deneyin.'); }
    });
  }
}
