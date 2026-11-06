import { coachingFeedback, coachingToaster } from '../src/app/features/coaching-portal/coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, Input, OnInit, inject, signal, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, finalize, map } from 'rxjs';
import { ComboOption, ComboPage, OnboardingComboboxComponent, fold } from '../src/app/features/coaching-portal/onboarding/onboarding-combobox.component';

interface StudentProfile {
  version: number; isActive: boolean; educationStatus: string; gradeLevel: number | null;
  schoolName?: string | null; schoolCity?: string | null; schoolDistrict?: string | null; schoolType?: string | null;
  classBranch?: string | null; graduationYear?: number | null; birthDate?: string | null;
  bio?: string | null; avatarUrl?: string | null; shareProgressWithTeachers: boolean;
  /** Paket C: asked in the first-login wizard; the student corrects them here (KVKK m.11). */
  homeProvinceId?: string | null; homeCity?: string | null; homeDistrictId?: string | null; homeDistrict?: string | null;
  diplomaGrade?: number | null; diplomaGradeEstimated?: boolean;
}
interface Place { id: string; name: string }
/** Paket D (#7): a school picked from the catalog on Profilim (the wizard's list). */
interface PickedSchool { id: string; name: string; district: string | null; type: string | null }
interface SchoolPage { items: { id: string; name: string; district: string | null; type?: string | null; inHomeDistrict?: boolean }[]; totalCount: number }
interface TeacherProfile { version: number; isActive: boolean; title?: string | null; subjects: string[]; experienceYears?: number | null; bio?: string | null; avatarUrl?: string | null; }
interface OwnProfile { student: StudentProfile | null; teacher: TeacherProfile | null;
  /** Paket D review (L2): the student's exam asks the YKS diploma grade (missing on an older API: asked). */
  examAsksDiploma?: boolean }
interface Invitation { id: string; inviterEmail: string; type: string; message?: string; expiresAt: string; }

