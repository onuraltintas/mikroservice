import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  CoachingAdminReadScope, CoachingAdminService, CoachingAdminStudentDetail, CoachingAdminStudentHistoryPage,
  CoachingStudentHistoryType, CoachingStudentRosterItem,
  CoachingStudentRosterPage, CoachingTeacherRosterItem, CoachingTeacherRosterPage,
  TeacherCoachingAnalytics
} from '../../../core/services/coaching-admin.service';
import { CoachingInstitutionMembershipService } from '../../../core/services/coaching-institution-membership.service';
import { InstitutionDto, InstitutionService } from '../../../core/services/institution.service';
import { ToasterService } from '../../../core/services/toaster.service';

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
              @if (canManageMemberships()) {
                <section class="space-y-3 rounded-lg bg-gray-50 p-4 text-sm dark:bg-gray-900" aria-labelledby="student-membership-heading">
                  <h3 id="student-membership-heading" class="font-semibold">Kurum üyeliğini yönet</h3>
                  <label class="block">Sınıf seviyesi
                    <select [(ngModel)]="editingGradeLevel" name="editingGradeLevel" [disabled]="savingMembership()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-800">
                      <option [ngValue]="null">Sınıf seçin</option>
                      @for (grade of gradeLevels; track grade) { <option [ngValue]="grade">{{ grade }}. sınıf</option> }
                    </select>
                  </label>
                  <label class="block">Öğretmen ara
                    <span class="mt-1 flex gap-2"><input [(ngModel)]="teacherLookupSearch" maxlength="100" [disabled]="savingMembership()" class="w-full rounded border px-3 py-2 dark:bg-gray-800" placeholder="Ad veya e-posta" /><button type="button" class="rounded border px-3" [disabled]="teacherLookupLoading() || savingMembership()" (click)="searchInstitutionTeachers()">{{ teacherLookupLoading() ? 'Aranıyor…' : 'Ara' }}</button></span>
                  </label>
                  @if (teacherLookupError()) { <p role="alert" class="text-red-700">{{ teacherLookupError() }}</p> }
                  <label class="block">Atanan öğretmen
                    <select [(ngModel)]="editingTeacherUserId" name="editingTeacherUserId" [disabled]="savingMembership()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-800">
                      <option value="">Öğretmen atama</option>
                      @if (student.teacherUserId && !hasTeacherLookupResult(student.teacherUserId)) { <option [value]="student.teacherUserId">{{ student.teacherName || 'Mevcut öğretmen' }}</option> }
                      @for (teacher of teacherLookupResults(); track teacher.userId) { <option [value]="teacher.userId">{{ teacher.firstName }} {{ teacher.lastName }} · {{ teacher.email }}</option> }
                    </select>
                  </label>
                  <button type="button" class="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-50" [disabled]="savingMembership() || editingGradeLevel === null" (click)="saveStudentMembership()">{{ savingMembership() ? 'Kaydediliyor…' : 'Sınıf ve atamayı kaydet' }}</button>
                  <p class="text-xs text-gray-500">Bu işlem üyeliği ve öğretmen atamasını günceller; hesabı veya koçluk geçmişini silmez.</p>
                </section>
              }
              @if (studentDetail(); as detail) {
                <div class="grid gap-3 sm:grid-cols-4 text-sm"><p>Ödev: {{ detail.submittedAssignments }}/{{ detail.totalAssignments }} teslim</p><p>Sınav sonucu: {{ detail.totalExams }}</p><p>Seans: {{ detail.totalSessions }}</p><p>Hedef: {{ detail.totalGoals }}</p></div>
                <div class="grid gap-4 lg:grid-cols-2 text-sm">
                  <div><h3 class="font-semibold">Son ödevler</h3>@for (item of detail.assignments; track item.id) { <p class="mt-2">{{ item.title }} · {{ item.status }} · {{ item.score ?? 'Not yok' }}</p> } @empty { <p class="mt-2 text-gray-500">Ödev yok.</p> }</div>
                  <div><h3 class="font-semibold">Son sınav sonuçları</h3>@for (item of detail.exams; track item.id) { <p class="mt-2">{{ item.title }} · {{ item.score }}/{{ item.maxScore }}</p> } @empty { <p class="mt-2 text-gray-500">Sonuç yok.</p> }</div>
                </div>
                <p class="text-xs text-gray-500">Bu öğrenciye erişim aktif kurum üyeliğiyle doğrulanır; ayrıntı kurum değişikliği dâhil tüm koçluk geçmişini kapsar.</p>
              }
            </article>
            <section class="space-y-3 rounded-xl border bg-white p-5 dark:border-gray-700 dark:bg-gray-800" aria-labelledby="student-history-heading">
              <div class="flex flex-wrap items-end justify-between gap-3">
                <div><h3 id="student-history-heading" class="font-semibold">Tüm koçluk geçmişi</h3><p class="text-xs text-gray-500">Kurum değişikliği öncesi kayıtlar da dâhil, sayfalı geçmiş.</p></div>
                <label class="text-sm">Kayıt türü
                  <select [ngModel]="studentHistoryType" (ngModelChange)="selectStudentHistoryType($event)" class="mt-1 rounded border px-3 py-2 dark:bg-gray-900">
                    @for (option of historyTypeOptions; track option.value) { <option [ngValue]="option.value">{{ option.label }}</option> }
                  </select>
                </label>
              </div>
              @if (studentHistoryLoading()) { <p role="status">Geçmiş yükleniyor…</p> }
              @if (studentHistory(); as history) {
                <div class="divide-y text-sm">
                  @for (item of history.items; track item.id) {
                    <article class="py-3"><div class="flex flex-wrap justify-between gap-2"><strong>{{ item.title }}</strong><time>{{ item.eventDate | date:'dd.MM.yyyy HH:mm' }}</time></div>
                      <p class="mt-1 text-gray-500">{{ item.status }}</p>
                      @if (item.category) { <p class="text-gray-500">Kategori: {{ item.category }}</p> }
                      @if (item.progress !== null && item.progress !== undefined) { <p class="text-gray-500">İlerleme: %{{ item.progress }}</p> }
                      @if (item.score !== null && item.score !== undefined) { <p class="text-gray-500">Puan: {{ item.score }} / {{ item.maxScore ?? '—' }}</p> }
                    </article>
                  } @empty { <p class="py-3 text-gray-500">Bu türde geçmiş kayıt yok.</p> }
                </div>
                <div class="flex items-center gap-3 text-sm"><span>{{ history.totalCount }} kayıt · {{ studentHistoryPage }}. sayfa</span><button type="button" class="rounded border px-2 py-1" [disabled]="studentHistoryPage <= 1 || studentHistoryLoading()" (click)="loadStudentHistory(studentHistoryPage - 1)">Önceki</button><button type="button" class="rounded border px-2 py-1" [disabled]="studentHistoryPage * 25 >= history.totalCount || studentHistoryLoading()" (click)="loadStudentHistory(studentHistoryPage + 1)">Sonraki</button></div>
              }
            </section>
          }
          @if (kind === 'teachers' && selectedTeacher(); as teacher) {
            <article class="space-y-4 rounded-xl border bg-white p-5 dark:border-gray-700 dark:bg-gray-800">
              <div><h2 class="text-lg font-semibold">{{ teacher.firstName }} {{ teacher.lastName }}</h2><p class="text-sm text-gray-500">{{ teacher.email }}</p></div>
              @if (canManageMemberships()) { <button type="button" class="rounded border border-red-300 px-3 py-2 text-sm text-red-700 disabled:opacity-50" [disabled]="savingMembership()" (click)="removeTeacherFromInstitution(teacher)">Öğretmeni kurumdan çıkar</button> }
              @if (teacherAnalytics(); as analytics) {
                <div class="grid gap-3 sm:grid-cols-3 text-sm"><p>Ödev, son 30 gün: {{ analytics.currentPeriod.assignments }} · önceki dönem: {{ analytics.previousPeriod.assignments }}</p><p>Sınav: {{ analytics.currentPeriod.exams }} · önceki: {{ analytics.previousPeriod.exams }}</p><p>Seans: {{ analytics.currentPeriod.sessions }} · önceki: {{ analytics.previousPeriod.sessions }}</p></div>
                <div class="text-sm"><h3 class="font-semibold">Sınav sonuç dağılımı (son 30 gün)</h3><p>Düşük (&lt;%50): {{ analytics.lowResults }} · Orta (%50–79): {{ analytics.mediumResults }} · Yüksek (≥%80): {{ analytics.highResults }}</p></div>
              }
              <div class="text-sm"><h3 class="font-semibold">Atanmış öğrenciler</h3>
                <label class="mt-2 block">Öğrenci ara
                  <span class="mt-1 flex gap-2"><input [(ngModel)]="teacherStudentSearch" (keyup.enter)="loadTeacherStudents(1)" maxlength="100" class="w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="Ad veya e-posta" /><button type="button" class="rounded border px-3" (click)="loadTeacherStudents(1)">Ara</button></span>
                </label>
                @for (student of teacherStudents()?.students; track student.userId) { <p class="mt-2">{{ student.firstName }} {{ student.lastName }} · {{ student.email }}</p> } @empty { <p class="mt-2 text-gray-500">Atanmış öğrenci yok.</p> }
                <div class="mt-3 flex items-center gap-3"><span>Toplam {{ teacherStudents()?.totalCount ?? 0 }}</span><button type="button" class="rounded border px-2 py-1" [disabled]="teacherStudentPage <= 1" (click)="loadTeacherStudents(teacherStudentPage - 1)">Önceki</button><button type="button" class="rounded border px-2 py-1" [disabled]="teacherStudentPage * 25 >= (teacherStudents()?.totalCount ?? 0)" (click)="loadTeacherStudents(teacherStudentPage + 1)">Sonraki</button></div>
              </div>
            </article>
          }
        } @else {
          @if (canManageMemberships() && kind === 'teachers') {
            <form class="flex flex-wrap items-end gap-3 rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800" (ngSubmit)="sendTeacherInvitation()">
              <label class="min-w-64 flex-1 text-sm">Öğretmen e-posta adresi
                <input type="email" name="teacherInviteEmail" [(ngModel)]="teacherInviteEmail" required maxlength="254" [disabled]="savingMembership()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="ogretmen@ornek.com" />
              </label>
              <button type="submit" [disabled]="savingMembership()" class="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-50">Öğretmen davet et</button>
              <p class="basis-full text-xs text-gray-500">Davet e-postasına katılım bağlantısı gönderilir; öğretmen kabul edince kurum listesine eklenir.</p>
            </form>
          }
          @if (canManageMemberships() && kind === 'students') {
            <section class="space-y-3 rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
              <h2 class="font-semibold">Öğrenci davet et</h2>
              <form class="flex flex-wrap items-end gap-3" (ngSubmit)="sendStudentInvitation()">
                <label class="min-w-64 flex-1 text-sm">Öğrenci e-posta adresi
                  <input type="email" name="studentInviteEmail" [(ngModel)]="studentInviteEmail" required maxlength="254" [disabled]="savingMembership()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="ogrenci@ornek.com" />
                </label>
                <label class="min-w-64 flex-1 text-sm">Davet sırasında öğretmen ata (isteğe bağlı)
                  <select [(ngModel)]="studentInviteTeacherUserId" name="studentInviteTeacherUserId" [disabled]="savingMembership()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-900">
                    <option value="">Şimdilik atama yapma</option>
                    @for (teacher of teacherLookupResults(); track teacher.userId) { <option [value]="teacher.userId">{{ teacher.firstName }} {{ teacher.lastName }} · {{ teacher.email }}</option> }
                  </select>
                </label>
                <button type="submit" [disabled]="savingMembership()" class="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-50">Öğrenci davet et</button>
              </form>
              <label class="block text-sm">Atanabilecek öğretmen ara
                <span class="mt-1 flex gap-2"><input [(ngModel)]="teacherLookupSearch" maxlength="100" class="w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="Ad veya e-posta" /><button type="button" class="rounded border px-3" [disabled]="teacherLookupLoading()" (click)="searchInstitutionTeachers()">{{ teacherLookupLoading() ? 'Aranıyor…' : 'Ara' }}</button></span>
              </label>
              @if (teacherLookupError()) { <p role="alert" class="text-sm text-red-700">{{ teacherLookupError() }}</p> }
              <p class="text-xs text-gray-500">Öğrenci daveti kabul edildikten sonra kurum listesinde görünür. Davet mevcut hesabı silmez veya değiştirmez.</p>
            </section>
          }
          <div class="rounded-xl border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
            <label class="block text-sm">{{ kind === 'students' ? 'Öğrenci' : 'Öğretmen' }} ara
              <span class="mt-1 flex gap-2"><input [(ngModel)]="search" (keyup.enter)="loadPage()" maxlength="100" class="w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="Ad veya e-posta" /><button type="button" class="rounded border px-3" (click)="loadPage()">Ara</button></span>
            </label>
            @if (kind === 'students') {
              <label class="mt-3 block text-sm">Sınıf seviyesi
                <select [(ngModel)]="gradeLevelFilter" (ngModelChange)="loadPage()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-900">
                  <option [ngValue]="null">Tüm sınıflar</option>
                  @for (grade of gradeLevels; track grade) { <option [ngValue]="grade">{{ grade }}. sınıf</option> }
                </select>
              </label>
              <label class="mt-3 block text-sm">Öğretmen ara ve filtrele
                <span class="mt-1 flex gap-2"><input [(ngModel)]="teacherLookupSearch" (keyup.enter)="searchInstitutionTeachers()" maxlength="100" class="w-full rounded border px-3 py-2 dark:bg-gray-900" placeholder="Ad veya e-posta" /><button type="button" class="rounded border px-3" [disabled]="teacherLookupLoading()" (click)="searchInstitutionTeachers()">{{ teacherLookupLoading() ? 'Aranıyor…' : 'Öğretmenleri bul' }}</button></span>
              </label>
              <label class="mt-3 block text-sm">Atanmış öğretmen
                <select [(ngModel)]="teacherFilterUserId" (ngModelChange)="loadPage()" class="mt-1 w-full rounded border px-3 py-2 dark:bg-gray-900">
                  <option value="">Tüm öğretmenler</option>
                  @for (teacher of teacherLookupResults(); track teacher.userId) { <option [value]="teacher.userId">{{ teacher.firstName }} {{ teacher.lastName }} · {{ teacher.email }}</option> }
                </select>
              </label>
              @if (teacherLookupError()) { <p role="alert" class="mt-2 text-sm text-red-700">{{ teacherLookupError() }}</p> }
            }
            @if (loading()) { <p role="status" class="mt-3">Yükleniyor…</p> }
            @if (error()) { <p role="alert" class="mt-3 text-red-700">{{ error() }}</p> }
            <div class="mt-4 overflow-x-auto"><table class="min-w-full text-left text-sm"><thead><tr><th class="py-2">Ad</th><th>E-posta</th><th>{{ kind === 'students' ? 'Öğretmen' : 'Kurum' }}</th><th></th></tr></thead><tbody>
              @if (kind === 'students') {
                @for (student of studentPage()?.students; track student.userId) { <tr class="border-t"><td class="py-2">{{ student.firstName }} {{ student.lastName }}</td><td>{{ student.email }}</td><td>{{ student.teacherName || 'Atanmamış' }}</td><td class="space-x-3"><button type="button" class="text-indigo-700 underline" (click)="openDetail(student.userId)">Ayrıntı</button>@if (canManageMemberships()) { <button type="button" class="text-red-700 underline disabled:opacity-50" [disabled]="savingMembership()" (click)="removeStudentFromInstitution(student)">Kurumdan çıkar</button> }</td></tr> }
              } @else {
                @for (teacher of teacherPage()?.teachers; track teacher.userId) { <tr class="border-t"><td class="py-2">{{ teacher.firstName }} {{ teacher.lastName }}</td><td>{{ teacher.email }}</td><td>{{ selectedInstitutionName() }}</td><td class="space-x-3"><button type="button" class="text-indigo-700 underline" (click)="openDetail(teacher.userId)">Ayrıntı</button>@if (canManageMemberships()) { <button type="button" class="text-red-700 underline disabled:opacity-50" [disabled]="savingMembership()" (click)="removeTeacherFromInstitution(teacher)">Kurumdan çıkar</button> }</td></tr> }
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
  private readonly membershipService = inject(CoachingInstitutionMembershipService);
  private readonly institutionService = inject(InstitutionService);
  private readonly authService = inject(AuthService);
  private readonly toaster = inject(ToasterService);
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
  readonly studentHistory = signal<CoachingAdminStudentHistoryPage | null>(null);
  readonly studentHistoryLoading = signal(false);
  readonly historyTypeOptions: { value: CoachingStudentHistoryType; label: string }[] = [
    { value: 'Assignments', label: 'Ödevler' },
    { value: 'Exams', label: 'Sınavlar' },
    { value: 'Sessions', label: 'Seanslar' },
    { value: 'Goals', label: 'Hedefler' }
  ];
  readonly teacherAnalytics = signal<TeacherCoachingAnalytics | null>(null);
  readonly teacherStudents = signal<CoachingStudentRosterPage | null>(null);
  readonly teacherLookupResults = signal<CoachingTeacherRosterItem[]>([]);
  readonly teacherLookupLoading = signal(false);
  readonly teacherLookupError = signal<string | null>(null);
  readonly savingMembership = signal(false);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private requestId = 0;
  private studentHistoryRequestId = 0;
  private teacherStudentsRequestId = 0;
  private teacherLookupRequestId = 0;
  institutionId = this.route.snapshot.queryParamMap.get('institutionId') ?? '';
  institutionSearch = '';
  search = '';
  teacherInviteEmail = '';
  studentInviteEmail = '';
  studentInviteTeacherUserId = '';
  teacherLookupSearch = '';
  teacherFilterUserId = '';
  editingGradeLevel: number | null = null;
  editingTeacherUserId = '';
  gradeLevelFilter: number | null = null;
  readonly gradeLevels = Array.from({ length: 12 }, (_, index) => index + 1);
  teacherStudentSearch = '';
  pageNumber = 1;
  teacherStudentPage = 1;
  studentHistoryType: CoachingStudentHistoryType = 'Assignments';
  studentHistoryPage = 1;

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
    ++this.teacherLookupRequestId;
    this.studentPage.set(null);
    this.teacherPage.set(null);
    this.selectedStudent.set(null);
    this.selectedTeacher.set(null);
    this.studentDetail.set(null);
    this.studentHistory.set(null);
    this.studentHistoryLoading.set(false);
    ++this.studentHistoryRequestId;
    this.teacherAnalytics.set(null);
    this.teacherStudents.set(null);
    this.teacherLookupResults.set([]);
    this.teacherLookupError.set(null);
    this.teacherLookupLoading.set(false);
    this.teacherLookupSearch = '';
    this.teacherFilterUserId = '';
    this.editingGradeLevel = null;
    this.editingTeacherUserId = '';
    this.studentInviteTeacherUserId = '';
    this.search = '';
    this.gradeLevelFilter = null;
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
      ? this.gradeLevelFilter === null && !this.teacherFilterUserId
        ? this.service.getStudentRoster(this.institutionId, page, this.search)
        : this.service.getStudentRoster(this.institutionId, page, this.search, this.teacherFilterUserId || undefined, this.gradeLevelFilter ?? undefined)
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
    ++this.studentHistoryRequestId;
    this.studentHistory.set(null);
    this.studentHistoryLoading.set(false);
    this.studentHistoryType = 'Assignments';
    this.studentHistoryPage = 1;
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
          this.editingGradeLevel = this.selectedStudent()?.gradeLevel ?? null;
          this.editingTeacherUserId = this.selectedStudent()?.teacherUserId ?? '';
          if (!this.selectedStudent()) this.error.set('Öğrenci bu kurumda bulunamadı.');
          else this.loadStudentHistory(1);
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
    this.error.set(null);
    this.service.getStudentRoster(this.institutionId, page, this.teacherStudentSearch, this.detailId).subscribe({
      next: result => { if (requestId === this.teacherStudentsRequestId) { this.teacherStudentPage = page; this.teacherStudents.set(result); } },
      error: () => { if (requestId === this.teacherStudentsRequestId) this.error.set('Öğrenci listesi yüklenemedi.'); }
    });
  }

  canManageMemberships(): boolean {
    const user = this.authService.userProfile();
    if (this.scope()?.isGlobal === true) return !!this.institutionId && user?.roles.includes('SystemAdmin') === true;
    return this.scope()?.isGlobal === false
      && (user?.roles.includes('InstitutionAdmin') === true || user?.roles.includes('InstitutionOwner') === true);
  }

  private membershipInstitutionId(): string | undefined {
    return this.scope()?.isGlobal ? this.institutionId : undefined;
  }

  searchInstitutionTeachers(): void {
    if (!this.institutionId) return;
    const requestId = ++this.teacherLookupRequestId;
    this.teacherLookupLoading.set(true);
    this.teacherLookupError.set(null);
    this.service.getTeacherRoster(this.institutionId, 1, this.teacherLookupSearch.trim()).subscribe({
      next: page => {
        if (requestId !== this.teacherLookupRequestId) return;
        const selectedTeacherIds = new Set([
          this.editingTeacherUserId,
          this.teacherFilterUserId,
          this.studentInviteTeacherUserId
        ].filter(Boolean));
        const previouslySelected = this.teacherLookupResults().filter(teacher =>
          selectedTeacherIds.has(teacher.userId)
          && !page.teachers.some(result => result.userId === teacher.userId));
        this.teacherLookupResults.set([...previouslySelected, ...page.teachers]);
        this.teacherLookupLoading.set(false);
      },
      error: () => {
        if (requestId !== this.teacherLookupRequestId) return;
        this.teacherLookupError.set('Öğretmen listesi yüklenemedi. Yeniden deneyin.');
        this.teacherLookupLoading.set(false);
      }
    });
  }

  hasTeacherLookupResult(teacherUserId: string): boolean {
    return this.teacherLookupResults().some(teacher => teacher.userId === teacherUserId);
  }

  sendTeacherInvitation(): void {
    const teacherEmail = this.teacherInviteEmail.trim();
    if (!this.canManageMemberships() || !teacherEmail || this.savingMembership()) return;
    this.savingMembership.set(true);
    this.membershipService.inviteTeacher({ teacherEmail }, this.membershipInstitutionId()).subscribe({
      next: () => {
        this.savingMembership.set(false);
        if (this.teacherInviteEmail.trim() === teacherEmail) this.teacherInviteEmail = '';
        this.toaster.success('Öğretmen daveti gönderildi. Kabul edildiğinde öğretmen kurum listesinde görünecek.');
        this.loadPage(this.pageNumber);
      },
      error: () => {
        this.savingMembership.set(false);
        this.toaster.error('Öğretmen daveti gönderilemedi. E-posta adresini ve bekleyen davetleri kontrol edip yeniden deneyin.');
      }
    });
  }

  sendStudentInvitation(): void {
    const studentEmail = this.studentInviteEmail.trim();
    if (!this.canManageMemberships() || !studentEmail || this.savingMembership()) return;
    const teacherUserId = this.studentInviteTeacherUserId || undefined;
    this.savingMembership.set(true);
    this.membershipService.inviteStudent({ studentEmail, ...(teacherUserId ? { teacherUserId } : {}) }, this.membershipInstitutionId()).subscribe({
      next: () => {
        this.savingMembership.set(false);
        if (this.studentInviteEmail.trim() === studentEmail) this.studentInviteEmail = '';
        if (this.studentInviteTeacherUserId === (teacherUserId ?? '')) this.studentInviteTeacherUserId = '';
        this.toaster.success('Öğrenci daveti gönderildi. Kabul edildiğinde öğrenci kurum listesinde görünecek.');
        this.loadPage(this.pageNumber);
      },
      error: () => {
        this.savingMembership.set(false);
        this.toaster.error('Öğrenci daveti gönderilemedi. E-posta adresini ve bekleyen davetleri kontrol edip yeniden deneyin.');
      }
    });
  }

  saveStudentMembership(): void {
    const student = this.selectedStudent();
    if (!this.canManageMemberships() || !student || this.editingGradeLevel === null
      || this.editingGradeLevel < 1 || this.editingGradeLevel > 12 || this.savingMembership()) return;
    const gradeLevel = this.editingGradeLevel;
    const teacherUserId = this.editingTeacherUserId || null;
    this.savingMembership.set(true);
    this.membershipService.updateStudent(student.userId, {
      gradeLevel,
      teacherUserId
    }, this.membershipInstitutionId()).subscribe({
      next: () => {
        this.savingMembership.set(false);
        const teacher = this.teacherLookupResults().find(item => item.userId === teacherUserId);
        this.selectedStudent.set({
          ...student,
          gradeLevel,
          teacherUserId,
          teacherName: teacher ? `${teacher.firstName} ${teacher.lastName}` : teacherUserId ? student.teacherName : null
        });
        this.toaster.success('Öğrencinin sınıfı ve öğretmen ataması güncellendi.');
        this.loadPage(this.pageNumber);
      },
      error: () => {
        this.savingMembership.set(false);
        this.toaster.error('Öğrenci üyeliği güncellenemedi. Seçilen öğretmenin hâlâ bu kurumda olduğunu kontrol edin.');
      }
    });
  }

  async removeStudentFromInstitution(student: CoachingStudentRosterItem): Promise<void> {
    if (!this.canManageMemberships()) return;
    const confirmed = await this.toaster.confirm(
      `${student.firstName} ${student.lastName} kurumdan çıkarılacak. Hesabı veya koçluk geçmişi silinmez. Devam edilsin mi?`,
      { title: 'Öğrenciyi kurumdan çıkar', confirmText: 'Kurumdan çıkar', cancelText: 'Vazgeç' }
    );
    if (!confirmed || this.savingMembership()) return;
    this.savingMembership.set(true);
    this.membershipService.removeStudent(student.userId, this.membershipInstitutionId()).subscribe({
      next: () => {
        this.savingMembership.set(false);
        this.toaster.success('Öğrenci kurum üyeliğinden çıkarıldı; hesabı ve koçluk geçmişi korunuyor.');
        if (this.detailId === student.userId) this.backToList();
        else this.loadPage(this.pageNumber);
      },
      error: () => {
        this.savingMembership.set(false);
        this.toaster.error('Öğrenci kurumdan çıkarılamadı. Yeniden deneyin.');
      }
    });
  }

  async removeTeacherFromInstitution(teacher: CoachingTeacherRosterItem): Promise<void> {
    if (!this.canManageMemberships()) return;
    const confirmed = await this.toaster.confirm(
      `${teacher.firstName} ${teacher.lastName} kurumdan çıkarılacak ve aktif öğrenci atamaları sonlandırılacak. Hesabı veya koçluk geçmişi silinmez. Devam edilsin mi?`,
      { title: 'Öğretmeni kurumdan çıkar', confirmText: 'Kurumdan çıkar', cancelText: 'Vazgeç' }
    );
    if (!confirmed || this.savingMembership()) return;
    this.savingMembership.set(true);
    this.membershipService.removeTeacher(teacher.userId, this.membershipInstitutionId()).subscribe({
      next: () => {
        this.savingMembership.set(false);
        this.toaster.success('Öğretmen kurum üyeliğinden çıkarıldı; hesabı ve koçluk geçmişi korunuyor.');
        if (this.detailId === teacher.userId) this.backToList();
        else this.loadPage(this.pageNumber);
      },
      error: () => {
        this.savingMembership.set(false);
        this.toaster.error('Öğretmen kurumdan çıkarılamadı. Yeniden deneyin.');
      }
    });
  }

  selectStudentHistoryType(type: CoachingStudentHistoryType): void {
    this.studentHistoryType = type;
    this.loadStudentHistory(1);
  }

  loadStudentHistory(page: number): void {
    if (!this.institutionId || !this.detailId || !this.selectedStudent()) return;
    const requestId = ++this.studentHistoryRequestId;
    this.studentHistory.set(null);
    this.studentHistoryLoading.set(true);
    this.error.set(null);
    this.service.getStudentHistory(this.detailId, this.studentHistoryType, page, 25).subscribe({
      next: result => {
        if (requestId !== this.studentHistoryRequestId) return;
        this.studentHistoryPage = page;
        this.studentHistory.set(result);
        this.studentHistoryLoading.set(false);
      },
      error: () => {
        if (requestId !== this.studentHistoryRequestId) return;
        this.error.set('Öğrencinin koçluk geçmişi yüklenemedi.');
        this.studentHistoryLoading.set(false);
      }
    });
  }

  openDetail(id: string): void {
    void this.router.navigate(['/dashboard/coaching', this.kind, id], { queryParams: { institutionId: this.institutionId } });
  }

  backToList(): void {
    void this.router.navigate(['/dashboard/coaching', this.kind], { queryParams: { institutionId: this.institutionId } });
  }
}
