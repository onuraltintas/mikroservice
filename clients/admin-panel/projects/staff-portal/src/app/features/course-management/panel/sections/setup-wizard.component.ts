import { Component, EventEmitter, OnInit, Output, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CourseManagementService } from '../../course-management.service';
import { CoursePanelApi, PanelEmailSettings, PanelStudentRow, SetupStepId } from '../panel-api.service';
import { CoursePanelState, PanelSection, formatMoney, problemText } from '../panel-state';
import { FEE_MODELS, createFeeAgreementWithInstallments } from '../fee-agreements';
import { readCsvText } from './students.component';

type StepId = SetupStepId | 'summary';
const STEPS: { id: StepId; title: string; question: string }[] = [
  { id: 'profile', title: 'Kurum', question: 'Kurumunuzu tanıyalım' },
  { id: 'teachers', title: 'Ders verenler', question: 'Derslere kimler giriyor?' },
  { id: 'rooms', title: 'Mekân', question: 'Dersler nerede yapılıyor?' },
  { id: 'courses', title: 'Kurslar ve gruplar', question: 'Hangi kursları, hangi gruplarla veriyorsunuz?' },
  { id: 'students', title: 'Öğrenciler', question: 'Öğrencilerinizi ekleyip yerleştirin' },
  { id: 'fees', title: 'Ücretler', question: 'Ücretleri nasıl alıyorsunuz?' },
  { id: 'schedule', title: 'Ders programı', question: 'Dersler haftanın hangi gün ve saatinde?' },
  { id: 'emails', title: 'Veli bildirimleri', question: 'Velilere hangi e-postalar gitsin?' },
  { id: 'summary', title: 'Özet', question: 'Kurulum özeti' }
];
const KINDS = [
  { value: 2, label: 'Kurs / dershane', hint: 'LGS, YKS, dil ve benzeri kurslar', key: 'PrivateCourse' },
  { value: 3, label: 'Etüt merkezi', hint: 'Etüt, ödev ve okul destek dersleri', key: 'StudyCenter' },
  { value: 1, label: 'Okul', hint: 'Okul bünyesindeki kurslar', key: 'School' },
  { value: 4, label: 'Çevrim içi', hint: 'Dersler internetten yapılıyor', key: 'OnlinePlatform' }
] as const;
const COURSE_IDEAS: Record<number, { name: string; subject: string }[]> = {
  2: [{ name: 'LGS Matematik', subject: 'Matematik' }, { name: 'LGS Fen Bilimleri', subject: 'Fen Bilimleri' }, { name: 'LGS Türkçe', subject: 'Türkçe' },
    { name: 'YKS Matematik', subject: 'Matematik' }, { name: 'YKS Türkçe', subject: 'Türkçe' }, { name: 'İngilizce', subject: 'İngilizce' }],
  3: [{ name: 'Ödev etüdü', subject: 'Etüt' }, { name: 'Matematik etüdü', subject: 'Matematik' }, { name: 'Okuma etüdü', subject: 'Türkçe' },
    { name: 'Fen etüdü', subject: 'Fen Bilimleri' }],
  1: [{ name: 'Destekleme ve yetiştirme', subject: 'Genel' }, { name: 'Matematik kursu', subject: 'Matematik' }, { name: 'İngilizce kursu', subject: 'İngilizce' }],
  4: [{ name: 'Canlı Matematik', subject: 'Matematik' }, { name: 'Canlı İngilizce', subject: 'İngilizce' }, { name: 'Birebir özel ders', subject: 'Özel ders' }]
};
const GROUP_IDEAS = ['5-A', '6-A', '7-A', '8-A', '8-B', '9-A', '10-A', '11-A', '12-A', 'Hafta içi', 'Hafta sonu', 'Mezun'];
const CAPACITIES = [6, 8, 10, 12, 16, 20, 25, 30];
const AMOUNTS = [500, 1000, 1500, 2000, 2500, 3000, 4000, 5000];
const DAYS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];
const TIMES = ['09:00', '10:00', '11:00', '13:00', '14:00', '15:00', '16:00', '17:00', '18:00', '19:00', '20:00'];
const DURATIONS = [{ value: 40, label: '40 dk' }, { value: 45, label: '45 dk' }, { value: 60, label: '1 saat' }, { value: 90, label: '1,5 saat' }, { value: 120, label: '2 saat' }];
const EMAIL_KINDS = [
  { kind: 'Absence', value: 1, label: 'Devamsızlık', hint: 'Öğrenci "gelmedi" işaretlenince' },
  { kind: 'HomeworkNotDone', value: 2, label: 'Yapılmayan ödev', hint: 'Ödev "yapmadı" ya da "eksik" işaretlenince' },
  { kind: 'InstallmentReminder', value: 3, label: 'Taksit hatırlatma', hint: 'Vadeden önce' },
  { kind: 'InstallmentOverdue', value: 4, label: 'Geciken taksit', hint: 'Vadeden sonra' },
  { kind: 'LessonChanged', value: 5, label: 'Ders iptali ya da saat değişikliği', hint: 'Değişiklikten 10 dakika sonra' },
  { kind: 'WeeklySchedule', value: 6, label: 'Haftalık program', hint: 'Pazar akşamı' },
  { kind: 'PaymentReceived', value: 7, label: 'Ödeme alındı', hint: 'Ödeme kaydedilince' }
];

interface FeePlan { model: number; amount: number; count: number; firstDue: string; unit: number; sessions: number; expiresOn: string; courseId: string; }
interface SchedulePlan { days: boolean[]; time: string; duration: number; roomId: string; endsOn: string; }

/**
 * Kurulum: the setup guide (20.11.2026, design approved by the user). Eight steps, one question each, mostly choices
 * to click; every step saves at once, can be skipped, and can be done later by hand in its own section. What is done
 * is read from the data (the server's /setup), so the guide picks up where the owner left off, on any device.
 */
