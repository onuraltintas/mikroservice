import { coachingFeedback, coachingToaster } from '../src/app/features/coaching-portal/coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, inject, signal, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

interface StudentProfile {
  version: number; isActive: boolean; educationStatus: string; gradeLevel: number | null;
  schoolName?: string | null; schoolCity?: string | null; schoolDistrict?: string | null; schoolType?: string | null;
  classBranch?: string | null; graduationYear?: number | null; birthDate?: string | null;
  bio?: string | null; avatarUrl?: string | null; shareProgressWithTeachers: boolean;
}
interface TeacherProfile { version: number; isActive: boolean; title?: string | null; subjects: string[]; experienceYears?: number | null; bio?: string | null; avatarUrl?: string | null; }
interface OwnProfile { student: StudentProfile | null; teacher: TeacherProfile | null; }
interface Invitation { id: string; inviterEmail: string; type: string; message?: string; expiresAt: string; }

@Component({
  selector: 'edu-coaching-profile', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section aria-labelledby="coaching-profile-title" class="profile-card">
      <h2 id="coaching-profile-title">Koçluk profilim</h2>
      <p>Eğitim ve uzmanlık bilgilerinizi burada düzenleyebilirsiniz. Ad, e-posta ve şifre ortak hesap ayarlarınızdadır.</p>
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @if (success()) { <p role="status">{{ success() }}</p> }
      @if (loading()) { <p role="status">Profil yükleniyor…</p> }
      @if (ready() && !student && role === 'Student' || ready() && !teacher && role === 'Teacher') {
        <p>Koçluk profiliniz henüz oluşturulmamış.</p>
        <button type="button" [disabled]="working()" (click)="provision()">Koçluk profilimi oluştur</button>
      }
      @if (ready() && (role === 'Student' ? student : teacher); as profile) {
        @if (!profile.isActive) { <p role="alert">Koçluk profiliniz pasif. Yöneticinizle görüşün.</p> }
        <form #profileForm="ngForm" (ngSubmit)="profileForm.valid && save()" (ngModelChange)="dirty = true">
          <fieldset [disabled]="working() || !profile.isActive">
            @if (role === 'Student' && student; as s) {
              <label>Eğitim durumu <select name="educationStatus" [(ngModel)]="s.educationStatus" (ngModelChange)="dirty = true" required>
                <option value="unknown">Seçin</option><option value="student">Öğrenci</option><option value="graduate">Mezun</option>
              </select></label>
              @if (s.educationStatus === 'student') {
                <label>Sınıf <input name="gradeLevel" type="number" min="1" max="12" required [(ngModel)]="s.gradeLevel" (ngModelChange)="dirty = true"></label>
              }
              @if (!setupMode) {
              @if (s.educationStatus === 'graduate') {
                <label>Mezuniyet yılı (isteğe bağlı) <input name="graduationYear" type="number" min="1900" [max]="year" [(ngModel)]="s.graduationYear" (ngModelChange)="dirty = true"></label>
              }
              <label>Okul adı (isteğe bağlı) <input name="schoolName" maxlength="255" [(ngModel)]="s.schoolName" (ngModelChange)="dirty = true"></label>
              <label>Şehir (isteğe bağlı) <input name="schoolCity" maxlength="100" [(ngModel)]="s.schoolCity" (ngModelChange)="dirty = true"></label>
              <label>İlçe (isteğe bağlı) <input name="schoolDistrict" maxlength="100" [(ngModel)]="s.schoolDistrict" (ngModelChange)="dirty = true"></label>
              <label>Okul türü (isteğe bağlı) <input name="schoolType" maxlength="100" [(ngModel)]="s.schoolType" (ngModelChange)="dirty = true"></label>
              <label>Şube (isteğe bağlı) <input name="classBranch" maxlength="30" [(ngModel)]="s.classBranch" (ngModelChange)="dirty = true"></label>
              <label>Doğum tarihi (isteğe bağlı) <input name="birthDate" type="date" [max]="today" [(ngModel)]="s.birthDate" (ngModelChange)="dirty = true"></label>
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
      @else if (!invitations().length && !invitationError()) { <p>Bekleyen Koçluk davetiniz yok.</p> }
      @for (invite of invitations(); track invite.id) {
        <article><strong>{{ invite.inviterEmail }}</strong><p>{{ invite.type === 'TeacherToInstitution' ? 'Kurum öğretmen daveti' : invite.type === 'StudentToInstitution' ? 'Kurum öğrenci daveti' : 'Koç–öğrenci daveti' }}</p>
          @if (invite.message) { <p>{{ invite.message }}</p> }
          <p>Son tarih: {{ invite.expiresAt | date:'dd.MM.yyyy' }}</p>
          <button type="button" [disabled]="working()" (click)="respond(invite.id, 'accept')">Kabul et</button>
          <button type="button" [disabled]="working()" (click)="respond(invite.id, 'reject')">Reddet</button>
        </article>
      }
      <button type="button" [disabled]="working() || invitationLoading()" (click)="loadInvitations()">Davetleri yenile</button>
    </section> }`,
  styles: [`:host{display:block;color:inherit}.profile-card{padding:1.5rem;margin-bottom:1rem;border:1px solid #d6deea;border-radius:1rem;background:var(--surface,#fff);color:#16243c}h2{margin-top:0}fieldset{border:0;padding:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:1rem}label{display:grid;gap:.4rem}input,textarea,select,button{font:inherit;padding:.6rem;border:1px solid #bcc8da;border-radius:.5rem;max-width:100%;box-sizing:border-box}button{cursor:pointer;background:#edf3fb;margin:.4rem .4rem .4rem 0}button:disabled{opacity:.55;cursor:default}.check{display:flex;align-items:center}.error{color:#a21825}article{border-top:1px solid #d6deea;padding-top:1rem}input:focus-visible,select:focus-visible,textarea:focus-visible,button:focus-visible{outline:3px solid #3972c6;outline-offset:2px}`]
})
export class CoachingProfileComponent implements OnInit {
  private readonly toaster = coachingToaster();
  @Input({ required: true }) apiUrl = '';
  @Input() role: 'Student' | 'Teacher' = 'Student';
  @Input() setupMode = false;
  readonly saved = output<void>();
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly loading = signal(false); readonly working = signal(false); readonly ready = signal(false);
  readonly error = signal<string | null>(null); readonly success = signal<string | null>(null);
  readonly invitations = signal<Invitation[]>([]); readonly invitationError = signal<string | null>(null); readonly invitationLoading = signal(false);
  student: StudentProfile | null = null; teacher: TeacherProfile | null = null; subjects = ''; dirty = false;
  readonly year = new Date().getFullYear(); readonly today = new Date().toISOString().slice(0, 10);
  private get base() { return `${this.apiUrl}/coaching`; }
  ngOnInit() { this.loadProfile(); if (!this.setupMode) this.loadInvitations(); }

  private loadProfile() {
    this.loading.set(true); this.ready.set(false); this.error.set(null);
    this.http.get<{ data: OwnProfile }>(`${this.base}/profiles/me`).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false)))
      .subscribe({ next: response => { this.apply(response.data); this.ready.set(true); }, error: e => this.error.set(this.message(e, 'Koçluk profili yüklenemedi. Yeniden deneyin.')) });
  }
  private apply(profile: OwnProfile) {
    this.student = profile.student ? { ...profile.student, educationStatus: profile.student.educationStatus.toLowerCase() } : null;
    this.teacher = profile.teacher ? { ...profile.teacher } : null; this.subjects = this.teacher?.subjects?.join(', ') ?? ''; this.dirty = false;
  }
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
      if (s.educationStatus === 'unknown' || s.educationStatus === 'student' && (!s.gradeLevel || s.gradeLevel < 1 || s.gradeLevel > 12)) { this.error.set('Eğitim durumunu ve öğrenciyseniz sınıfınızı seçin.'); return; }
      payload = { expectedVersion: s.version, educationStatus: s.educationStatus, gradeLevel: s.educationStatus === 'student' ? s.gradeLevel : null,
        graduationYear: s.educationStatus === 'graduate' ? s.graduationYear || null : null, schoolName: s.schoolName || null, schoolCity: s.schoolCity || null,
        schoolDistrict: s.schoolDistrict || null, schoolType: s.schoolType || null, classBranch: s.classBranch || null, birthDate: s.birthDate || null,
        bio: s.bio || null, avatarUrl: s.avatarUrl || null, shareProgressWithTeachers: s.shareProgressWithTeachers };
    } else if (this.role === 'Teacher' && this.teacher) {
      const t = this.teacher;
      payload = { expectedVersion: t.version, title: t.title || null, subjects: this.subjects.split(',').map(x => x.trim()).filter(Boolean),
        experienceYears: t.experienceYears ?? null, bio: t.bio || null, avatarUrl: t.avatarUrl || null };
    } else return;
    this.working.set(true); this.error.set(null); this.success.set(null);
    this.http.put<{ data: StudentProfile | TeacherProfile }>(`${this.base}/profiles/me/${this.role.toLowerCase()}`, payload)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false))).subscribe({ next: response => {
        this.apply({ student: this.role === 'Student' ? response.data as StudentProfile : this.student,
          teacher: this.role === 'Teacher' ? response.data as TeacherProfile : this.teacher }); this.success.set('Koçluk profiliniz kaydedildi.'); this.saved.emit();
      }, error: e => this.error.set(this.message(e, 'Profil kaydedilemedi. Bilgilerinizi kontrol edin.')) });
  }
  loadInvitations() {
    this.invitationLoading.set(true); this.invitationError.set(null);
    this.http.get<Invitation[]>(`${this.base}/invitations/my-invitations`).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.invitationLoading.set(false)))
      .subscribe({ next: rows => this.invitations.set(rows), error: e => this.invitationError.set(this.message(e, 'Davetler yüklenemedi.')) });
  }
  respond(id: string, action: 'accept' | 'reject') {
    if (this.working()) return;
    if (this.dirty) { this.invitationError.set('Davet işlemi öncesinde profil değişikliklerini kaydedin.'); return; }
    this.working.set(true); this.invitationError.set(null);
    this.http.post(`${this.base}/invitations/${encodeURIComponent(id)}/${action}`, {}).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.working.set(false)))
      .subscribe({ next: () => { this.loadInvitations(); if (action === 'accept') this.loadProfile(); }, error: e => this.invitationError.set(this.message(e, 'Davet işlenemedi. Koçluk rolünüzü ve davetin geçerliliğini kontrol edin.')) });
  }
  async canLeavePage() { return !this.working() && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş Koçluk profil değişikliklerinden vazgeçilsin mi?')); }
  private message(e: { error?: { message?: unknown; detail?: unknown } }, fallback: string) {
    const text = e.error?.message ?? e.error?.detail; return typeof text === 'string' ? text : fallback;
  }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