@Component({
  selector: 'edu-coaching-profile', standalone: true, imports: [CommonModule, FormsModule, RouterLink, OnboardingComboboxComponent],
  template: `
    <section aria-labelledby="coaching-profile-title" class="profile-card">
      <h2 id="coaching-profile-title">Koçluk profilim</h2>
      <p>{{ t('Eğitim ve uzmanlık bilgilerinizi burada düzenleyebilirsiniz. Ad, e-posta ve şifre ortak hesap ayarlarınızdadır.', 'Okul ve sınıf bilgilerini burada düzenle. Ad, e-posta ve şifre hesap ayarlarında.') }}</p>
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @if (success()) { <p role="status">{{ success() }}</p> }
      @if (gradeNotice()) {
        <!-- Paket D (#8): the new grade no longer fits the exam in the study settings. -->
        <p role="status" class="notice" data-testid="grade-purpose-notice">Seçili sınavın sınıfına uymuyor; çalışma tercihlerini güncelle. <a routerLink="/coaching-portal/account/settings">Çalışma tercihlerim</a></p>
      }
      @if (loading()) { <p role="status">Profil yükleniyor…</p> }
      @if (ready() && !student && role === 'Student' || ready() && !teacher && role === 'Teacher') {
        <p>{{ t('Koçluk profiliniz henüz oluşturulmamış.', 'Koçluk profilin henüz oluşturulmamış.') }}</p>
        <button type="button" [disabled]="working()" (click)="provision()">Koçluk profilimi oluştur</button>
      }
      @if (ready() && (role === 'Student' ? student : teacher); as profile) {
        @if (!profile.isActive) { <p role="alert">{{ t('Koçluk profiliniz pasif. Yöneticinizle görüşün.', 'Koçluk profilin pasif. Öğretmeninle ya da kurumunla görüş.') }}</p> }
        <form #profileForm="ngForm" (ngSubmit)="profileForm.valid && save()" (ngModelChange)="dirty = true">
          <fieldset [disabled]="working() || !profile.isActive">
            @if (role === 'Student' && student; as s) {
              <label>Eğitim durumu <select name="educationStatus" [(ngModel)]="s.educationStatus" (ngModelChange)="dirty = true" required>
                <option value="unknown">{{ t('Seçin', 'Seç') }}</option><option value="student">Öğrenci</option><option value="graduate">Mezun</option>
              </select></label>
              @if (s.educationStatus === 'student') {
                <label>Sınıf <input name="gradeLevel" type="number" min="1" max="12" required [(ngModel)]="s.gradeLevel" (ngModelChange)="dirty = true"></label>
              }
              @if (!setupMode) {
              @if (s.educationStatus === 'graduate') {
                <label>Mezuniyet yılı (isteğe bağlı) <input name="graduationYear" type="number" min="1900" [max]="year" [(ngModel)]="s.graduationYear" (ngModelChange)="dirty = true"></label>
              }
              <!-- Paket D (#7): the school comes from the catalog as in the wizard (no typed school); an older typed one stays until replaced. -->
              <div class="place" data-testid="own-school">
                <span>Okulun (isteğe bağlı): <strong>{{ schoolName(s) ?? 'seçilmedi' }}</strong>
                  @if (schoolDetail(s); as detail) { <small> · {{ detail }}</small> }</span>
                @if (!schoolPicker()) {
                  <!-- Review L5: a saved school can always be removed; the catalog list is the high-school one (9th grade on). -->
                  <span>@if (schoolApplies(s)) { <button type="button" (click)="openSchoolPicker()">{{ schoolName(s) ? 'Okulunu değiştir' : 'Okulunu seç' }}</button> }
                  @if (schoolName(s)) { <button type="button" (click)="removeSchool()">Okulu kaldır</button> }</span>
                } @else if (schoolApplies(s)) {
                  @if (cityPicker()) {
                    <app-onboarding-combobox label="Okulun hangi şehirde?" placeholder="Şehir adını yaz" [options]="schoolCities()" (picked)="pickSchoolCity($event)" />
                    <button type="button" (click)="closeSchoolPicker()">Vazgeç</button>
                  } @else {
                    <span>Şehir: <strong>{{ schoolCityLabel() }}</strong> <button type="button" (click)="changeSchoolCity()">Şehri değiştir</button></span>
                    @for (key of [schoolCity()]; track key) {
                      <app-onboarding-combobox label="Okulunu listeden seç" placeholder="Okulunun adını yaz" [load]="loadSchools" (picked)="pickSchool($event)" />
                    }
                    <button type="button" (click)="closeSchoolPicker()">Vazgeç</button>
                  }
                }
              </div>
              <label>Şube (isteğe bağlı) <input name="classBranch" maxlength="30" [(ngModel)]="s.classBranch" (ngModelChange)="dirty = true"></label>
              <label>Doğum tarihi (isteğe bağlı) <input name="birthDate" type="date" [max]="today" [(ngModel)]="s.birthDate" (ngModelChange)="dirty = true"></label>
              <div class="place" data-testid="home-place">
                <span>Yaşadığın yer: <strong>{{ s.homeCity ? s.homeCity + (s.homeDistrict ? ' / ' + s.homeDistrict : '') : 'seçilmedi' }}</strong></span>
                @if (!provinces()) { <button type="button" (click)="loadProvinces()">{{ s.homeCity ? 'Değiştir' : 'Seç' }}</button> }
                @else {
                  <label>Şehir <select name="homeProvince" [ngModel]="homeProvince" (ngModelChange)="pickProvince($event)">
                    <option value="">Seç</option>@for (p of provinces(); track p.id) { <option [value]="p.id">{{ p.name }}</option> }
                  </select></label>
                  <label>İlçe <select name="homeDistrict" [ngModel]="homeDistrictId" (ngModelChange)="homeDistrictId = $event; dirty = true" [disabled]="!districts().length">
                    <option value="">Seç</option>@for (d of districts(); track d.id) { <option [value]="d.id">{{ d.name }}</option> }
                  </select></label>
                }
              </div>
              @if (diplomaApplies(s)) {
                <label>Diploma notu (YKS, isteğe bağlı) <input name="diplomaGrade" type="number" min="50" max="100" step="0.01" [(ngModel)]="s.diplomaGrade" (ngModelChange)="dirty = true"></label>
                <label class="check"><input name="diplomaEstimated" type="checkbox" [(ngModel)]="s.diplomaGradeEstimated" (ngModelChange)="dirty = true"> Diploma notum tahmini</label>
              }
              <label>Hakkımda (isteğe bağlı) <textarea name="studentBio" maxlength="2000" [(ngModel)]="s.bio" (ngModelChange)="dirty = true"></textarea></label>
              <label class="check"><input name="shareProgress" type="checkbox" [(ngModel)]="s.shareProgressWithTeachers" (ngModelChange)="dirty = true"> Çalışma ilerlememi yetkili öğretmenlerle paylaş</label>
              }
            }
            @if (role === 'Teacher' && teacher; as t) {
              <label>Unvan (isteğe bağlı) <input name="title" maxlength="100" [(ngModel)]="t.title" (ngModelChange)="dirty = true"></label>
              <label>Branşlar (virgülle ayırın) <input name="subjects" maxlength="5000" [(ngModel)]="subjects" (ngModelChange)="dirty = true"></label>
              <label>Deneyim (yıl, isteğe bağlı) <input name="experience" type="number" min="0" max="100" [(ngModel)]="t.experienceYears" (ngModelChange)="dirty = true"></label>
              <label>Hakkımda (isteğe bağlı) <textarea name="teacherBio" maxlength="2000" [(ngModel)]="t.bio" (ngModelChange)="dirty = true"></textarea></label>
            }
            <button type="submit" [disabled]="profileForm.invalid || !dirty">{{ working() ? 'Kaydediliyor…' : 'Profili kaydet' }}</button>
          </fieldset>
        </form>
      }
      <button type="button" [disabled]="loading() || working()" (click)="reload()">Profili yeniden yükle</button>
    </section>
    @if (!setupMode) { <section class="profile-card" aria-labelledby="coaching-invitations-title">
      <h2 id="coaching-invitations-title">Koçluk davetlerim</h2>
      @if (invitationError()) { <p role="alert" class="error">{{ invitationError() }}</p> }
      @if (invitationLoading()) { <p role="status">Davetler yükleniyor…</p> }
      @else if (!invitations().length && !invitationError()) { <p>{{ t('Bekleyen koçluk davetiniz yok.', 'Bekleyen koçluk davetin yok.') }}</p> }
      @for (invite of invitations(); track invite.id) {
        <article><strong>{{ invite.inviterEmail }}</strong><p>{{ invite.type === 'TeacherToInstitution' ? 'Kurum öğretmen daveti' : invite.type === 'StudentToInstitution' ? 'Kurum öğrenci daveti' : 'Koç–öğrenci daveti' }}</p>
          @if (invite.message) { <p>{{ invite.message }}</p> }
          @if (role === 'Student' && invite.type !== 'TeacherToInstitution') {
            <p class="history-notice">{{ invite.type === 'StudentToInstitution' ? 'Kabul edersen bu kurumun yöneticileri ve öğretmenleri' : 'Kabul edersen bu öğretmen' }} önceki öğretmen ve kurumlarındaki çalışma geçmişini (ödevler, denemeler, seanslar, hedefler) görebilir.</p>
          }
          <p>Son tarih: {{ invite.expiresAt | date:'dd.MM.yyyy' }}</p>
          <button type="button" [disabled]="working()" (click)="respond(invite.id, 'accept')">Kabul et</button>
          <button type="button" [disabled]="working()" (click)="respond(invite.id, 'reject')">Reddet</button>
        </article>
      }
      <button type="button" [disabled]="working() || invitationLoading()" (click)="loadInvitations()">Davetleri yenile</button>
    </section> }`,
  styles: [`:host{display:block;color:inherit}.profile-card{padding:1.5rem;margin-bottom:1rem;border:1px solid #d6deea;border-radius:1rem;background:var(--surface,#fff);color:#16243c}h2{margin-top:0}fieldset{border:0;padding:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:1rem}label{display:grid;gap:.4rem}input,textarea,select,button{font:inherit;padding:.6rem;border:1px solid #bcc8da;border-radius:.5rem;max-width:100%;box-sizing:border-box}button{cursor:pointer;background:#edf3fb;margin:.4rem .4rem .4rem 0}button:disabled{opacity:.55;cursor:default}.check{display:flex;align-items:center}.place{display:grid;gap:.4rem}.error{color:#a21825}article{border-top:1px solid #d6deea;padding-top:1rem}input:focus-visible,select:focus-visible,textarea:focus-visible,button:focus-visible{outline:3px solid #3972c6;outline-offset:2px}.notice{padding:.6rem .8rem;border-radius:.5rem;background:#fff6e0}:host ::ng-deep .ob-combo{position:relative}:host ::ng-deep .ob-combo input{width:100%}:host ::ng-deep .ob-combo-list{list-style:none;margin:.35rem 0 0;padding:.25rem;max-height:16rem;overflow:auto;border:1px solid #bcc8da;border-radius:.5rem;background:var(--surface,#fff)}:host ::ng-deep .ob-combo-list li{display:grid;padding:.45rem .6rem;border-radius:.4rem;cursor:pointer}:host ::ng-deep .ob-combo-list li.is-active,:host ::ng-deep .ob-combo-list li:hover{background:#edf3fb}:host ::ng-deep .ob-combo-list small,:host ::ng-deep .ob-combo-empty{color:#5a6a82}:host ::ng-deep .ob-combo-more button{border:0;background:none;text-decoration:underline}`]
})
export class CoachingProfileComponent implements OnInit {
  private readonly toaster = coachingToaster();
  @Input({ required: true }) apiUrl = '';
  @Input() role: 'Student' | 'Teacher' = 'Student';
  @Input() setupMode = false;
  readonly saved = output<void>();
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly loading = signal(false); readonly working = signal(false); readonly ready = signal(false);
  readonly error = signal<string | null>(null); readonly success = signal<string | null>(null);
  readonly invitations = signal<Invitation[]>([]); readonly invitationError = signal<string | null>(null); readonly invitationLoading = signal(false);
  student: StudentProfile | null = null; teacher: TeacherProfile | null = null; subjects = ''; dirty = false;
  readonly provinces = signal<Place[] | null>(null); readonly districts = signal<Place[]>([]);
  homeProvince = ''; homeDistrictId = ''; private savedDiploma: number | null = null; private savedEducation = ''; private examAsksDiploma = true;
  /** Paket D (#7): the school picker (the wizard's catalog list) and the school picked or removed but not saved yet. */
  readonly schoolPicker = signal(false); readonly cityPicker = signal(false);
  readonly schoolCity = signal(''); readonly schoolCityLabel = signal('');
  readonly schoolCities = signal<ComboOption[]>([]); readonly pickedSchool = signal<PickedSchool | null>(null); readonly schoolRemoved = signal(false);
  /** Paket D (#8): after a grade change the exam in the study settings no longer fits. */
  readonly gradeNotice = signal(false);
  readonly year = new Date().getFullYear(); readonly today = new Date().toISOString().slice(0, 10);
  private get base() { return `${this.apiUrl}/coaching`; }
  ngOnInit() { this.loadProfile(); if (!this.setupMode) this.loadInvitations(); }

