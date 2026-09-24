import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import {
  CoachingAdminReadScope, CoachingAdminService, CoachingAdminStudentDetail, CoachingStudentRosterItem,
  CoachingStudentRosterPage, CoachingTeacherRosterItem, CoachingTeacherRosterPage,
  TeacherCoachingAnalytics
} from '../../../core/services/coaching-admin.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';

@Component({
  selector: 'app-coaching-people',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-5">
      <div>
        <h1 class="text-2xl font-bold dark:text-white">Koçluk {{ kind === 'students' ? 'Öğrencileri' : 'Öğretmenleri' }}</h1>
        <p class="text-sm text-gray-500">İsim ve hesap bilgileri Identity, sonuçlar yalnızca Koçluk servisinden gelir.</p>
      </div>
      @if (scope()?.isGlobal) {
      <div class="rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
        <label class="block text-sm">Kurum ara
          <span class="mt-1 flex gap-2"><input [(ngModel)]="institutionSearch" (keyup.enter)="loadInstitutions()" maxlength="100" class="w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="Kurum adı" /><button type="button" class="rounded border px-3" (click)="loadInstitutions()">Ara</button></span>
        </label>
        <label class="mt-3 block text-sm">Kurum
          <select [(ngModel)]="institutionId" (ngModelChange)="onInstitutionChange()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-900">
            <option value="">Kurum seçin</option>
            @for (institution of institutions(); track institution.id) { <option [value]="institution.id">{{ institution.name }}</option> }
          </select>
        </label>
      </div>
      } @else if (scope()) { <p class="text-sm text-gray-500">Yalnızca kurumunuzun koçluk verileri gösterilir.</p> }

      @if (institutionId) {
        @if (detailId) {
          <button type="button" class="rounded border px-3 py-2 text-sm" (click)="backToList()">← Listeye dön</button>
          @if (loading()) { <p role="status">Ayrıntılar yükleniyor…</p> }
          @if (error()) { <p role="alert" class="text-red-700">{{ error() }}</p> }
          @if (kind === 'students' && selectedStudent(); as student) {
            <article class="space-y-4 rounded-xl border bg-white p-5 dark:border-gray-700 dark:bg-gray-800">
              <div><h2 class="text-lg font-semibold">{{ student.firstName }} {{ student.lastName }}</h2><p class="text-sm text-gray-500">{{ student.email }} · {{ student.gradeLevel ? student.gradeLevel + '. sınıf' : 'Sınıf yok' }} · {{ student.teacherName || 'Öğretmen atanmamış' }}</p></div>
              @if (studentDetail(); as detail) {
                <div class="grid gap-3 sm:grid-cols-4 text-sm"><p>Ödev: {{ detail.submittedAssignments }}/{{ detail.totalAssignments }} teslim</p><p>Sınav sonucu: {{ detail.totalExams }}</p><p>Seans: {{ detail.totalSessions }}</p><p>Hedef: {{ detail.totalGoals }}</p></div>
                <div class="grid gap-4 lg:grid-cols-2 text-sm">
                  <div><h3 class="font-semibold">Son ödevler</h3>@for (item of detail.assignments; track item.id) { <p class="mt-2">{{ item.title }} · {{ item.status }} · {{ item.score ?? 'Not yok' }}</p> } @empty { <p class="mt-2 text-gray-500">Ödev yok.</p> }</div>
                  <div><h3 class="font-semibold">Sınav sonuçları</h3>@for (item of detail.exams; track item.id) { <p class="mt-2">{{ item.title }} · {{ item.score }}/{{ item.maxScore }}</p> } @empty { <p class="mt-2 text-gray-500">Sonuç yok.</p> }</div>
                </div>
                <p class="text-xs text-gray-500">Kurum kimliği taşımayan geçmiş hedefler, kurum yöneticisi ayrıntısında gösterilmez.</p>
              }
            </article>
          }
          @if (kind === 'teachers' && selectedTeacher(); as teacher) {
            <article class="space-y-4 rounded-xl border bg-white p-5 dark:border-gray-700 dark:bg-gray-800">
              <div><h2 class="text-lg font-semibold">{{ teacher.firstName }} {{ teacher.lastName }}</h2><p class="text-sm text-gray-500">{{ teacher.email }}</p></div>
              @if (teacherAnalytics(); as analytics) {
                <div class="grid gap-3 sm:grid-cols-3 text-sm"><p>Ödev, son 30 gün: {{ analytics.currentPeriod.assignments }} · önceki dönem: {{ analytics.previousPeriod.assignments }}</p><p>Sınav: {{ analytics.currentPeriod.exams }} · önceki: {{ analytics.previousPeriod.exams }}</p><p>Seans: {{ analytics.currentPeriod.sessions }} · önceki: {{ analytics.previousPeriod.sessions }}</p></div>
                <div class="text-sm"><h3 class="font-semibold">Sınav sonuç dağılımı (son 30 gün)</h3><p>Düşük (&lt;%50): {{ analytics.lowResults }} · Orta (%50–79): {{ analytics.mediumResults }} · Yüksek (≥%80): {{ analytics.highResults }}</p></div>
              }
              <div class="text-sm"><h3 class="font-semibold">Atanmış öğrenciler</h3>
                @for (student of teacherStudents()?.students; track student.userId) { <p class="mt-2">{{ student.firstName }} {{ student.lastName }} · {{ student.email }}</p> } @empty { <p class="mt-2 text-gray-500">Atanmış öğrenci yok.</p> }
                <div class="mt-3 flex items-center gap-3"><span>Toplam {{ teacherStudents()?.totalCount ?? 0 }}</span><button type="button" class="rounded border px-2 py-1" [disabled]="teacherStudentPage <= 1" (click)="loadTeacherStudents(teacherStudentPage - 1)">Önceki</button><button type="button" class="rounded border px-2 py-1" [disabled]="teacherStudentPage * 25 >= (teacherStudents()?.totalCount ?? 0)" (click)="loadTeacherStudents(teacherStudentPage + 1)">Sonraki</button></div>
              </div>
            </article>
          }
        } @else {
          <div class="rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
            <label class="block text-sm">{{ kind === 'students' ? 'Öğrenci' : 'Öğretmen' }} ara
              <span class="mt-1 flex gap-2"><input [(ngModel)]="search" (keyup.enter)="loadPage()" maxlength="100" class="w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="Ad veya e-posta" /><button type="button" class="rounded border px-3" (click)="loadPage()">Ara</button></span>
            </label>
            @if (loading()) { <p role="status" class="mt-3">Yükleniyor…</p> }
            @if (error()) { <p role="alert" class="mt-3 text-red-700">{{ error() }}</p> }
            <div class="mt-4 overflow-x-auto"><table class="min-w-full text-left text-sm"><thead><tr><th class="py-2">Ad</th><th>E-posta</th><th>{{ kind === 'students' ? 'Öğretmen' : 'Kurum' }}</th><th></th></tr></thead><tbody>
              @if (kind === 'students') {
                @for (student of studentPage()?.students; track student.userId) { <tr class="border-t"><td class="py-2">{{ student.firstName }} {{ student.lastName }}</td><td>{{ student.email }}</td><td>{{ student.teacherName || 'Atanmamış' }}</td><td><button type="button" class="text-indigo-700 underline" (click)="openDetail(student.userId)">Ayrıntı</button></td></tr> }
              } @else {
                @for (teacher of teacherPage()?.teachers; track teacher.userId) { <tr class="border-t"><td class="py-2">{{ teacher.firstName }} {{ teacher.lastName }}</td><td>{{ teacher.email }}</td><td>{{ selectedInstitutionName() }}</td><td><button type="button" class="text-indigo-700 underline" (click)="openDetail(teacher.userId)">Ayrıntı</button></td></tr> }
              }
            </tbody></table></div>
            <div class="mt-3 flex items-center gap-3 text-sm"><span>Toplam {{ totalCount() }} · {{ pageNumber }}. sayfa</span><button type="button" class="rounded border px-2 py-1" [disabled]="pageNumber <= 1 || loading()" (click)="loadPage(pageNumber - 1)">Önceki</button><button type="button" class="rounded border px-2 py-1" [disabled]="pageNumber * 25 >= totalCount() || loading()" (click)="loadPage(pageNumber + 1)">Sonraki</button></div>
          </div>
        }
      } @else { <p class="text-sm text-gray-500">Liste için kurum seçin.</p> }
    </section>
  `
})
export class CoachingPeopleComponent implements OnInit {
  private readonly service = inject(CoachingAdminService);
  private readonly institutionService = inject(InstitutionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  readonly kind = this.route.snapshot.data['kind'] as 'students' | 'teachers';
  readonly detailId = this.route.snapshot.paramMap.get('id');
  readonly institutions = signal<InstitutionDto[]>([]);
  readonly scope = signal<CoachingAdminReadScope | null>(null);
  readonly studentPage = signal<CoachingStudentRosterPage | null>(null);
  readonly teacherPage = signal<CoachingTeacherRosterPage | null>(null);
  readonly selectedStudent = signal<CoachingStudentRosterItem | null>(null);
  readonly selectedTeacher = signal<CoachingTeacherRosterItem | null>(null);
  readonly studentDetail = signal<CoachingAdminStudentDetail | null>(null);
  readonly teacherAnalytics = signal<TeacherCoachingAnalytics | null>(null);
  readonly teacherStudents = signal<CoachingStudentRosterPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private requestId = 0;
  private teacherStudentsRequestId = 0;
  institutionId = this.route.snapshot.queryParamMap.get('institutionId') ?? '';
  institutionSearch = '';
  search = '';
  pageNumber = 1;
  teacherStudentPage = 1;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.service.getReadScope().subscribe({
      next: scope => {
        this.scope.set(scope);
        if (scope.isGlobal) this.loadInstitutions();
        else this.institutionId = scope.institutionId ?? '';
        if (this.institutionId) {
          if (this.detailId) this.loadDetail(this.detailId);
          else this.loadPage();
        }
      },
      error: () => this.error.set('Koçluk yönetim kapsamı yüklenemedi.')
    });
  }

  loadInstitutions(): void {
    this.institutionService.getAll(1, 100, this.institutionSearch.trim(), true).subscribe({
      next: page => this.institutions.set(page.items ?? []),
      error: () => this.error.set('Kurumlar yüklenemedi.')
    });
  }

  selectedInstitutionName(): string {
    return this.institutions().find(item => item.id === this.institutionId)?.name ?? 'Seçili kurum';
  }

  totalCount(): number {
    return this.kind === 'students' ? this.studentPage()?.totalCount ?? 0 : this.teacherPage()?.totalCount ?? 0;
  }

  onInstitutionChange(): void {
    ++this.requestId;
    ++this.teacherStudentsRequestId;
    this.studentPage.set(null);
    this.teacherPage.set(null);
    this.selectedStudent.set(null);
    this.selectedTeacher.set(null);
    this.studentDetail.set(null);
    this.teacherAnalytics.set(null);
    this.teacherStudents.set(null);
    this.loading.set(false);
    this.error.set(null);
    if (this.detailId) this.backToList();
    else if (this.institutionId) this.loadPage();
  }

  loadPage(page = 1): void {
    if (!this.institutionId) return;
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    const result: Observable<CoachingStudentRosterPage | CoachingTeacherRosterPage> = this.kind === 'students'
      ? this.service.getStudentRoster(this.institutionId, page, this.search)
      : this.service.getTeacherRoster(this.institutionId, page, this.search);
    result.subscribe({
      next: value => {
        if (requestId !== this.requestId) return;
        this.pageNumber = page;
        if (this.kind === 'students') this.studentPage.set(value as CoachingStudentRosterPage);
        else this.teacherPage.set(value as CoachingTeacherRosterPage);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.error.set('Liste yüklenemedi.');
        this.loading.set(false);
      }
    });
  }

  loadDetail(id: string): void {
    if (!this.institutionId) return;
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    if (this.kind === 'students') {
      forkJoin({
        roster: this.service.getStudentRoster(this.institutionId, 1, id),
        detail: this.service.getStudentDetail(id)
      }).subscribe({
        next: result => {
          if (requestId !== this.requestId) return;
          this.selectedStudent.set(result.roster.students.find(item => item.userId === id) ?? null);
          this.studentDetail.set(this.selectedStudent() ? result.detail : null);
          if (!this.selectedStudent()) this.error.set('Öğrenci bu kurumda bulunamadı.');
          this.loading.set(false);
        },
        error: () => { if (requestId === this.requestId) { this.error.set('Öğrenci ayrıntısı yüklenemedi.'); this.loading.set(false); } }
      });
    } else {
      forkJoin({
        roster: this.service.getTeacherRoster(this.institutionId, 1, id),
        analytics: this.service.getTeacherAnalytics(id),
        students: this.service.getStudentRoster(this.institutionId, 1, '', id)
      }).subscribe({
        next: result => {
          if (requestId !== this.requestId) return;
          this.selectedTeacher.set(result.roster.teachers.find(item => item.userId === id) ?? null);
          this.teacherAnalytics.set(this.selectedTeacher() ? result.analytics : null);
          this.teacherStudents.set(this.selectedTeacher() ? result.students : null);
          if (!this.selectedTeacher()) this.error.set('Öğretmen bu kurumda bulunamadı.');
          this.loading.set(false);
        },
        error: () => { if (requestId === this.requestId) { this.error.set('Öğretmen ayrıntısı yüklenemedi.'); this.loading.set(false); } }
      });
    }
  }

  loadTeacherStudents(page: number): void {
    if (!this.institutionId || !this.detailId) return;
    const requestId = ++this.teacherStudentsRequestId;
    this.service.getStudentRoster(this.institutionId, page, '', this.detailId).subscribe({
      next: result => { if (requestId === this.teacherStudentsRequestId) { this.teacherStudentPage = page; this.teacherStudents.set(result); } },
      error: () => { if (requestId === this.teacherStudentsRequestId) this.error.set('Öğrenci listesi yüklenemedi.'); }
    });
  }

  openDetail(id: string): void {
    void this.router.navigate(['/dashboard/coaching', this.kind, id], { queryParams: { institutionId: this.institutionId } });
  }

  backToList(): void {
    void this.router.navigate(['/dashboard/coaching', this.kind], { queryParams: { institutionId: this.institutionId } });
  }
}
