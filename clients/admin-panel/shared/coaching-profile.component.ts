import { coachingFeedback, coachingToaster } from '../src/app/features/coaching-portal/coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, Input, OnInit, computed, inject, signal, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, finalize, map } from 'rxjs';
import { ComboOption, ComboPage, OnboardingComboboxComponent, fold } from '../src/app/features/coaching-portal/onboarding/onboarding-combobox.component';
import { EDUCATION_STAGES, EducationStage, educationRequest, gradesFor, isStudentStage, stageOf } from '../src/app/features/coaching-portal/coaching-education';
import { coachingDate } from '../src/app/features/coaching-portal/coaching-labels';
import { CoachingStudentNavigationService } from '../src/app/core/services/coaching-student-navigation.service';

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
/** E4 #23: what saving a new education would remove or leave not fitting (asked from the server before saving). */
interface EducationImpact { examNoLongerFits: boolean; exam: string | null; diplomaGradeRemoved: number | null; target: string | null }
type FieldName = 'educationStage' | 'gradeLevel' | 'graduationYear' | 'birthDate' | 'homeDistrict' | 'diplomaGrade' | 'experience';
const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });

@Component({
  selector: 'edu-coaching-profile', standalone: true, imports: [CommonModule, FormsModule, RouterLink, OnboardingComboboxComponent],
  template: `
    <!-- F2 #25: the student page already has the "Profilim" heading; only staff see this card's own heading. -->
    <section [attr.aria-labelledby]="role === 'Student' ? null : 'coaching-profile-title'" [attr.aria-label]="role === 'Student' ? 'Profil bilgilerin' : null" class="profile-card">
      @if (role !== 'Student') { <h2 id="coaching-profile-title">Koçluk profilim</h2> }
      <!-- E5 (#22): the student's name, e-mail and password are on the Hesabım tab; staff use the portal's Hesap ayarları. -->
      @if (role === 'Student') { <p>Okul ve sınıf bilgilerini burada düzenle. Adın, e-posta adresin ve şifren <a routerLink="/coaching-portal/account/settings/account">Hesabım</a> sekmesinde.</p> }
      @else { <p>Eğitim ve uzmanlık bilgilerinizi burada düzenleyebilirsiniz. Ad, e-posta ve şifre ortak hesap ayarlarınızdadır.</p> }
      <!-- E4 #24: every save attempt replaces the previous message; field errors are listed here and next to each field. -->
      @if (error()) {
        @if (hasFieldErrors()) { <div role="alert" class="error" data-testid="error-summary">{{ error() }}</div> }
        @else { <p role="alert" class="error">{{ error() }}</p> }
        <!-- F2 review: a profile that did not load can be loaded again (students, teachers and staff). -->
        @if (!ready() && !loading()) { <button type="button" (click)="reload()">Yeniden dene</button> }
      }
      @if (success()) { <p role="status">{{ success() }}</p> }
      @if (gradeNotice()) {
        <!-- Paket D (#8): the new grade no longer fits the exam in the study settings. -->
        <p role="status" class="notice" data-testid="grade-purpose-notice">Seçili sınavın eğitim durumuna ya da sınıfına uymuyor; çalışma tercihlerini güncelle.@if (savedImpactTarget(); as target) { Hedefin ({{ target }}) yeni sınavına uymazsa, sınavını değiştirdiğinde kaldırılır. } <a routerLink="/coaching-portal/account/settings">Çalışma tercihlerim</a></p>
      }
      @if (loading()) { <p role="status">Profil yükleniyor…</p> }
      @if (ready() && !student && role === 'Student' || ready() && !teacher && role === 'Teacher') {
        <p>{{ t('Koçluk profiliniz henüz oluşturulmamış.', 'Koçluk profilin henüz oluşturulmamış.') }}</p>
        <button type="button" [disabled]="working()" (click)="provision()">Koçluk profilimi oluştur</button>
      }
      @if (ready() && (role === 'Student' ? student : teacher); as profile) {
        @if (!profile.isActive) { <p role="alert">{{ t('Koçluk profiliniz pasif. Yöneticinizle görüşün.', 'Koçluk profilin pasif. Öğretmeninle ya da kurumunla görüş.') }}</p> }
        <form #profileForm="ngForm" novalidate (ngSubmit)="save()" (ngModelChange)="dirty = true">
          <!-- E4 review: while the consequences are asked the form is locked, so "Evet, kaydet" saves exactly what was shown. -->
          <fieldset [disabled]="working() || !profile.isActive || !!consequences()">
            @if (role === 'Student' && student; as s) {
              <!-- E3 #14 / F2 #25: "Şu an durumun ne?" and "Kaçıncı sınıftasın?" as the wizard's choice buttons (ortaokul 5–8, lise 9–12). -->
              <fieldset class="choice" data-testid="choice-educationStage" [attr.aria-describedby]="describedBy('educationStage')">
                <legend>Şu an durumun ne?</legend>
                <div class="chips">@for (item of stages; track item.value) {
                  <label class="chip" [class.is-selected]="stage === item.value"><input type="radio" name="educationStage" [value]="item.value" [ngModel]="stage" (ngModelChange)="changeStage($event)" [attr.aria-invalid]="invalid('educationStage')">{{ item.label }}</label>
                }</div>
                @if (fieldError('educationStage'); as message) { <small class="field-error" id="educationStage-error">{{ message }}</small> }
              </fieldset>
              @if (isStudentStage(stage)) {
                <fieldset class="choice" data-testid="choice-gradeLevel" [attr.aria-describedby]="describedBy('gradeLevel')">
                  <legend>Kaçıncı sınıftasın?</legend>
                  <div class="chips">@for (grade of gradesFor(stage); track grade) {
                    <label class="chip" [class.is-selected]="s.gradeLevel === grade"><input type="radio" name="gradeLevel" [value]="grade" [(ngModel)]="s.gradeLevel" (ngModelChange)="dirty = true" [attr.aria-invalid]="invalid('gradeLevel')">{{ grade }}. sınıf</label>
                  }</div>
                  @if (fieldError('gradeLevel'); as message) { <small class="field-error" id="gradeLevel-error">{{ message }}</small> }
                </fieldset>
              }
              @if (clearsSchoolNotice(); as notice) { <p role="status" class="notice" data-testid="education-clears-school">{{ notice }}</p> }
              @if (!setupMode) {
              @if (stage === 'HighSchoolGraduate' || stage === 'UniversityGraduate') {
                <label>Mezuniyet yılı (isteğe bağlı) <input name="graduationYear" type="number" min="1900" [max]="year" [(ngModel)]="s.graduationYear" (ngModelChange)="dirty = true" [attr.aria-invalid]="invalid('graduationYear')" [attr.aria-describedby]="describedBy('graduationYear')">@if (fieldError('graduationYear'); as message) { <small class="field-error" id="graduationYear-error">{{ message }}</small> }</label>
              }
              @if (isStudentStage(stage)) {
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
              }
              <label>Doğum tarihi (isteğe bağlı) <input name="birthDate" type="date" [max]="today" [(ngModel)]="s.birthDate" (ngModelChange)="dirty = true" [attr.aria-invalid]="invalid('birthDate')" [attr.aria-describedby]="describedBy('birthDate')">@if (fieldError('birthDate'); as message) { <small class="field-error" id="birthDate-error">{{ message }}</small> }</label>
              <!-- F2 #25: the city and district are the wizard's searchable lists. -->
              <div class="place" data-testid="home-place">
                <span>Yaşadığın yer: <strong>{{ homePlace(s) }}</strong></span>
                @if (!provinces()) { <span><button type="button" (click)="loadProvinces()">{{ s.homeCity ? 'Değiştir' : 'Seç' }}</button></span> }
                @else {
                  @if (!homeProvince) {
                    <app-onboarding-combobox label="Hangi şehirde yaşıyorsun?" placeholder="Şehir adını yaz" [options]="provinceOptions()" (picked)="pickProvince($event.value)" />
                  } @else {
                    <span>Şehir: <strong>{{ provinceName() }}</strong> <button type="button" (click)="pickProvince('')">Şehri değiştir</button></span>
                    @if (districtOptions().length) {
                      @for (key of [homeProvince]; track key) {
                        <app-onboarding-combobox [label]="provinceName() + ' içinde hangi ilçedesin?'" placeholder="İlçe adını yaz" [options]="districtOptions()" (picked)="pickDistrict($event)"
                          [invalid]="!!fieldError('homeDistrict')" [describedBy]="describedBy('homeDistrict')" />
                      }
                    } @else { <p role="status">İlçeler yükleniyor…</p> }
                  }
                  @if (fieldError('homeDistrict'); as message) { <small class="field-error" id="homeDistrict-error">{{ message }}</small> }
                  <span><button type="button" (click)="closePlacePicker()">Vazgeç</button></span>
                }
              </div>
              @if (diplomaApplies(s)) {
                <label>Diploma notu (YKS, isteğe bağlı) <input name="diplomaGrade" type="number" min="50" max="100" step="0.01" [(ngModel)]="s.diplomaGrade" (ngModelChange)="dirty = true" [attr.aria-invalid]="invalid('diplomaGrade')" [attr.aria-describedby]="describedBy('diplomaGrade')">@if (fieldError('diplomaGrade'); as message) { <small class="field-error" id="diplomaGrade-error">{{ message }}</small> }</label>
                <label class="check"><input name="diplomaEstimated" type="checkbox" [(ngModel)]="s.diplomaGradeEstimated" (ngModelChange)="dirty = true"> Diploma notum tahmini</label>
              }
              <label>Hakkımda (isteğe bağlı) <textarea name="studentBio" maxlength="2000" [(ngModel)]="s.bio" (ngModelChange)="dirty = true"></textarea></label>
              <label class="check"><input name="shareProgress" type="checkbox" [(ngModel)]="s.shareProgressWithTeachers" (ngModelChange)="dirty = true"> Çalışma ilerlememi yetkili öğretmenlerle paylaş</label>
              }
            }
            @if (role === 'Teacher' && teacher; as t) {
              <label>Unvan (isteğe bağlı) <input name="title" maxlength="100" [(ngModel)]="t.title" (ngModelChange)="dirty = true"></label>
              <label>Branşlar (virgülle ayırın) <input name="subjects" maxlength="5000" [(ngModel)]="subjects" (ngModelChange)="dirty = true"></label>
              <label>Deneyim (yıl, isteğe bağlı) <input name="experience" type="number" min="0" max="100" [(ngModel)]="t.experienceYears" (ngModelChange)="dirty = true" [attr.aria-invalid]="invalid('experience')" [attr.aria-describedby]="describedBy('experience')">@if (fieldError('experience'); as message) { <small class="field-error" id="experience-error">{{ message }}</small> }</label>
              <label>Hakkımda (isteğe bağlı) <textarea name="teacherBio" maxlength="2000" [(ngModel)]="t.bio" (ngModelChange)="dirty = true"></textarea></label>
            }
            <button type="submit" [disabled]="!dirty || !!consequences()">{{ working() ? 'Kaydediliyor…' : 'Profili kaydet' }}</button>
          </fieldset>
        </form>
        @if (consequences(); as lines) {
          <!-- E4 #23: what the save removes or leaves not fitting, asked in the page (no dialog). -->
          <div class="notice" role="group" aria-labelledby="save-consequences-title" data-testid="save-consequences">
            <p id="save-consequences-title"><strong>Kaydetmeden önce bil:</strong></p>
            <ul>@for (line of lines; track line) { <li>{{ line }}</li> }</ul>
            <button type="button" [disabled]="working()" (click)="confirmSave()">Evet, kaydet</button>
            <button type="button" [disabled]="working()" (click)="cancelSave()">Vazgeç</button>
          </div>
        }
      }
    </section>
    @if (!setupMode) { <section class="profile-card" aria-labelledby="coaching-invitations-title">
      <h2 id="coaching-invitations-title">Koçluk davetlerim</h2>
      @if (invitationError()) { <p role="alert" class="error">{{ invitationError() }}</p>
        <button type="button" [disabled]="working() || invitationLoading()" (click)="loadInvitations()">Yeniden dene</button> }
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
    </section> }`,
  styles: [`:host{display:block;color:inherit}.profile-card{padding:1.5rem;margin-bottom:1rem;border:1px solid #d6deea;border-radius:1rem;background:var(--surface,#fff);color:#16243c}h2{margin-top:0}fieldset{border:0;padding:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:1rem}label{display:grid;gap:.4rem}input,textarea,select,button{font:inherit;padding:.6rem;border:1px solid #bcc8da;border-radius:.5rem;max-width:100%;box-sizing:border-box}button{cursor:pointer;background:#edf3fb;margin:.4rem .4rem .4rem 0}button:disabled{opacity:.55;cursor:default}.check{display:flex;align-items:center}.place{display:grid;gap:.4rem}.error{color:#a21825}article{border-top:1px solid #d6deea;padding-top:1rem}input:focus-visible,select:focus-visible,textarea:focus-visible,button:focus-visible{outline:3px solid #3972c6;outline-offset:2px}.notice{padding:.6rem .8rem;border-radius:.5rem;background:#fff6e0}.field-error{color:#a21825}.choice{grid-column:1/-1;display:block;border:0;margin:0;padding:0}.choice legend{padding:0;margin-bottom:.4rem}.chips{display:flex;flex-wrap:wrap;gap:.5rem}.chip{display:inline-flex;align-items:center;gap:.4rem;padding:.45rem .8rem;border:1px solid #bcc8da;border-radius:999px;cursor:pointer}.chip.is-selected{border-color:#3972c6;background:#edf3fb;font-weight:600}.chip input{margin:0;padding:0}input[aria-invalid=true],select[aria-invalid=true]{border-color:#a21825}:host ::ng-deep .ob-combo{position:relative}:host ::ng-deep .ob-combo input{width:100%}:host ::ng-deep .ob-combo-list{list-style:none;margin:.35rem 0 0;padding:.25rem;max-height:16rem;overflow:auto;border:1px solid #bcc8da;border-radius:.5rem;background:var(--surface,#fff)}:host ::ng-deep .ob-combo-list li{display:grid;padding:.45rem .6rem;border-radius:.4rem;cursor:pointer}:host ::ng-deep .ob-combo-list li.is-active,:host ::ng-deep .ob-combo-list li:hover{background:#edf3fb}:host ::ng-deep .ob-combo-list small,:host ::ng-deep .ob-combo-empty{color:#5a6a82}:host ::ng-deep .ob-combo-more button{border:0;background:none;text-decoration:underline}`]
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
  /** The student menu (absent in the staff portal). */
  private readonly navigation = inject(CoachingStudentNavigationService, { optional: true });
  readonly loading = signal(false); readonly working = signal(false); readonly ready = signal(false);
  readonly error = signal<string | null>(null); readonly success = signal<string | null>(null);
  readonly invitations = signal<Invitation[]>([]); readonly invitationError = signal<string | null>(null); readonly invitationLoading = signal(false);
  student: StudentProfile | null = null; teacher: TeacherProfile | null = null; subjects = ''; dirty = false;
  readonly provinces = signal<Place[] | null>(null); readonly districts = signal<Place[]>([]);
  homeProvince = ''; homeDistrictId = ''; private savedDiploma: number | null = null; private savedEducation = ''; private examAsksDiploma = true;
  /** E3 #14: the status choice ("Şu an durumun ne?") and the one saved on the server. */
  readonly stages = EDUCATION_STAGES; stage: EducationStage | '' = ''; private savedStage: EducationStage | '' = '';
  private savedSchool: string | null = null; private savedBranch: string | null = null;
  readonly isStudentStage = isStudentStage; readonly gradesFor = gradesFor;
  readonly clearsSchoolText = 'Öğrenci olmadığını seçersen kaydettiğinde sınıf, okul ve şube bilgin silinecek.';
  /** Paket D (#7): the school picker (the wizard's catalog list) and the school picked or removed but not saved yet. */
  readonly schoolPicker = signal(false); readonly cityPicker = signal(false);
  readonly schoolCity = signal(''); readonly schoolCityLabel = signal('');
  readonly schoolCities = signal<ComboOption[]>([]); readonly pickedSchool = signal<PickedSchool | null>(null); readonly schoolRemoved = signal(false);
  /** Paket D (#8): after a grade change the exam in the study settings no longer fits. */
  readonly gradeNotice = signal(false);
  /** E4 #24: the message of each field the last save attempt refused. */
  readonly fieldErrors = signal<Partial<Record<FieldName, string>>>({});
  /** E4 #23: the consequences the student confirms in the page before the save is sent. */
  readonly consequences = signal<string[] | null>(null);
  private pending: { payload: object; impact: EducationImpact | null } | null = null;
  /** The profile as the server last sent it (the form edits a copy). */
  private savedProfile: StudentProfile | null = null;
  /** E4 #26: the student's own target named by the last save whose exam no longer fits. */
  readonly savedImpactTarget = signal<string | null>(null);
  readonly year = new Date().getFullYear();
  /** Today in Türkiye (YYYY-MM-DD). */
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(new Date());
  private get base() { return `${this.apiUrl}/coaching`; }
  ngOnInit() { this.loadProfile(); if (!this.setupMode) this.loadInvitations(); }

  private loadProfile() {
    this.loading.set(true); this.ready.set(false); this.error.set(null);
    this.http.get<{ data: OwnProfile }>(`${this.base}/profiles/me`).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false)))
      .subscribe({ next: response => { this.apply(response.data); this.ready.set(true); }, error: e => this.error.set(this.message(e, this.t('Koçluk profili yüklenemedi. Yeniden deneyin.', 'Profilin yüklenemedi. Yeniden dene.'))) });
  }
  private apply(profile: OwnProfile) {
    this.student = profile.student ? { ...profile.student } : null;
    this.savedProfile = profile.student ? { ...profile.student } : null; this.cancelSave();
    this.stage = this.savedStage = this.student ? stageOf(this.student.educationStatus, this.student.gradeLevel) : '';
    if (this.student && !this.stage) this.student.gradeLevel = null;
    this.savedSchool = this.student?.schoolName || null; this.savedBranch = this.student?.classBranch || null;
    if (profile.examAsksDiploma !== undefined) this.examAsksDiploma = profile.examAsksDiploma;
    this.savedDiploma = this.student?.diplomaGrade ?? null; this.provinces.set(null); this.homeProvince = ''; this.homeDistrictId = '';
    this.savedEducation = this.education(this.student); this.closeSchoolPicker(); this.pickedSchool.set(null); this.schoolRemoved.set(false);
    this.teacher = profile.teacher ? { ...profile.teacher } : null; this.subjects = this.teacher?.subjects?.join(', ') ?? ''; this.dirty = false;
  }
  loadProvinces() {
    this.http.get<Place[]>(`${this.apiUrl}/locations/provinces`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: list => this.provinces.set(list), error: () => this.error.set(this.t('Şehir listesi yüklenemedi.', 'Şehir listesi yüklenemedi. Yeniden dene.')) });
  }
  /** F2 #25: the wizard's searchable lists for the home city and district. */
  readonly provinceOptions = computed<ComboOption[]>(() => (this.provinces() ?? []).map(p => ({ value: p.id, label: p.name })));
  readonly districtOptions = computed<ComboOption[]>(() => this.districts().map(d => ({ value: d.id, label: d.name })));
  provinceName() { return this.provinces()?.find(p => p.id === this.homeProvince)?.name ?? ''; }
  /** The place on the card: the one picked (not saved yet) or the saved one. */
  homePlace(s: StudentProfile) {
    const district = this.districts().find(d => d.id === this.homeDistrictId);
    if (this.homeProvince && district) return `${this.provinceName()} / ${district.name}`;
    return s.homeCity ? s.homeCity + (s.homeDistrict ? ' / ' + s.homeDistrict : '') : 'seçilmedi';
  }
  pickDistrict(option: ComboOption) { this.homeDistrictId = option.value; this.dirty = true; }
  /** Leaves the saved place as it is. */
  closePlacePicker() { this.provinces.set(null); this.homeProvince = ''; this.homeDistrictId = ''; this.districts.set([]); }
  pickProvince(id: string) {
    this.homeProvince = id; this.homeDistrictId = ''; this.districts.set([]); this.dirty = true;
    if (!id) return;
    this.http.get<Place[]>(`${this.apiUrl}/locations/provinces/${encodeURIComponent(id)}/districts`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: list => this.districts.set(list), error: () => this.error.set(this.t('İlçe listesi yüklenemedi.', 'İlçe listesi yüklenemedi. Yeniden dene.')) });
  }
  // ---------- Paket D (#7): the school from the catalog ----------
  /** As in the wizard: the own-school list is the high-school catalog (lise students; E3 #14: graduates have no school). */
  schoolApplies(_s: StudentProfile) { return this.stage === 'high'; }
  /** E3 #14: a new status keeps only a grade that fits it; leaving the student statuses clears the grade, school and branch on save. */
  changeStage(stage: EducationStage | '') {
    this.stage = stage; this.dirty = true;
    if (this.student && !gradesFor(stage).includes(Number(this.student.gradeLevel))) this.student.gradeLevel = null;
    if (stage !== 'high') this.closeSchoolPicker();
  }
  clearsSchool() { return isStudentStage(this.savedStage) && this.stage !== '' && !isStudentStage(this.stage); }
  /** E3 review L4: from lise to ortaokul the stored lise and its branch are dropped (the catalog lists high schools only). */
  dropsLise() {
    const s = this.student;
    return !!s && this.savedStage === 'high' && this.stage === 'middle' && !this.schoolRemoved() && !!(this.savedSchool || this.savedBranch);
  }
  readonly dropsLiseText = 'Ortaokul öğrencisi olduğunu seçersen kaydettiğinde kayıtlı okulun ve şuben silinecek.';
  clearsSchoolNotice() { return this.clearsSchool() ? this.clearsSchoolText : this.dropsLise() ? this.dropsLiseText : null; }
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
  /**
   * The wizard's rule: the diploma grade is asked for 11th, 12th grade and of a lise graduate (YKS; the server drops it for other
   * exams). G1 (07.11.2026): university students and graduates no longer choose YKS.
   */
  diplomaApplies(s: StudentProfile) {
    return this.examAsksDiploma && (this.stage === 'HighSchoolGraduate' || isStudentStage(this.stage) && [11, 12].includes(Number(s.gradeLevel)));
  }
  private education(s: StudentProfile | null) { return s ? `${this.stage}:${isStudentStage(this.stage) ? s.gradeLevel ?? '' : ''}` : ''; }
  async reload() { if (this.dirty && !await this.toaster.confirm('Kaydedilmemiş profil değişikliklerinden vazgeçilsin mi?')) return; this.loadProfile(); }
  provision() {
    if (this.working() || !this.ready()) return;
    this.working.set(true); this.error.set(null);
    this.http.post(`${this.base}/profiles/me/provision`, { role: this.role }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false)))
      .subscribe({ next: () => this.loadProfile(), error: e => this.error.set(this.message(e, 'Koçluk profili oluşturulamadı.')) });
  }
  fieldError(name: FieldName) { return this.fieldErrors()[name] ?? null; }
  invalid(name: FieldName) { return this.fieldErrors()[name] ? 'true' : null; }
  describedBy(name: FieldName) { return this.fieldErrors()[name] ? `${name}-error` : null; }
  hasFieldErrors() { return Object.keys(this.fieldErrors()).length > 0; }

  /**
   * E4 #23/#24: every attempt clears the previous message; values the server would refuse are named next to their field and in a
   * summary (the form is novalidate, so the browser never swallows a save). A new education first asks the server what it removes
   * or leaves not fitting; the student confirms that in the page before anything is saved.
   */
  save(): void {
    if (this.working() || !this.ready()) return;
    this.error.set(null); this.success.set(null); this.gradeNotice.set(false); this.fieldErrors.set({}); this.consequences.set(null); this.pending = null;
    const payload = this.role === 'Student' ? this.studentPayload() : this.teacherPayload();
    if (!payload) return;
    const educationChanged = this.role === 'Student' && !!this.student && this.education(this.student) !== this.savedEducation;
    if (!educationChanged || this.setupMode) { this.put(payload, null); return; }
    const education = educationRequest(this.stage as EducationStage, this.student!.gradeLevel);
    this.working.set(true);
    this.http.post<{ data: EducationImpact }>(`${this.base}/profiles/me/student/impact`, education).pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: response => {
          this.working.set(false);
          this.ask(payload, response.data, this.consequenceLines(response.data));
        },
        error: e => {
          this.working.set(false);
          // E4 review: an API without the check (deployed later) still saves, after the warning the page knows itself.
          if (e?.status === 404) { this.ask(payload, null, this.clearsSchoolNotice() ? [this.clearsSchoolNotice()!] : []); return; }
          this.error.set(this.message(e, 'Değişikliğin sınavına ve hedefine etkisi kontrol edilemedi. Bağlantını kontrol edip yeniden dene.'));
        }
      });
  }
  confirmSave() {
    const pending = this.pending; if (!pending || this.working()) return;
    this.pending = null; this.consequences.set(null); this.put(pending.payload, pending.impact);
  }
  cancelSave() { this.pending = null; this.consequences.set(null); }
  private ask(payload: object, impact: EducationImpact | null, lines: string[]) {
    if (lines.length) { this.pending = { payload, impact }; this.consequences.set(lines); }
    else this.put(payload, impact);
  }

  private consequenceLines(impact: EducationImpact): string[] {
    const lines: string[] = [];
    const school = this.clearsSchoolNotice(); if (school) lines.push(school);
    if (impact.diplomaGradeRemoved !== null) lines.push(`Diploma notun (${decimal.format(impact.diplomaGradeRemoved)}) silinecek; bu eğitim durumunda ve sınıfta diploma notu sorulmuyor.`);
    if (impact.examNoLongerFits) {
      lines.push(`Seçili sınavın (${impact.exam}) ${isStudentStage(this.stage) ? 'bu sınıfa' : 'bu eğitim durumuna'} uymuyor; kaydedersen Çalışma tercihlerim'de yeni sınavını seçmen gerekecek.`);
      if (impact.target) lines.push(`Hedefin (${impact.target}) yeni sınavına uymazsa, sınavını değiştirdiğinde kaldırılır.`);
    }
    return lines;
  }

  private studentPayload(): object | null {
    const s = this.student; if (!s) return null;
    const stage = this.stage; const errors: Partial<Record<FieldName, string>> = {};
    if (!stage) errors.educationStage = 'Şu an durumunu seç.';
    else if (isStudentStage(stage) && !gradesFor(stage).includes(Number(s.gradeLevel))) errors.gradeLevel = 'Sınıfını seç.';
    const graduation = s.graduationYear;
    if ((stage === 'HighSchoolGraduate' || stage === 'UniversityGraduate') && graduation !== null && graduation !== undefined && String(graduation) !== ''
      && (!Number.isInteger(Number(graduation)) || Number(graduation) < 1900 || Number(graduation) > this.year))
      errors.graduationYear = `Mezuniyet yılı 1900 ile ${this.year} arasında olmalı.`;
    if (s.birthDate && s.birthDate > this.today) errors.birthDate = 'Doğum tarihin gelecekte olamaz.';
    else if (s.birthDate && s.birthDate < '1900-01-01') errors.birthDate = 'Doğum tarihini kontrol et.';
    if (this.homeProvince && !this.homeDistrictId) errors.homeDistrict = 'Şehrinle birlikte ilçeni de seç.';
    const diploma = this.diplomaApplies(s);
    const hasGrade = s.diplomaGrade !== null && s.diplomaGrade !== undefined && String(s.diplomaGrade) !== '';
    if (diploma && hasGrade && (!Number.isFinite(Number(s.diplomaGrade)) || Number(s.diplomaGrade) < 50 || Number(s.diplomaGrade) > 100))
      errors.diplomaGrade = 'Diploma notu 50 ile 100 arasında olmalı.';
    if (this.refuse(errors)) return null;
    const education = educationRequest(stage as EducationStage, s.gradeLevel); const student = isStudentStage(stage);
    // Paket D (#7): the saved school is sent back as it is, a picked one by its catalog id (the server fills it from the catalog).
    const dropLise = this.dropsLise();
    const removed = this.schoolRemoved() || !student || dropLise; const picked = student && !dropLise ? this.pickedSchool() : null;
    // Paket D (#8): the diploma grade only where the wizard asks it; the server drops it elsewhere.
    return { expectedVersion: s.version, ...education,
      graduationYear: stage === 'HighSchoolGraduate' || stage === 'UniversityGraduate' ? s.graduationYear || null : null, schoolName: removed ? null : s.schoolName || null,
      schoolCity: removed ? null : s.schoolCity || null, schoolDistrict: removed ? null : s.schoolDistrict || null, schoolType: removed ? null : s.schoolType || null,
      ...(picked && !removed ? { schoolId: picked.id } : {}),
      classBranch: student && !(dropLise && (s.classBranch || null) === this.savedBranch) ? s.classBranch || null : null, birthDate: s.birthDate || null,
      bio: s.bio || null, avatarUrl: s.avatarUrl || null, shareProgressWithTeachers: s.shareProgressWithTeachers,
      // Paket C: only a changed place is sent (checked against the shared list); an emptied diploma grade is removed.
      ...(this.homeProvince && this.homeDistrictId ? { homeProvinceId: this.homeProvince, homeDistrictId: this.homeDistrictId } : {}),
      ...(!diploma ? {} : hasGrade ? { diplomaGrade: Number(s.diplomaGrade), diplomaGradeEstimated: !!s.diplomaGradeEstimated }
        : { clearDiplomaGrade: this.savedDiploma !== null }) };
  }
  private teacherPayload(): object | null {
    const t = this.teacher; if (!t) return null;
    const experience = t.experienceYears;
    if (experience !== null && experience !== undefined && String(experience) !== ''
      && (!Number.isInteger(Number(experience)) || Number(experience) < 0 || Number(experience) > 100)
      && this.refuse({ experience: 'Deneyim 0 ile 100 yıl arasında olmalıdır.' })) return null;
    return { expectedVersion: t.version, title: t.title || null, subjects: this.subjects.split(',').map(x => x.trim()).filter(Boolean),
      experienceYears: experience ?? null, bio: t.bio || null, avatarUrl: t.avatarUrl || null };
  }
  /** E4 #24: names the refused fields (next to each field and in the summary); true when anything was refused. */
  private refuse(errors: Partial<Record<FieldName, string>>): boolean {
    const messages = Object.values(errors);
    if (!messages.length) return false;
    this.fieldErrors.set(errors);
    this.error.set(`${this.t('Profiliniz kaydedilmedi.', 'Profilin kaydedilmedi.')} ${messages.join(' ')}`);
    setTimeout(() => (this.host.nativeElement.querySelector('[aria-invalid="true"]') as HTMLElement | null)?.focus());
    return true;
  }

  private put(payload: object, impact: EducationImpact | null) {
    if (this.working()) return;
    this.working.set(true); this.error.set(null); this.success.set(null); this.gradeNotice.set(false);
    const before = this.role === 'Student' ? this.savedProfile : null;
    const beforeStage = this.savedStage; const beforeSchool = this.savedSchool; const beforeBranch = this.savedBranch; const beforeDiploma = this.savedDiploma;
    const gradeChanged = this.role === 'Student' && !!this.student && this.education(this.student) !== this.savedEducation;
    this.http.put<{ data: StudentProfile | TeacherProfile }>(`${this.base}/profiles/me/${this.role.toLowerCase()}`, payload)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false))).subscribe({ next: response => {
        this.apply({ student: this.role === 'Student' ? response.data as StudentProfile : this.student,
          teacher: this.role === 'Teacher' ? response.data as TeacherProfile : this.teacher });
        // E4 #23: the confirmation says what changed and what was removed.
        const changes = this.role === 'Student' && before ? this.changesOf(beforeStage, before, response.data as StudentProfile, beforeSchool, beforeBranch, beforeDiploma) : [];
        this.success.set(this.t('Koçluk profiliniz kaydedildi.', 'Profilin kaydedildi.') + (changes.length ? ` ${changes.join('. ')}.` : ''));
        this.savedImpactTarget.set(impact?.examNoLongerFits ? impact.target : null);
        this.saved.emit();
        // Paket D (#8): the study settings follow the new grade; when the exam no longer fits, say so and link there.
        if (gradeChanged && !this.setupMode) this.checkStudySettings();
      }, error: e => this.error.set(this.message(e, this.t('Profil kaydedilemedi. Bilgilerinizi kontrol edin.', 'Profil kaydedilemedi. Bilgilerini kontrol et.'))) });
  }

  private changesOf(beforeStage: EducationStage | '', before: StudentProfile, after: StudentProfile, school: string | null, branch: string | null, diploma: number | null): string[] {
    const changes: string[] = [];
    const stage = stageOf(after.educationStatus, after.gradeLevel);
    if (stage !== beforeStage) {
      const label = EDUCATION_STAGES.find(x => x.value === stage)?.label;
      if (label) changes.push(`Eğitim durumun: ${label}${isStudentStage(stage) && after.gradeLevel ? `, ${after.gradeLevel}. sınıf` : ''}`);
    } else if (isStudentStage(stage) && after.gradeLevel !== before.gradeLevel && after.gradeLevel) changes.push(`Sınıfın: ${after.gradeLevel}. sınıf`);
    const schoolGone = !!school && !after.schoolName; const branchGone = !!branch && !after.classBranch;
    if (schoolGone && branchGone) changes.push('Okulun ve şuben silindi');
    else if (schoolGone) changes.push('Okulun silindi');
    else if (branchGone) changes.push('Şuben silindi');
    if (after.schoolName && after.schoolName !== school) changes.push(`Okulun: ${after.schoolName}`);
    if (diploma !== null && (after.diplomaGrade ?? null) === null) changes.push(`Diploma notun (${decimal.format(diploma)}) silindi`);
    else if (after.diplomaGrade !== null && after.diplomaGrade !== undefined && after.diplomaGrade !== diploma) changes.push(`Diploma notun: ${decimal.format(after.diplomaGrade)}`);
    if ((after.homeCity ?? null) !== (before.homeCity ?? null) || (after.homeDistrict ?? null) !== (before.homeDistrict ?? null))
      changes.push(`Yaşadığın yer: ${[after.homeCity, after.homeDistrict].filter(Boolean).join(' / ')}`);
    if ((after.birthDate ?? null) !== (before.birthDate ?? null)) changes.push(after.birthDate ? `Doğum tarihin: ${coachingDate(after.birthDate)}` : 'Doğum tarihin silindi');
    return changes;
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
      .subscribe({ next: () => {
        this.loadInvitations();
        if (action !== 'accept') return;
        this.loadProfile();
        // F2/F3: a new teacher or institution can open menu items; the refresh is forced past a request in flight. Called this way it
        // works with refresh(reset) and refresh({ force, reset }) (PR #122).
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        if (this.role === 'Student') (this.navigation as any)?.refresh?.({ force: true });
      }, error: e => this.invitationError.set(this.message(e, this.t('Davet işlenemedi. Koçluk rolünüzü ve davetin geçerliliğini kontrol edin.', 'Davet yanıtlanamadı. Davetin süresi dolmuş olabilir.'))) });
  }
  async canLeavePage() { return !this.working() && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş Koçluk profil değişikliklerinden vazgeçilsin mi?')); }
  /** Teachers are addressed formally (siz), students informally (sen). */
  t(formal: string, informal: string) { return this.role === 'Student' ? informal : formal; }
  private message(e: { error?: { message?: unknown; detail?: unknown } }, fallback: string) {
    const text = e.error?.message ?? e.error?.detail; return typeof text === 'string' ? text : fallback;
  }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