  private loadProfile() {
    this.loading.set(true); this.ready.set(false); this.error.set(null);
    this.http.get<{ data: OwnProfile }>(`${this.base}/profiles/me`).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false)))
      .subscribe({ next: response => { this.apply(response.data); this.ready.set(true); }, error: e => this.error.set(this.message(e, this.t('Koçluk profili yüklenemedi. Yeniden deneyin.', 'Profilin yüklenemedi. Yeniden dene.'))) });
  }
  private apply(profile: OwnProfile) {
    this.student = profile.student ? { ...profile.student, educationStatus: profile.student.educationStatus.toLowerCase() } : null;
    if (profile.examAsksDiploma !== undefined) this.examAsksDiploma = profile.examAsksDiploma;
    this.savedDiploma = this.student?.diplomaGrade ?? null; this.provinces.set(null); this.homeProvince = ''; this.homeDistrictId = '';
    this.savedEducation = this.education(this.student); this.closeSchoolPicker(); this.pickedSchool.set(null); this.schoolRemoved.set(false);
    this.teacher = profile.teacher ? { ...profile.teacher } : null; this.subjects = this.teacher?.subjects?.join(', ') ?? ''; this.dirty = false;
  }
  loadProvinces() {
    this.http.get<Place[]>(`${this.apiUrl}/locations/provinces`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: list => this.provinces.set(list), error: () => this.error.set(this.t('Şehir listesi yüklenemedi.', 'Şehir listesi yüklenemedi. Yeniden dene.')) });
  }
  pickProvince(id: string) {
    this.homeProvince = id; this.homeDistrictId = ''; this.districts.set([]); this.dirty = true;
    if (!id) return;
    this.http.get<Place[]>(`${this.apiUrl}/locations/provinces/${encodeURIComponent(id)}/districts`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: list => this.districts.set(list), error: () => this.error.set(this.t('İlçe listesi yüklenemedi.', 'İlçe listesi yüklenemedi. Yeniden dene.')) });
  }
  // ---------- Paket D (#7): the school from the catalog ----------
  /** As in the wizard: the own-school list is the high-school catalog (9th grade on, and graduates). */
  schoolApplies(s: StudentProfile) { return s.educationStatus === 'graduate' || s.educationStatus === 'student' && Number(s.gradeLevel) >= 9; }
  schoolName(s: StudentProfile) { return this.schoolRemoved() ? null : this.pickedSchool()?.name ?? (s.schoolName || null); }
  schoolDetail(s: StudentProfile) {
    if (this.schoolRemoved()) return null;
    const picked = this.pickedSchool();
    const parts = picked ? [picked.district, picked.type] : [[s.schoolCity, s.schoolDistrict].filter(Boolean).join(' / '), s.schoolType];
    return parts.filter(Boolean).join(' · ') || null;
  }
  /** Starts in the home city (its district's schools come first); another city can be picked from the catalog's cities. */
  openSchoolPicker() {
    this.schoolPicker.set(true);
    const home = this.student?.homeCity;
    if (home) { this.schoolCity.set(home); this.schoolCityLabel.set(home); this.cityPicker.set(false); this.focusPicker(); } else this.changeSchoolCity();
  }
  /** Review L8: the focus moves into the picker's list as it opens or changes (after it is rendered). */
  private focusPicker() {
    setTimeout(() => (this.host.nativeElement.querySelector('[data-testid="own-school"] input[role="combobox"]') as HTMLInputElement | null)?.focus());
  }
  changeSchoolCity() {
    this.cityPicker.set(true); this.focusPicker();
    if (this.schoolCities().length) return;
    this.http.get<{ data: { value: string; label?: string }[] }>(`${this.base}/students/me/onboarding/options/schools/cities`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: r => this.schoolCities.set(r.data.map(x => ({ value: x.value, label: x.label ?? x.value }))),
      error: e => this.error.set(this.message(e, 'Şehir listesi yüklenemedi. Yeniden dene.')) });
  }
  pickSchoolCity(option: ComboOption) { this.schoolCity.set(option.value); this.schoolCityLabel.set(option.label); this.cityPicker.set(false); this.focusPicker(); }
  closeSchoolPicker() { this.schoolPicker.set(false); this.cityPicker.set(false); }
  pickSchool(option: ComboOption) {
    const data = option.data ?? {};
    this.pickedSchool.set({ id: option.value, name: option.label, district: (data['district'] as string | null) ?? null, type: (data['type'] as string | null) ?? null });
    this.schoolRemoved.set(false); this.closeSchoolPicker(); this.dirty = true;
  }
  removeSchool() { this.pickedSchool.set(null); this.schoolRemoved.set(true); this.closeSchoolPicker(); this.dirty = true; }
  readonly loadSchools = (search: string, page: number): Observable<ComboPage> => {
    const home = this.student?.homeCity && fold(this.student.homeCity) === fold(this.schoolCityLabel()) ? this.student.homeDistrict ?? null : null;
    const params: Record<string, string | number> = { city: this.schoolCity(), page };
    if (search.trim()) params['search'] = search.trim();
    if (home) params['district'] = home;
    return this.http.get<{ data: SchoolPage }>(`${this.base}/students/me/onboarding/options/schools`, { params }).pipe(map(r => ({ total: r.data.totalCount,
      items: r.data.items.map(x => ({ value: x.id, label: x.name, data: { district: x.district, type: x.type ?? null },
        hint: [x.inHomeDistrict ? 'ilçende' : null, x.district, x.type].filter(Boolean).join(' · ') || undefined })) })));
  };
  /** The wizard's rule: the diploma grade is asked for 11th, 12th grade and graduates (YKS; the server drops it for other exams). */
  diplomaApplies(s: StudentProfile) {
    return this.examAsksDiploma && (s.educationStatus === 'graduate' || s.educationStatus === 'student' && [11, 12].includes(Number(s.gradeLevel)));
  }
  private education(s: StudentProfile | null) { return s ? `${s.educationStatus.toLowerCase()}:${s.educationStatus.toLowerCase() === 'student' ? s.gradeLevel ?? '' : ''}` : ''; }
  async reload() { if (this.dirty && !await this.toaster.confirm('Kaydedilmemiş profil değişikliklerinden vazgeçilsin mi?')) return; this.loadProfile(); }
  provision() {
    if (this.working() || !this.ready()) return;
    this.working.set(true); this.error.set(null);
    this.http.post(`${this.base}/profiles/me/provision`, { role: this.role }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false)))
      .subscribe({ next: () => this.loadProfile(), error: e => this.error.set(this.message(e, 'Koçluk profili oluşturulamadı.')) });
  }
  save() {
    if (this.working() || !this.ready()) return;
    let payload: object;
    if (this.role === 'Student' && this.student) {
      const s = this.student;
      if (s.educationStatus === 'unknown' || s.educationStatus === 'student' && (!s.gradeLevel || s.gradeLevel < 1 || s.gradeLevel > 12)) { this.error.set('Eğitim durumunu ve öğrenciysen sınıfını seç.'); return; }
      // Paket D (#7): the saved school is sent back as it is, a picked one by its catalog id (the server fills it from the catalog).
      const removed = this.schoolRemoved(); const picked = this.pickedSchool();
      // Paket D (#8): the diploma grade only where the wizard asks it; the server drops it elsewhere.
      const diploma = this.diplomaApplies(s);
      payload = { expectedVersion: s.version, educationStatus: s.educationStatus, gradeLevel: s.educationStatus === 'student' ? s.gradeLevel : null,
        graduationYear: s.educationStatus === 'graduate' ? s.graduationYear || null : null, schoolName: removed ? null : s.schoolName || null,
        schoolCity: removed ? null : s.schoolCity || null, schoolDistrict: removed ? null : s.schoolDistrict || null, schoolType: removed ? null : s.schoolType || null,
        ...(picked && !removed ? { schoolId: picked.id } : {}),
        classBranch: s.classBranch || null, birthDate: s.birthDate || null,
        bio: s.bio || null, avatarUrl: s.avatarUrl || null, shareProgressWithTeachers: s.shareProgressWithTeachers,
        // Paket C: only a changed place is sent (checked against the shared list); an emptied diploma grade is removed.
        ...(this.homeProvince && this.homeDistrictId ? { homeProvinceId: this.homeProvince, homeDistrictId: this.homeDistrictId } : {}),
        ...(!diploma ? {} : s.diplomaGrade !== null && s.diplomaGrade !== undefined && String(s.diplomaGrade) !== ''
          ? { diplomaGrade: Number(s.diplomaGrade), diplomaGradeEstimated: !!s.diplomaGradeEstimated }
          : { clearDiplomaGrade: this.savedDiploma !== null }) };
      if (this.homeProvince && !this.homeDistrictId) { this.error.set('Şehrinle birlikte ilçeni de seç.'); return; }
      const grade = Number(s.diplomaGrade);
      if (diploma && s.diplomaGrade !== null && s.diplomaGrade !== undefined && String(s.diplomaGrade) !== '' && (grade < 50 || grade > 100)) { this.error.set('Diploma notu 50 ile 100 arasında olmalı.'); return; }
    } else if (this.role === 'Teacher' && this.teacher) {
      const t = this.teacher;
      payload = { expectedVersion: t.version, title: t.title || null, subjects: this.subjects.split(',').map(x => x.trim()).filter(Boolean),
        experienceYears: t.experienceYears ?? null, bio: t.bio || null, avatarUrl: t.avatarUrl || null };
    } else return;
    this.working.set(true); this.error.set(null); this.success.set(null); this.gradeNotice.set(false);
    const gradeChanged = this.role === 'Student' && !!this.student && this.education(this.student) !== this.savedEducation;
    this.http.put<{ data: StudentProfile | TeacherProfile }>(`${this.base}/profiles/me/${this.role.toLowerCase()}`, payload)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false))).subscribe({ next: response => {
        this.apply({ student: this.role === 'Student' ? response.data as StudentProfile : this.student,
          teacher: this.role === 'Teacher' ? response.data as TeacherProfile : this.teacher }); this.success.set(this.t('Koçluk profiliniz kaydedildi.', 'Profilin kaydedildi.')); this.saved.emit();
        // Paket D (#8): the study settings follow the new grade; when the exam no longer fits, say so and link there.
        if (gradeChanged && !this.setupMode) this.checkStudySettings();
      }, error: e => this.error.set(this.message(e, this.t('Profil kaydedilemedi. Bilgilerinizi kontrol edin.', 'Profil kaydedilemedi. Bilgilerini kontrol et.'))) });
  }
  private checkStudySettings() {
    this.http.get<{ data: { purposeNotForEducation?: boolean } }>(`${this.base}/study-planning/setup`).pipe(takeUntilDestroyed(this.destroy))
      .subscribe({ next: r => this.gradeNotice.set(!!r.data?.purposeNotForEducation), error: () => this.gradeNotice.set(false) });
  }
  loadInvitations() {
    this.invitationLoading.set(true); this.invitationError.set(null);
    this.http.get<Invitation[]>(`${this.base}/invitations/my-invitations`).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.invitationLoading.set(false)))
      .subscribe({ next: rows => this.invitations.set(rows), error: e => this.invitationError.set(this.message(e, 'Davetler yüklenemedi.')) });
  }
  respond(id: string, action: 'accept' | 'reject') {
    if (this.working()) return;
    if (this.dirty) { this.invitationError.set(this.t('Davet işlemi öncesinde profil değişikliklerini kaydedin.', 'Daveti yanıtlamadan önce profil değişikliklerini kaydet.')); return; }
    this.working.set(true); this.invitationError.set(null);
    this.http.post(`${this.base}/invitations/${encodeURIComponent(id)}/${action}`, {}).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false)))
      .subscribe({ next: () => { this.loadInvitations(); if (action === 'accept') this.loadProfile(); }, error: e => this.invitationError.set(this.message(e, this.t('Davet işlenemedi. Koçluk rolünüzü ve davetin geçerliliğini kontrol edin.', 'Davet yanıtlanamadı. Davetin süresi dolmuş olabilir.'))) });
  }
  async canLeavePage() { return !this.working() && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş Koçluk profil değişikliklerinden vazgeçilsin mi?')); }
  /** Teachers are addressed formally (siz), students informally (sen). */
  t(formal: string, informal: string) { return this.role === 'Student' ? informal : formal; }
  private message(e: { error?: { message?: unknown; detail?: unknown } }, fallback: string) {
    const text = e.error?.message ?? e.error?.detail; return typeof text === 'string' ? text : fallback;
  }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