@Component({
  selector: 'course-setup-wizard',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet],
  styleUrl: '../panel.scss',
  styles: `
    .wizard { display: grid; grid-template-columns: 240px minmax(0, 1fr); gap: 24px; align-items: start; }
    .steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; position: sticky; top: 16px; }
    .steps button { display: flex; align-items: center; gap: 10px; width: 100%; min-height: 44px; padding: 0 10px; border: 0; border-radius: 8px;
      background: transparent; color: var(--ds-text); font: inherit; text-align: left; cursor: pointer; }
    .steps button[aria-current='step'] { background: var(--ds-surface); font-weight: 600; box-shadow: inset 3px 0 0 var(--ds-accent); }
    .dot { flex: none; display: inline-grid; place-items: center; width: 26px; height: 26px; border-radius: 50%; border: 1.5px solid var(--ds-field);
      font-size: 13px; color: var(--ds-muted); background: var(--ds-surface); }
    .dot.done { background: var(--ds-success); border-color: var(--ds-success); color: var(--ds-surface); }
    .dot.skipped { border-style: dashed; }
    .step-card { padding: 24px; display: flex; flex-direction: column; gap: 18px; }
    .step-card h2 { margin: 0; font-size: 22px; }
    .lead { margin: -8px 0 0; color: var(--ds-muted); line-height: 1.6; }
    .choices { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 10px; }
    .choice { display: flex; flex-direction: column; align-items: flex-start; gap: 4px; min-height: 64px; padding: 12px 14px; border: 1.5px solid var(--ds-line);
      border-radius: 10px; background: var(--ds-surface); color: var(--ds-text); font: inherit; text-align: left; cursor: pointer; }
    .choice[aria-pressed='true'] { border-color: var(--ds-accent); box-shadow: inset 0 0 0 1px var(--ds-accent); background: var(--mk-soft, var(--ds-soft)); }
    .choice strong { font-size: 15px; } .choice span { font-size: 13px; color: var(--ds-muted); }
    .chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .chips button { min-height: 40px; padding: 0 12px; border-radius: 20px; border: 1px solid var(--ds-field); background: var(--ds-surface); color: var(--ds-text);
      font: inherit; font-size: 14px; cursor: pointer; }
    .chips button[aria-pressed='true'] { background: var(--ds-text); color: var(--ds-surface); border-color: var(--ds-text); font-weight: 600; }
    .chips button:disabled { opacity: .45; cursor: default; }
    .item { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; padding: 12px 14px; border: 1px solid var(--ds-line);
      border-radius: 10px; background: var(--ds-surface); }
    .item-main { display: flex; flex-direction: column; gap: 4px; min-width: 180px; }
    .block { display: flex; flex-direction: column; gap: 10px; padding: 14px; border: 1px solid var(--ds-line); border-radius: 10px; background: var(--ds-surface); }
    .block h3 { margin: 0; font-size: 16px; }
    .label { font-size: 13px; color: var(--ds-muted); margin: 0; }
    .inline { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 10px; }
    .inline .field { flex: 1 1 180px; }
    .foot { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; padding-top: 6px; border-top: 1px solid var(--ds-line); }
    .foot .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .progress { height: 6px; border-radius: 3px; background: var(--ds-line); overflow: hidden; }
    .progress span { display: block; height: 100%; background: var(--ds-accent); }
    .placement { display: grid; grid-template-columns: minmax(140px, 1fr) minmax(0, 3fr); gap: 8px 14px; align-items: center; }
    .placement > strong { font-weight: 500; }
    @media (max-width: 900px) { .wizard { grid-template-columns: 1fr; } .steps { position: static; flex-direction: row; overflow-x: auto; }
      .steps button { white-space: nowrap; } .steps .name { display: none; } .placement { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: no-preference) { .progress span { transition: width .3s ease; } }
  `,
  template: `
    <header class="page-head">
      <div><h1>Kurulum</h1><p>{{ progress().done }} / 8 adım tamam. Her adım hemen kaydedilir; istediğiniz adımı atlayıp sonra yapabilirsiniz.</p></div>
      <div class="actions">
        <button type="button" class="btn-secondary" (click)="later()">Daha sonra devam et</button>
      </div>
    </header>
    <div class="progress" role="progressbar" aria-label="Kurulum ilerlemesi" [attr.aria-valuenow]="progress().done" aria-valuemin="0" aria-valuemax="8"
      style="margin-bottom: 20px"><span [style.width.%]="progress().done / 8 * 100"></span></div>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }

    <div class="wizard">
      <ol class="steps" aria-label="Kurulum adımları">
        @for (step of steps; track step.id; let i = $index) {
          <li><button type="button" [attr.aria-current]="current() === step.id ? 'step' : null" (click)="goTo(step.id)">
            <span class="dot" [class.done]="status(step.id) === 'done'" [class.skipped]="status(step.id) === 'skipped'" aria-hidden="true">
              {{ status(step.id) === 'done' ? '✓' : i + 1 }}</span>
            <span class="name">{{ step.title }}<span class="sr-only">{{ statusText(step.id) }}</span></span>
          </button></li>
        }
      </ol>

      <section class="card step-card" [attr.aria-labelledby]="'step-' + current()">
        <h2 [id]="'step-' + current()" tabindex="-1">{{ stepInfo().question }}</h2>
        @switch (current()) {

          @case ('profile') {
            <p class="lead">Kurumunuzun adı panelde ve velilere giden e-postalarda görünür. Türe göre size örnek kurslar ve hazır ayarlar öneririz.</p>
            <label class="field">Kurumun adı<input [(ngModel)]="profileName" maxlength="200" autocomplete="organization"></label>
            <p class="label" id="kind-label">Kurumunuzun türü</p>
            <div class="choices" role="group" aria-labelledby="kind-label">
              @for (kind of kinds; track kind.value) {
                <button type="button" class="choice" [attr.aria-pressed]="profileKind === kind.value" (click)="profileKind = kind.value">
                  <strong>{{ kind.label }}</strong><span>{{ kind.hint }}</span></button>
              }
            </div>
          }

          @case ('teachers') {
            <p class="lead">Öğretmenlerinize davet e-postası gider. Daveti kabul etmeden de onlara grup ve ders verebilirsiniz; kabul edince hepsi onların olur.</p>
            <div class="choices" role="group" aria-label="Ders verenler">
              <button type="button" class="choice" [attr.aria-pressed]="teachMode === 'self'" (click)="teachMode = 'self'">
                <strong>Tek başıma ders veriyorum</strong><span>Grupları ve dersleri kendinize açarsınız</span></button>
              <button type="button" class="choice" [attr.aria-pressed]="teachMode === 'team'" (click)="teachMode = 'team'">
                <strong>Öğretmenlerim var</strong><span>Ad ve e-posta yazıp davet gönderin</span></button>
            </div>
            @if (teachMode === 'team') {
              <form class="inline" (ngSubmit)="invite()">
                <label class="field">Öğretmenin adı soyadı<input name="tname" [(ngModel)]="teacherName" maxlength="200" autocomplete="off"></label>
                <label class="field">E-posta adresi<input name="temail" type="email" [(ngModel)]="teacherEmail" maxlength="320" autocomplete="off"></label>
                <button type="submit" class="btn" [disabled]="busy() || !teacherName.trim() || !teacherEmail.includes('@')">Davet gönder</button>
              </form>
            }
            <div class="stack" style="gap: 8px">
              @for (teacher of state.teachers(); track teacher.userId) {
                <div class="item"><div class="item-main"><strong>{{ state.teacherName(teacher.userId) }}</strong>
                  @if (teacher.invited) { <span class="muted">{{ teacher.email }}, daveti henüz kabul etmedi</span> }</div>
                  <span class="badge">{{ teacher.role === 'Owner' ? 'Siz' : teacher.invited ? 'Davet gitti' : 'Katıldı' }}</span></div>
              }
            </div>
          }

          @case ('rooms') {
            <p class="lead">Derslik girerseniz aynı saatte aynı sınıfa iki ders konmaz ve kapasite aşılmaz. Derslik takibi istemiyorsanız bu adımı atlayın.</p>
            @if (!state.branches().length) {
              <form class="inline" (ngSubmit)="addBranch()">
                <label class="field">Şube adı<input name="branch" [(ngModel)]="branchName" maxlength="200" placeholder="Merkez"></label>
                <button type="submit" class="btn" [disabled]="busy() || !branchName.trim()">Şubeyi ekle</button>
              </form>
              <div class="chips" aria-label="Hazır şube adları">
                @for (idea of ['Merkez', 'Ana bina', 'Şube 2']; track idea) { <button type="button" (click)="branchName = idea">{{ idea }}</button> }
              </div>
            }
            @for (branch of state.branches(); track branch.id) {
              <div class="block">
                <h3>{{ branch.name }}</h3>
                <div class="chips">@for (room of roomsOf(branch.id); track room.id) { <span class="badge">{{ room.name }}{{ room.capacity ? ', ' + room.capacity + ' kişi' : '' }}</span> }
                  @empty { <span class="muted">Henüz derslik yok.</span> }</div>
                <form class="inline" (ngSubmit)="addRoom(branch.id)">
                  <label class="field">Derslik adı<input [name]="'room' + branch.id" [(ngModel)]="roomName" maxlength="200" placeholder="Derslik 1"></label>
                  <button type="submit" class="btn-secondary" [disabled]="busy() || !roomName.trim()">Dersliği ekle</button>
                </form>
                <p class="label">Kaç kişilik?</p>
                <div class="chips">
                  <button type="button" [attr.aria-pressed]="roomCapacity === null" (click)="roomCapacity = null">Sınır yok</button>
                  @for (size of capacities; track size) { <button type="button" [attr.aria-pressed]="roomCapacity === size" (click)="roomCapacity = size">{{ size }}</button> }
                </div>
              </div>
            }
            @if (state.branches().length) {
              <form class="inline" (ngSubmit)="addBranch()">
                <label class="field">Başka bir şube<input name="branch2" [(ngModel)]="branchName" maxlength="200"></label>
                <button type="submit" class="btn-quiet" [disabled]="busy() || !branchName.trim()">Şube ekle</button>
              </form>
            }
          }

          @case ('courses') {
            <p class="lead">Önce kursu seçin ya da yazın, sonra altına grupları açın. Her grubun bir öğretmeni olur; davet bekleyen öğretmeni de seçebilirsiniz.</p>
            <div class="block">
              <h3>Kurs ekle</h3>
              <div class="chips" aria-label="Önerilen kurslar">
                @for (idea of courseIdeas(); track idea.name) {
                  <button type="button" [disabled]="hasCourse(idea.name)" (click)="courseName = idea.name; courseSubject = idea.subject">{{ idea.name }}</button>
                }
              </div>
              <form class="inline" (ngSubmit)="addCourse()">
                <label class="field">Kurs adı<input name="cname" [(ngModel)]="courseName" maxlength="200"></label>
                <label class="field">Ders<input name="csub" [(ngModel)]="courseSubject" maxlength="100"></label>
                <button type="submit" class="btn" [disabled]="busy() || !courseName.trim() || !courseSubject.trim()">Kursu ekle</button>
              </form>
            </div>
            @for (course of state.courses(); track course.id) {
              <div class="block">
                <h3>{{ course.name }} <span class="muted" style="font-weight: 400">{{ course.subject }}</span></h3>
                <div class="stack" style="gap: 6px">
                  @for (group of groupsOf(course.id); track group.id) {
                    <div class="item"><strong>{{ group.name }}</strong><span class="muted">{{ state.teacherName(group.teacherUserId) }}</span></div>
                  } @empty { <span class="muted">Bu kursta henüz grup yok.</span> }
                </div>
                <p class="label">Yeni grup: adı</p>
                <div class="chips">
                  @for (idea of groupIdeas; track idea) {
                    <button type="button" [attr.aria-pressed]="groupFor === course.id && groupName === idea" (click)="groupFor = course.id; groupName = idea">{{ idea }}</button>
                  }
                </div>
                <label class="field">Ya da yazın<input [name]="'g' + course.id" [ngModel]="groupFor === course.id ? groupName : ''"
                  (ngModelChange)="groupFor = course.id; groupName = $event" maxlength="160"></label>
                <p class="label">Grubun öğretmeni</p>
                <div class="chips">
                  @for (teacher of state.teachers(); track teacher.userId) {
                    <button type="button" [attr.aria-pressed]="groupFor === course.id && groupTeacher === teacher.userId"
                      (click)="groupFor = course.id; groupTeacher = teacher.userId">{{ state.teacherName(teacher.userId) }}</button>
                  }
                </div>
                <div><button type="button" class="btn-secondary" [disabled]="busy() || groupFor !== course.id || !groupName.trim() || !groupTeacher"
                  (click)="addGroup(course.id)">Grubu ekle</button></div>
              </div>
            }
          }

          @case ('students') {
            <p class="lead">Öğrencileri tek tek ya da Excel'den kaydedilmiş CSV dosyasıyla ekleyin. Sonra her öğrenciyi bir gruba koyun ya da birebir ders alıyorsa öğretmenini seçin.</p>
            <div class="block">
              <h3>Öğrenci ekle</h3>
              <form class="inline" (ngSubmit)="addStudent()">
                <label class="field">Adı soyadı<input name="sname" [(ngModel)]="studentName" maxlength="200" autocomplete="off"></label>
                <label class="field">Veli e-postası (isteğe bağlı)<input name="gemail" type="email" [(ngModel)]="guardianEmail" maxlength="320" autocomplete="off"></label>
                <label class="field">Veli telefonu (isteğe bağlı)<input name="gphone" type="tel" [(ngModel)]="guardianPhone" maxlength="40" autocomplete="off"></label>
                <button type="submit" class="btn" [disabled]="busy() || !studentName.trim()">Ekle</button>
              </form>
              <div class="file-pick">
                <input id="setup-csv" class="file-pick__input" type="file" accept=".csv,text/csv" (change)="pickCsv($event)">
                <label for="setup-csv" class="btn-secondary file-pick__button">CSV dosyası seç</label>
                <span class="file-pick__name" aria-live="polite">{{ csvMessage() || 'İlk satırda ad, e-posta, telefon başlıkları olmalı.' }}</span>
              </div>
              @if (csvReady() > 0) {
                <div><button type="button" class="btn-secondary" [disabled]="busy()" (click)="commitCsv()">{{ csvReady() }} öğrenciyi ekle</button></div>
              }
            </div>
            @if (students().length) {
              <p class="label">{{ unplacedCount() }} öğrenci yerleştirilmedi. Gruba ya da birebir öğretmenine tıklayın.</p>
              <div class="placement">
                @for (student of students(); track student.id) {
                  <strong>{{ student.displayName }}</strong>
                  <div class="chips">
                    @for (group of state.groups(); track group.id) {
                      <button type="button" [attr.aria-pressed]="inGroup(student, group.id)" [disabled]="busy()" (click)="toggleGroup(student, group.id)"
                        [attr.aria-label]="student.displayName + ', ' + groupLabel(group.id)">{{ groupLabel(group.id) }}</button>
                    }
                    @for (teacher of state.teachers(); track teacher.userId) {
                      <button type="button" [attr.aria-pressed]="student.oneToOneTeacherUserId === teacher.userId" [disabled]="busy()"
                        (click)="toggleSolo(student, teacher.userId)">Birebir: {{ shortName(teacher.userId) }}</button>
                    }
                  </div>
                }
              </div>
            }
          }

          @case ('fees') {
            <p class="lead">Grup başına bir ücret seçin; o gruptaki anlaşması olmayan öğrencilere uygulanır. Birebir öğrenciler için ayrı seçin. Sonradan Tahsilat'tan öğrenci öğrenci değiştirebilirsiniz.</p>
            @for (group of state.groups(); track group.id) {
              <div class="block">
                <h3>{{ groupLabel(group.id) }}</h3>
                <p class="muted" style="margin: 0">{{ waitingFee(group.id).length }} öğrencinin bu kurs için anlaşması yok.</p>
                <ng-container *ngTemplateOutlet="feeForm; context: { $implicit: feeOf('g:' + group.id), key: 'g:' + group.id, solo: false }"></ng-container>
                <div><button type="button" class="btn-secondary" [disabled]="busy() || !waitingFee(group.id).length || !(feeOf('g:' + group.id).amount > 0)"
                  (click)="applyGroupFee(group.id)">{{ waitingFee(group.id).length }} öğrenciye uygula</button></div>
              </div>
            }
            @for (student of soloStudents(); track student.id) {
              <div class="block">
                <h3>{{ student.displayName }} <span class="muted" style="font-weight: 400">birebir</span></h3>
                @if (hasFee(student.id)) { <p class="muted" style="margin: 0">Anlaşması var.</p> } @else {
                  <p class="label">Kurs</p>
                  <div class="chips">@for (course of state.courses(); track course.id) {
                    <button type="button" [attr.aria-pressed]="feeOf('s:' + student.id).courseId === course.id" (click)="feeOf('s:' + student.id).courseId = course.id">{{ course.name }}</button>
                  }</div>
                  <ng-container *ngTemplateOutlet="feeForm; context: { $implicit: feeOf('s:' + student.id), key: 's:' + student.id, solo: true }"></ng-container>
                  <div><button type="button" class="btn-secondary" [disabled]="busy() || !feeOf('s:' + student.id).courseId || !(feeOf('s:' + student.id).amount > 0)"
                    (click)="applySoloFee(student)">Kaydet</button></div>
                }
              </div>
            }
            @if (!state.groups().length && !soloStudents().length) { <p class="empty">Önce kurs ve grup açın, öğrencileri yerleştirin.</p> }
          }

          @case ('schedule') {
            <p class="lead">Her grubun haftalık gününü ve saatini seçin; dönem sonuna kadar tekrarlayan dersler açılır. Çakışan saatler kaydetmeden önce listelenir.</p>
            @for (group of state.groups(); track group.id) {
              <div class="block">
                <h3>{{ groupLabel(group.id) }} <span class="muted" style="font-weight: 400">{{ state.teacherName(group.teacherUserId) }}</span></h3>
                @if (plannedGroups().has(group.id)) { <p class="muted" style="margin: 0">Bu grubun planlı dersleri var.</p> }
                <ng-container *ngTemplateOutlet="scheduleForm; context: { $implicit: planOf('g:' + group.id) }"></ng-container>
                <div><button type="button" class="btn-secondary" [disabled]="busy() || !ready(planOf('g:' + group.id))"
                  (click)="planSeries('g:' + group.id, group.courseId, group.id, null, group.teacherUserId)">Dersleri aç</button></div>
              </div>
            }
            @for (student of soloStudents(); track student.id) {
              <div class="block">
                <h3>{{ student.displayName }} <span class="muted" style="font-weight: 400">birebir, {{ state.teacherName(student.oneToOneTeacherUserId) }}</span></h3>
                <p class="label">Kurs</p>
                <div class="chips">@for (course of state.courses(); track course.id) {
                  <button type="button" [attr.aria-pressed]="soloCourse[student.id] === course.id" (click)="soloCourse[student.id] = course.id">{{ course.name }}</button>
                }</div>
                <ng-container *ngTemplateOutlet="scheduleForm; context: { $implicit: planOf('s:' + student.id) }"></ng-container>
                <div><button type="button" class="btn-secondary" [disabled]="busy() || !soloCourse[student.id] || !ready(planOf('s:' + student.id))"
                  (click)="planSeries('s:' + student.id, soloCourse[student.id], null, student.id, student.oneToOneTeacherUserId!)">Dersleri aç</button></div>
              </div>
            }
            @if (conflicts().length) {
              <div class="banner" role="alert">
                <p style="margin: 0 0 6px"><strong>{{ conflicts().length }} ders başka bir dersle çakışıyor.</strong> Çakışanları atlayıp kalanları açabilir ya da saati değiştirebilirsiniz.</p>
                <ul class="conflicts">@for (item of conflicts().slice(0, 6); track $index) { <li>{{ item }}</li> }</ul>
                <div class="actions"><button type="button" class="btn" [disabled]="busy()" (click)="planSeries(pendingKey, pendingArgs[0], pendingArgs[1], pendingArgs[2], pendingArgs[3], true)">Çakışanları atla, kalanları aç</button>
                  <button type="button" class="btn-quiet" (click)="conflicts.set([])">Vazgeç</button></div>
              </div>
            }
          }

          @case ('emails') {
            <p class="lead">E-postalar kurumunuzun adıyla gider. Göndermeye başlamadan önce öğrencilerinize ve velilerinize bilgi verdiğinizi onaylamanız gerekir.</p>
            <div class="stack" style="gap: 8px">
              @for (item of emailKinds; track item.value) {
                <button type="button" class="choice" [attr.aria-pressed]="emailOn[item.value]" (click)="emailOn[item.value] = !emailOn[item.value]">
                  <strong>{{ emailOn[item.value] ? '✓ ' : '' }}{{ item.label }}</strong><span>{{ item.hint }}</span></button>
              }
            </div>
            <p class="label">Kime gitsin?</p>
            <div class="chips">
              @for (audience of [{ v: 1, l: 'Veliye' }, { v: 2, l: 'Öğrenciye' }, { v: 3, l: 'Öğrenciye ve veliye' }]; track audience.v) {
                <button type="button" [attr.aria-pressed]="emailAudience === audience.v" (click)="emailAudience = audience.v">{{ audience.l }}</button>
              }
            </div>
            <label class="check"><input type="checkbox" [(ngModel)]="consent">
              <span><strong>Öğrencileri ve velileri bilgilendirdik.</strong> Kişisel verilerinin bu e-postalar için kullanılacağını onlara bildirdiğinizi onaylarsınız.</span></label>
          }

          @case ('summary') {
            <p class="lead">Kurulum özeti. Atladığınız adımları istediğiniz zaman buradan ya da menüdeki bölümlerinden tamamlayabilirsiniz.</p>
            <div class="stack" style="gap: 8px">
              @for (step of steps.slice(0, 8); track step.id) {
                <div class="item"><div class="item-main"><strong>{{ step.title }}</strong><span class="muted">{{ summaryLine(step.id) }}</span></div>
                  <span class="badge">{{ statusText(step.id) }}</span>
                  @if (status(step.id) !== 'done') { <button type="button" class="btn-quiet" (click)="goTo(step.id)">Şimdi yap</button> }</div>
              }
            </div>
          }
        }

        <div class="foot">
          <div class="actions">
            @if (index() > 0) { <button type="button" class="btn-quiet" (click)="back()">Geri</button> }
          </div>
          <div class="actions">
            @if (current() !== 'summary') {
              @if (current() !== 'profile') { <button type="button" class="btn-quiet" [disabled]="busy()" (click)="skip()">Bu adımı atla</button> }
              <button type="button" class="btn" [disabled]="busy() || !canContinue()" (click)="next()">{{ nextLabel() }}</button>
            } @else {
              <button type="button" class="btn" [disabled]="busy()" (click)="finish()">Kurulumu bitir, panele geç</button>
            }
          </div>
        </div>
      </section>
    </div>

    <ng-template #feeForm let-fee let-solo="solo">
      <p class="label">Ücret türü</p>
      <div class="chips">@for (model of models; track model.value) {
        <button type="button" [attr.aria-pressed]="fee.model === model.value" (click)="fee.model = model.value">{{ model.short }}</button>
      }</div>
      <p class="muted" style="margin: 0; font-size: 13px">{{ modelHint(fee.model) }}</p>
      <p class="label">{{ fee.model === 1 ? 'Toplam tutar' : fee.model === 2 ? 'Aylık tutar' : fee.model === 3 ? 'Ders başı tutar' : 'Paket tutarı' }}</p>
      <div class="chips">@for (amount of amounts; track amount) {
        <button type="button" [attr.aria-pressed]="fee.amount === amount" (click)="fee.amount = amount">{{ money(amount) }}</button>
      }</div>
      <label class="field" style="max-width: 220px">Ya da yazın (TL)<input type="number" min="1" step="1" [(ngModel)]="fee.amount" [ngModelOptions]="{ standalone: true }"></label>
      @if (fee.model === 1 || fee.model === 2) {
        <p class="label">{{ fee.model === 1 ? 'Kaç taksit?' : 'Kaç ay?' }}</p>
        <div class="chips">@for (count of [3, 4, 6, 8, 9, 10, 12]; track count) {
          <button type="button" [attr.aria-pressed]="fee.count === count" (click)="fee.count = count">{{ count }}</button>
        }</div>
        <p class="label">İlk ödeme günü</p>
        <div class="chips">@for (due of dueChoices(); track due.value) {
          <button type="button" [attr.aria-pressed]="fee.firstDue === due.value" (click)="fee.firstDue = due.value">{{ due.label }}</button>
        }</div>
      }
      @if (fee.model === 3) {
        <div class="chips"><button type="button" [attr.aria-pressed]="fee.unit === 1" (click)="fee.unit = 1">Ders başı</button>
          <button type="button" [attr.aria-pressed]="fee.unit === 2" (click)="fee.unit = 2">Saat başı</button></div>
      }
      @if (fee.model === 4) {
        <p class="label">Kaç ders?</p>
        <div class="chips">@for (count of [4, 8, 12, 16, 24]; track count) {
          <button type="button" [attr.aria-pressed]="fee.sessions === count" (click)="fee.sessions = count">{{ count }} ders</button>
        }</div>
      }
    </ng-template>

    <ng-template #scheduleForm let-plan>
      <p class="label">Günler</p>
      <div class="chips">@for (day of days; track day; let i = $index) {
        <button type="button" [attr.aria-pressed]="plan.days[i]" (click)="plan.days[i] = !plan.days[i]">{{ day }}</button>
      }</div>
      <p class="label">Başlangıç saati</p>
      <div class="chips">@for (time of times; track time) {
        <button type="button" [attr.aria-pressed]="plan.time === time" (click)="plan.time = time">{{ time }}</button>
      }</div>
      <p class="label">Süre</p>
      <div class="chips">@for (duration of durations; track duration.value) {
        <button type="button" [attr.aria-pressed]="plan.duration === duration.value" (click)="plan.duration = duration.value">{{ duration.label }}</button>
      }</div>
      @if (state.rooms().length) {
        <p class="label">Derslik</p>
        <div class="chips"><button type="button" [attr.aria-pressed]="!plan.roomId" (click)="plan.roomId = ''">Seçilmedi</button>
          @for (room of state.rooms(); track room.id) { <button type="button" [attr.aria-pressed]="plan.roomId === room.id" (click)="plan.roomId = room.id">{{ room.name }}</button> }</div>
      }
      <p class="label">Ne zamana kadar?</p>
      <div class="chips">@for (end of endChoices(); track end.value) {
        <button type="button" [attr.aria-pressed]="plan.endsOn === end.value" (click)="plan.endsOn = end.value">{{ end.label }}</button>
      }</div>
    </ng-template>
  `
})
export class SetupWizardComponent implements OnInit {
  @Output() readonly navigate = new EventEmitter<PanelSection>();
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);

  readonly steps = STEPS;
  readonly kinds = KINDS;
  readonly models = FEE_MODELS;
  readonly amounts = AMOUNTS;
  readonly capacities = CAPACITIES;
  readonly groupIdeas = GROUP_IDEAS;
  readonly days = DAYS;
  readonly times = TIMES;
  readonly durations = DURATIONS;
  readonly emailKinds = EMAIL_KINDS;
  readonly money = (value: number) => formatMoney(value).replace(',00 TL', ' TL');

  readonly current = signal<StepId>('profile');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly students = signal<PanelStudentRow[]>([]);
  readonly agreements = signal<{ studentId: string; courseId: string; isActive: boolean }[]>([]);
  readonly plannedGroups = signal<Set<string>>(new Set());
  readonly conflicts = signal<string[]>([]);
  readonly csvMessage = signal('');
  readonly csvReady = signal(0);
  readonly index = computed(() => STEPS.findIndex(step => step.id === this.current()));
  readonly stepInfo = computed(() => STEPS[this.index()]);
  readonly progress = computed(() => this.state.setupProgress());
  readonly unplacedCount = computed(() => this.students().filter(student => !student.groups.length && !student.oneToOneTeacherUserId).length);
  readonly soloStudents = computed(() => this.students().filter(student => !!student.oneToOneTeacherUserId));

  profileName = ''; profileKind: number | null = null;
  teachMode: 'self' | 'team' | null = null; teacherName = ''; teacherEmail = '';
  branchName = ''; roomName = ''; roomCapacity: number | null = 12;
  courseName = ''; courseSubject = ''; groupFor = ''; groupName = ''; groupTeacher = '';
  studentName = ''; guardianEmail = ''; guardianPhone = '';
  private csvText = ''; private csvPreviewId = '';
  readonly fees = new Map<string, FeePlan>();
  readonly plans = new Map<string, SchedulePlan>();
  soloCourse: Record<string, string> = {};
  emailOn: Record<number, boolean> = { 1: true, 2: false, 3: true, 4: true, 5: true, 6: false, 7: true };
  emailAudience = 1; consent = false;
  private emailSettings: PanelEmailSettings | null = null;
  pendingKey = ''; pendingArgs: [string, string | null, string | null, string] = ['', null, null, ''];

  async ngOnInit(): Promise<void> {
    await this.reload();
    const setup = this.state.setup();
    this.profileName = setup?.name ?? this.state.workspace()?.name ?? '';
    this.profileKind = KINDS.find(kind => kind.key === setup?.institutionCategory)?.value ?? null;
    this.teachMode = (setup?.counts.teachers ?? 0) + (setup?.counts.invited ?? 0) > 0 ? 'team' : null;
    // Continue where the owner left off: the first step neither done nor skipped.
    const open = setup?.steps.find(step => step.status === 'open');
    this.current.set(open?.id ?? 'summary');
  }

  private async reload(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    try {
      await this.state.refreshLists();
      const now = Date.now();
      const [students, agreements, lessons, emails] = await Promise.all([
        this.allStudents(id),
        this.api.feeAgreements(id).catch(() => []),
        this.api.lessons(id, new Date(now), new Date(now + 120 * 86_400_000)).catch(() => []),
        this.api.emailSettings(id).catch(() => null)
      ]);
      this.students.set(students);
      this.agreements.set(agreements);
      this.plannedGroups.set(new Set(lessons.filter(lesson => lesson.status === 'Scheduled' && lesson.groupId).map(lesson => lesson.groupId!)));
      if (emails && !this.emailSettings) {
        this.emailSettings = emails;
        if (emails.informedConsentAt) {
          this.consent = true;
          for (const rule of emails.rules) this.emailOn[rule.kindValue] = rule.enabled;
          this.emailAudience = emails.rules.find(rule => rule.enabled)?.audience ?? 1;
        }
      }
      await this.state.loadSetup();
    } catch (error) { this.error.set(problemText(error, 'Kurulum bilgileri yüklenemedi.')); }
  }

  /** Every active student, page by page (the search returns at most 100 at a time). */
  private async allStudents(id: string): Promise<PanelStudentRow[]> {
    const all: PanelStudentRow[] = [];
    for (let skip = 0; skip < 2000; skip += 100) {
      const page = await this.api.searchStudents(id, { status: 'active', take: 100, skip, sort: 'name' });
      all.push(...page.items);
      if (page.items.length < 100) break;
    }
    return all;
  }

  status(step: StepId): 'done' | 'skipped' | 'open' {
    if (step === 'summary') return 'open';
    return this.state.setup()?.steps.find(item => item.id === step)?.status ?? 'open';
  }
  statusText(step: StepId): string { return { done: 'Tamam', skipped: 'Atlandı', open: 'Yapılmadı' }[this.status(step)]; }
  goTo(step: StepId): void { this.current.set(step); this.error.set(''); this.notice.set(''); this.conflicts.set([]); this.focusTitle(); }
  back(): void { this.goTo(STEPS[Math.max(0, this.index() - 1)].id); }

  canContinue(): boolean {
    if (this.current() === 'profile') return !!this.profileName.trim() && this.profileKind !== null;
    if (this.current() === 'emails') return !this.anyEmail() || this.consent;
    return true;
  }
  nextLabel(): string { return this.current() === 'profile' || this.current() === 'emails' ? 'Kaydet ve devam et' : 'Devam et'; }
  private anyEmail(): boolean { return Object.values(this.emailOn).some(Boolean); }

  async next(): Promise<void> {
    const step = this.current();
    const ok = await this.act(async () => {
      if (step === 'profile') {
        await this.api.saveProfile(this.state.id(), this.profileName.trim(), this.profileKind);
        await this.state.refreshWorkspaceName(this.profileName.trim());
      }
      if (step === 'emails') await this.saveEmails();
      await this.setSkipped(step as SetupStepId, false);
    });
    if (ok) this.goTo(STEPS[this.index() + 1].id);
  }

  async skip(): Promise<void> {
    const step = this.current() as SetupStepId;
    if (await this.act(() => this.setSkipped(step, this.status(step) !== 'done'))) this.goTo(STEPS[this.index() + 1].id);
  }

  async later(): Promise<void> { this.navigate.emit('overview'); }

  async finish(): Promise<void> {
    if (await this.act(() => this.api.saveSetup(this.state.id(), this.skippedList(), true))) {
      await this.state.loadSetup();
      this.navigate.emit('overview');
    }
  }

  private skippedList(): SetupStepId[] {
    return (this.state.setup()?.steps ?? []).filter(item => item.status === 'skipped').map(item => item.id);
  }
  private async setSkipped(step: SetupStepId, skipped: boolean): Promise<void> {
    const list = this.skippedList().filter(item => item !== step);
    if (skipped) list.push(step);
    await this.api.saveSetup(this.state.id(), list, false);
    await this.state.loadSetup();
  }

  // ---- teachers, rooms, courses ----
  async invite(): Promise<void> {
    await this.act(async () => {
      await this.api.inviteTeacher(this.state.id(), this.teacherEmail.trim(), this.teacherName.trim());
      this.teacherName = ''; this.teacherEmail = '';
      await this.reload();
    }, 'Davet gönderildi. Öğretmen kabul etmeden de ona grup ve ders verebilirsiniz.');
  }
  roomsOf(branchId: string) { return this.state.rooms().filter(room => room.branchId === branchId); }
  async addBranch(): Promise<void> {
    await this.act(async () => { await this.service.createBranch(this.state.id(), this.branchName.trim()); this.branchName = ''; await this.reload(); }, 'Şube eklendi.');
  }
  async addRoom(branchId: string): Promise<void> {
    await this.act(async () => {
      await this.service.createRoom(this.state.id(), branchId, this.roomName.trim(), this.roomCapacity);
      this.roomName = ''; await this.reload();
    }, 'Derslik eklendi.');
  }
  courseIdeas() { return COURSE_IDEAS[this.profileKind ?? 2] ?? COURSE_IDEAS[2]; }
  hasCourse(name: string): boolean { return this.state.courses().some(course => course.name.toLocaleLowerCase('tr') === name.toLocaleLowerCase('tr')); }
  groupsOf(courseId: string) { return this.state.groups().filter(group => group.courseId === courseId); }
  async addCourse(): Promise<void> {
    await this.act(async () => {
      const created = await this.service.createCourse(this.state.id(), this.courseName.trim(), this.courseSubject.trim());
      this.courseName = ''; this.courseSubject = '';
      this.groupFor = created.id; this.groupTeacher = this.state.teachers().length === 1 ? this.state.teachers()[0].userId : '';
      await this.reload();
    }, 'Kurs eklendi. Şimdi altına grup açın.');
  }
  async addGroup(courseId: string): Promise<void> {
    await this.act(async () => {
      await this.service.createGroup(this.state.id(), courseId, this.groupTeacher, this.groupName.trim());
      this.groupName = '';
      await this.reload();
    }, 'Grup eklendi.');
  }
  groupLabel(groupId: string): string {
    const group = this.state.groups().find(item => item.id === groupId);
    const course = this.state.courses().find(item => item.id === group?.courseId);
    return group ? `${course?.name ?? ''} ${group.name}`.trim() : '';
  }
  shortName(userId: string): string { return this.state.teacherName(userId).replace(/ \((davet bekliyor|kurum sahibi|davet geçersiz)\)$/, ''); }

  // ---- students ----
  inGroup(student: PanelStudentRow, groupId: string): boolean { return student.groups.some(group => group.id === groupId); }
  async addStudent(): Promise<void> {
    await this.act(async () => {
      const created = await this.service.addStudent(this.state.id(), this.studentName.trim(), '', '');
      if (this.guardianEmail.trim() || this.guardianPhone.trim())
        await this.service.linkGuardian(this.state.id(), created.id, crypto.randomUUID(), 1,
          { displayName: `${this.studentName.trim()} velisi`, email: this.guardianEmail, phoneNumber: this.guardianPhone });
      this.studentName = ''; this.guardianEmail = ''; this.guardianPhone = '';
      await this.reload();
    }, 'Öğrenci eklendi. Aşağıdan gruba yerleştirin.');
  }
  async pickCsv(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.csvReady.set(0); this.csvMessage.set(file?.name ?? '');
    if (!file) return;
    if (file.size > 1_000_000) { this.csvMessage.set('Dosya en çok 1 MB olabilir.'); return; }
    this.csvText = readCsvText(await file.arrayBuffer());
    input.value = '';
    try {
      const preview = await this.service.previewStudentCsv(this.state.id(), this.csvText);
      this.csvPreviewId = preview.previewId;
      this.csvReady.set(preview.invalidCount ? 0 : preview.readyCount);
      this.csvMessage.set(preview.invalidCount
        ? `${preview.invalidCount} satır hatalı; dosyayı düzeltip yeniden seçin (Öğrenciler bölümünde ayrıntı görünür).`
        : `${file.name}: ${preview.readyCount} eklenecek, ${preview.duplicateCount + preview.importedCount} zaten var.`);
    } catch (error) { this.csvMessage.set(problemText(error, 'Dosya okunamadı. İlk satırda ad, e-posta, telefon başlıkları olmalı.')); }
  }
  async commitCsv(): Promise<void> {
    await this.act(async () => {
      const result = await this.service.commitStudentCsv(this.state.id(), this.csvPreviewId, this.csvText);
      this.csvReady.set(0); this.csvMessage.set(`${result.createdCount} öğrenci eklendi.`);
      await this.reload();
    });
  }
  async toggleGroup(student: PanelStudentRow, groupId: string): Promise<void> {
    await this.act(async () => {
      if (this.inGroup(student, groupId)) await this.api.removeFromGroup(this.state.id(), groupId, student.id);
      else await this.service.enrollStudent(this.state.id(), groupId, student.id);
      await this.reload();
    });
  }
  async toggleSolo(student: PanelStudentRow, teacherUserId: string): Promise<void> {
    await this.act(async () => {
      await this.api.setOneToOneTeacher(this.state.id(), student.id, student.oneToOneTeacherUserId === teacherUserId ? null : teacherUserId);
      await this.reload();
    });
  }

  // ---- fees ----
  private today(): string { return new Date(Date.now() + 3 * 3_600_000).toISOString().slice(0, 10); }
  dueChoices(): { value: string; label: string }[] {
    const today = this.today();
    const next = new Date(`${today}T12:00:00Z`); next.setUTCMonth(next.getUTCMonth() + 1, 1);
    const fifteenth = new Date(`${today}T12:00:00Z`); fifteenth.setUTCDate(15); if (fifteenth.toISOString().slice(0, 10) <= today) fifteenth.setUTCMonth(fifteenth.getUTCMonth() + 1);
    const label = (date: Date) => new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', timeZone: 'UTC' }).format(date);
    return [{ value: today, label: 'Bugün' }, { value: fifteenth.toISOString().slice(0, 10), label: label(fifteenth) }, { value: next.toISOString().slice(0, 10), label: label(next) }];
  }
  feeOf(key: string): FeePlan {
    let fee = this.fees.get(key);
    if (!fee) {
      fee = { model: key.startsWith('s:') ? 3 : 2, amount: 0, count: 9, firstDue: this.today(), unit: 1, sessions: 8, expiresOn: '', courseId: '' };
      this.fees.set(key, fee);
    }
    return fee;
  }
  modelHint(model: number): string { return FEE_MODELS.find(item => item.value === model)?.hint ?? ''; }
  hasFee(studentId: string, courseId?: string): boolean {
    return this.agreements().some(item => item.isActive && item.studentId === studentId && (!courseId || item.courseId === courseId));
  }
  waitingFee(groupId: string): PanelStudentRow[] {
    const group = this.state.groups().find(item => item.id === groupId);
    return group ? this.students().filter(student => this.inGroup(student, groupId) && !this.hasFee(student.id, group.courseId)) : [];
  }
  async applyGroupFee(groupId: string): Promise<void> {
    const group = this.state.groups().find(item => item.id === groupId);
    if (!group) return;
    const fee = this.feeOf('g:' + groupId);
    const waiting = this.waitingFee(groupId);
    let done = 0;
    await this.act(async () => {
      for (const student of waiting) {
        await createFeeAgreementWithInstallments(this.service, this.state.id(), { ...fee, studentId: student.id, courseId: group.courseId }, this.today());
        done++;
      }
      await this.reload();
    });
    if (done) this.notice.set(`${done} öğrenciye ücret anlaşması açıldı.`);
  }
  async applySoloFee(student: PanelStudentRow): Promise<void> {
    const fee = this.feeOf('s:' + student.id);
    await this.act(async () => {
      await createFeeAgreementWithInstallments(this.service, this.state.id(), { ...fee, studentId: student.id }, this.today());
      await this.reload();
    }, `${student.displayName} için ücret anlaşması açıldı.`);
  }

  // ---- schedule ----
  endChoices(): { value: string; label: string }[] {
    const today = new Date(`${this.today()}T12:00:00Z`);
    const year = today.getUTCFullYear(); const month = today.getUTCMonth();
    const endOfMonth = new Date(Date.UTC(year, month + 1, 0));
    const termEnd = month >= 8 || month === 0 ? new Date(Date.UTC(month === 0 ? year : year + 1, 0, 23)) : new Date(Date.UTC(year, 5, 26));
    const schoolYear = new Date(Date.UTC(month >= 6 ? year + 1 : year, 5, 26));
    const fmt = (date: Date) => date.toISOString().slice(0, 10);
    const label = (date: Date) => new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date);
    return [
      { value: fmt(endOfMonth), label: `Ay sonu (${label(endOfMonth)})` },
      { value: fmt(termEnd), label: `Dönem sonu (${label(termEnd)})` },
      { value: fmt(schoolYear), label: `Okul yılı sonu (${label(schoolYear)})` }
    ].filter((item, index, list) => list.findIndex(other => other.value === item.value) === index);
  }
  planOf(key: string): SchedulePlan {
    let plan = this.plans.get(key);
    if (!plan) { plan = { days: [false, false, false, false, false, false, false], time: '17:00', duration: 60, roomId: '', endsOn: this.endChoices()[1]?.value ?? '' }; this.plans.set(key, plan); }
    return plan;
  }
  ready(plan: SchedulePlan): boolean { return plan.days.some(Boolean) && !!plan.time && !!plan.endsOn; }
  async planSeries(key: string, courseId: string, groupId: string | null, studentId: string | null, teacherUserId: string, skipConflicts = false): Promise<void> {
    const plan = this.planOf(key);
    const tomorrow = new Date(Date.now() + 27 * 3_600_000).toISOString().slice(0, 10);
    const request = { courseId, groupId, studentId, teacherUserId, startsOn: tomorrow, endsOn: plan.endsOn, startTime: plan.time,
      durationMinutes: plan.duration, daysOfWeekMask: plan.days.reduce((mask, on, index) => on ? mask | (1 << index) : mask, 0),
      roomId: plan.roomId || null, repeatEveryWeeks: 1, monthlyWeekOfMonth: null };
    await this.act(async () => {
      if (!skipConflicts) {
        const preview = await this.api.previewSeries(this.state.id(), request);
        const clashes = preview.occurrences.filter(item => item.conflicts.length);
        if (clashes.length) {
          this.pendingKey = key; this.pendingArgs = [courseId, groupId, studentId, teacherUserId];
          this.conflicts.set(clashes.map(item => `${new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', weekday: 'short', hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul' }).format(new Date(item.startsAt))}: ${item.conflicts.join(', ')}`));
          return;
        }
      }
      const result = await this.api.createSeries(this.state.id(), request, skipConflicts);
      this.conflicts.set([]);
      this.notice.set(`${result.sessionsCreated} ders açıldı${result.sessionsSkipped ? `, ${result.sessionsSkipped} çakışan atlandı` : ''}.`);
      this.state.changed();
      await this.reload();
    });
  }

  // ---- e-mails ----
  private async saveEmails(): Promise<void> {
    const current = this.emailSettings;
    await this.api.saveEmailSettings(this.state.id(), {
      rules: EMAIL_KINDS.map(item => ({ kind: item.value, enabled: !!this.emailOn[item.value], audience: this.emailAudience,
        note: current?.rules.find(rule => rule.kindValue === item.value)?.note ?? null })),
      installmentDaysBefore: current?.installmentDaysBefore ?? 3, installmentOnDueDay: current?.installmentOnDueDay ?? true,
      overdueDaysAfter: current?.overdueDaysAfter ?? 3, sendHour: current?.sendHour ?? 9, replyToEmail: current?.replyToEmail ?? null,
      informedConsentConfirmed: this.consent
    });
  }

  summaryLine(step: StepId): string {
    const counts = this.state.setup()?.counts;
    if (!counts) return '';
    switch (step) {
      case 'profile': return `${this.state.workspace()?.name ?? ''}${this.profileKind ? ', ' + KINDS.find(kind => kind.value === this.profileKind)?.label : ''}`;
      case 'teachers': return `${counts.teachers} öğretmen katıldı, ${counts.invited} davet bekliyor`;
      case 'rooms': return `${counts.rooms} derslik`;
      case 'courses': return `${counts.courses} kurs, ${counts.groups} grup`;
      case 'students': return `${counts.students} öğrenci, ${counts.unplaced} yerleştirilmedi`;
      case 'fees': return `${counts.withoutFee} öğrencinin ücret anlaşması yok`;
      case 'schedule': return `${counts.futureLessons} planlı ders`;
      case 'emails': return this.status('emails') === 'done' ? 'Velileri bilgilendirme onayı verildi' : 'E-postalar kapalı';
      default: return '';
    }
  }

  private focusTitle(): void { setTimeout(() => document.getElementById('step-' + this.current())?.focus({ preventScroll: false })); }

  private async act(work: () => Promise<unknown>, done = ''): Promise<boolean> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { await work(); if (done) this.notice.set(done); return true; }
    catch (error) { this.error.set(problemText(error)); return false; }
    finally { this.busy.set(false); }
  }
}
