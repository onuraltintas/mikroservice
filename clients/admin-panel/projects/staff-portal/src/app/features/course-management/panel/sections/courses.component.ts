import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseGroup, CourseManagementService } from '../../course-management.service';
import { CoursePanelState, problemText } from '../panel-state';
import { CoursePanelApi } from '../panel-api.service';
import { PanelConfirm } from '../panel-confirm';

/**
 * Kurslar ve gruplar: what is taught, and which teacher teaches which group. Students join groups from their card or
 * many at once from the student list. 23.11.2026: each group shows its students; a group can be archived or deleted.
 */
@Component({
  selector: 'course-courses-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head"><div><h1>Kurslar ve gruplar</h1><p>{{ state.courses().length }} kurs, {{ state.groups().length }} grup@if (archivedGroups().length) {, {{ archivedGroups().length }} arşivde}</p></div></header>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
    <div class="row">
      <section class="card grow" aria-labelledby="course-list-title">
        <div class="card-head"><h2 id="course-list-title">Kurslar</h2></div>
        @for (course of state.courses(); track course.id) {
          <div class="course">
            <div><strong>{{ course.name }}</strong> <span class="muted">{{ course.subject }}</span></div>
            <ul>
              @for (group of groupsOf(course.id); track group.id) {
                @if (editing() === group.id) {
                  <li><form class="edit" (ngSubmit)="saveGroup(group.id)">
                    <label class="field">Grup adı<input name="editName" [(ngModel)]="editName" maxlength="160"></label>
                    <label class="field">Öğretmen<select name="editTeacher" [(ngModel)]="editTeacher">
                      @for (teacher of state.teachers(); track teacher.userId) { <option [value]="teacher.userId">{{ state.teacherName(teacher.userId) }}</option> }</select></label>
                    @if (editTeacher !== group.teacherUserId) {
                      <label class="check"><input type="checkbox" name="editMove" [(ngModel)]="editMove">
                        <span>Grubun planlı dersleri ve tekrarlayan programı da yeni öğretmene geçsin</span></label>
                    }
                    <div class="actions"><button type="submit" class="btn btn-small" [disabled]="busy() || !editName.trim() || !editTeacher">Kaydet</button>
                      <button type="button" class="btn-quiet btn-small" (click)="editing.set('')">Vazgeç</button></div>
                  </form></li>
                } @else {
                  <li>{{ group.name }} <span class="muted">{{ state.teacherName(group.teacherUserId) }}, {{ studentsText(group) }}</span>
                    @if (!state.readOnly()) {
                      <button type="button" class="btn-quiet btn-small" (click)="edit(group)" [attr.aria-label]="group.name + ' grubunu düzenle'">Düzenle</button>
                      <button type="button" class="btn-quiet btn-small" (click)="archiveGroup(group)" [disabled]="busy()" [attr.aria-label]="group.name + ' grubunu arşive al'">Arşive al</button>
                      <button type="button" class="btn-quiet btn-small danger" (click)="deleteGroup(group)" [disabled]="busy()" [attr.aria-label]="group.name + ' grubunu sil'">Sil</button>
                    }</li>
                }
              } @empty { <li class="muted">Henüz grup yok.</li> }
            </ul>
          </div>
        } @empty { <p class="empty">Henüz kurs yok. Sağdaki formdan ilk kursunuzu ekleyin.</p> }
        @if (archivedGroups().length) {
          <div class="course archived">
            <button type="button" class="btn-quiet btn-small" (click)="showArchived.set(!showArchived())" [attr.aria-expanded]="showArchived()">
              {{ showArchived() ? 'Arşivdeki grupları gizle' : 'Arşivdeki grupları göster (' + archivedGroups().length + ')' }}</button>
            @if (showArchived()) {
              <ul>
                @for (group of archivedGroups(); track group.id) {
                  <li>{{ group.name }} <span class="muted">{{ group.courseName }}, {{ state.teacherName(group.teacherUserId) }}</span>
                    @if (!state.readOnly()) {
                      <button type="button" class="btn-quiet btn-small" (click)="restoreGroup(group)" [disabled]="busy()" [attr.aria-label]="group.name + ' grubunu arşivden çıkar'">Arşivden çıkar</button>
                      <button type="button" class="btn-quiet btn-small danger" (click)="deleteGroup(group)" [disabled]="busy()" [attr.aria-label]="group.name + ' grubunu sil'">Sil</button>
                    }</li>
                }
              </ul>
            }
          </div>
        }
      </section>
      @if (!state.readOnly()) {
        <div class="side stack">
          <form class="card card-pad stack" style="gap: 14px" (ngSubmit)="addCourse()">
            <h2>Kurs ekle</h2>
            <label class="field">Kurs adı<input name="courseName" [(ngModel)]="courseName" maxlength="200" placeholder="LGS Matematik"></label>
            <label class="field">Ders<input name="subject" [(ngModel)]="subject" maxlength="100" placeholder="Matematik"></label>
            <div><button type="submit" class="btn" [disabled]="busy() || !courseName.trim() || !subject.trim()">Kursu ekle</button></div>
          </form>
          <form class="card card-pad stack" style="gap: 14px" (ngSubmit)="addGroup()">
            <h2>Grup ekle</h2>
            <label class="field">Kurs<select name="groupCourse" [(ngModel)]="groupCourseId"><option value="">Kurs seçin</option>
              @for (course of state.courses(); track course.id) { <option [value]="course.id">{{ course.name }}</option> }</select></label>
            <label class="field">Grup adı<input name="groupName" [(ngModel)]="groupName" maxlength="200" placeholder="8-A"></label>
            <label class="field">Öğretmen<select name="groupTeacher" [(ngModel)]="groupTeacherId"><option value="">Öğretmen seçin</option>
              @for (teacher of state.teachers(); track teacher.userId) { <option [value]="teacher.userId">{{ state.teacherName(teacher.userId) }}</option> }</select>
              @if (onlyOwner()) { <span class="hint">Öğretmen eklemek için Öğretmenler bölümünden davet gönderin.</span> }</label>
            <div><button type="submit" class="btn" [disabled]="busy() || !groupCourseId || !groupName.trim() || !groupTeacherId">Grubu ekle</button></div>
          </form>
        </div>
      }
    </div>
  `,
  styles: `
    .course { padding: 14px 20px; border-top: 1px solid var(--ds-line); }
    .course ul { margin: 8px 0 0; padding-left: 18px; line-height: 1.8; }
    .course .muted { font-size: 14px; margin-left: 6px; }
    .course li .btn-quiet { margin-left: 6px; }
    .course.archived > .btn-quiet { padding-left: 0; }
    .btn-quiet.danger { color: var(--ds-danger); }
    .edit { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 10px; padding: 10px 0; }
    .edit .field { flex: 1 1 180px; } .edit .check { flex-basis: 100%; }
  `
})
export class CoursesSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly service = inject(CourseManagementService);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  private readonly api = inject(CoursePanelApi);
  private readonly confirm = inject(PanelConfirm);
  readonly editing = signal('');
  editName = ''; editTeacher = ''; editMove = true;
  readonly onlyOwner = computed(() => this.state.teachers().every(member => member.role === 'Owner'));
  courseName = ''; subject = ''; groupCourseId = ''; groupName = ''; groupTeacherId = '';

  readonly showArchived = signal(false);
  readonly archivedGroups = computed(() => this.state.allGroups().filter(group => !!group.archivedAt));

  groupsOf(courseId: string) { return this.state.groups().filter(group => group.courseId === courseId); }
  studentsText(group: CourseGroup): string { return group.studentCount ? `${group.studentCount} öğrenci` : 'öğrenci yok'; }

  /** Archive: the group's planned lessons are cancelled and its students leave it; the owner confirms when there is any. */
  async archiveGroup(group: CourseGroup, force = false): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try {
      const result = await this.api.archiveGroup(this.state.id(), group.id, force);
      await this.state.refreshLists();
      this.state.changed();
      const done = [result?.cancelledLessons ? `${result.cancelledLessons} planlı ders iptal edildi` : '',
        result?.removedStudents ? `${result.removedStudents} öğrenci gruptan çıktı` : ''].filter(Boolean).join(', ');
      this.notice.set(done ? `${group.name} arşive alındı; ${done}.` : `${group.name} arşive alındı.`);
    } catch (error) {
      this.busy.set(false);
      const body = (error as { error?: { code?: string; lessons?: number; students?: number } })?.error;
      if (!force && body?.code === 'Group.HasPlannedLessonsOrStudents') {
        if (await this.confirm.ask(groupEndQuestion(group.name, body.lessons ?? 0, body.students ?? 0, 'archive'),
          { title: 'Grubu arşive al', confirmText: 'Arşive al' })) await this.archiveGroup(group, true);
        return;
      }
      this.error.set(problemText(error));
    } finally { this.busy.set(false); }
  }

  restoreGroup(group: CourseGroup): Promise<void> {
    return this.act(async () => { await this.api.restoreGroup(this.state.id(), group.id); this.state.changed(); },
      `${group.name} arşivden çıkarıldı. Öğrencileri yeniden ekleyebilir, ders planlayabilirsiniz.`);
  }

  /** Delete: only a group with no history; one with history is archived instead (asked first). */
  async deleteGroup(group: CourseGroup, force = false): Promise<void> {
    if (!force && !await this.confirm.ask(`${group.name} kalıcı olarak silinsin mi? Bu geri alınamaz.`, { title: 'Grubu sil', confirmText: 'Sil', danger: true })) return;
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try {
      await this.api.deleteGroup(this.state.id(), group.id, force);
      await this.state.refreshLists();
      this.state.changed();
      this.notice.set(`${group.name} silindi.`);
    } catch (error) {
      this.busy.set(false);
      const body = (error as { error?: { code?: string; lessons?: number; students?: number } })?.error;
      if (body?.code === 'Group.InUse') {
        if (group.archivedAt) { this.error.set(`${group.name} geçmiş dersleri, ödevleri ya da yoklaması olduğu için silinemez; arşivde kalır.`); return; }
        if (await this.confirm.ask(`${group.name} grubunun geçmiş dersleri, ödevleri ya da yoklaması var; kayıtlar bozulmasın diye silinemez. Bunun yerine arşive alayım mı?`,
          { title: 'Silinemez', confirmText: 'Arşive al' })) await this.archiveGroup(group);
        return;
      }
      if (!force && body?.code === 'Group.HasPlannedLessonsOrStudents') {
        if (await this.confirm.ask(groupEndQuestion(group.name, body.lessons ?? 0, body.students ?? 0, 'delete'),
          { title: 'Grubu sil', confirmText: 'Yine de sil', danger: true })) await this.deleteGroup(group, true);
        return;
      }
      this.error.set(problemText(error));
    } finally { this.busy.set(false); }
  }

  private async act(work: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true); this.error.set('');
    try { await work(); await this.state.refreshLists(); this.notice.set(done); }
    catch (error) { this.error.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  addCourse(): Promise<void> {
    return this.act(async () => {
      await this.service.createCourse(this.state.id(), this.courseName.trim(), this.subject.trim());
      this.courseName = ''; this.subject = '';
    }, 'Kurs eklendi.');
  }

  edit(group: { id: string; name: string; teacherUserId: string }): void {
    this.editing.set(group.id); this.editName = group.name; this.editTeacher = group.teacherUserId; this.editMove = true;
  }

  /** Rename a group or give it to another teacher; a clash with the new teacher's lessons is asked about first. */
  async saveGroup(groupId: string, force = false): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try {
      const result = await this.api.updateGroup(this.state.id(), groupId, this.editName.trim(), this.editTeacher, this.editMove, force);
      await this.state.refreshLists();
      this.state.changed();
      this.editing.set('');
      this.notice.set(result.movedLessons ? `Grup kaydedildi; ${result.movedLessons} planlı ders yeni öğretmene geçti.` : 'Grup kaydedildi.');
    } catch (error) {
      const body = (error as { error?: { clashes?: number; overlapping?: number; duringTimeOff?: number; outsideHours?: number } })?.error;
      const clashes = body?.clashes;
      if (!force && typeof clashes === 'number' && clashes > 0) {
        this.busy.set(false);
        if (await this.confirm.ask(teacherChangeQuestion(clashes, body?.overlapping ?? 0, body?.duringTimeOff ?? 0, body?.outsideHours ?? 0),
          { title: 'Saat çakışması', confirmText: 'Yine de değiştir' })) await this.saveGroup(groupId, true);
        return;
      }
      this.error.set(problemText(error));
    } finally { this.busy.set(false); }
  }

  addGroup(): Promise<void> {
    return this.act(async () => {
      await this.service.createGroup(this.state.id(), this.groupCourseId, this.groupTeacherId, this.groupName.trim());
      this.groupName = '';
    }, 'Grup eklendi. Öğrencileri Öğrenciler bölümünde seçip bu gruba toplu ekleyebilir ya da kartlarından tek tek ekleyebilirsiniz.');
  }
}

/** What ending a group does to its planned lessons and students, asked before archiving or deleting (23.11.2026). */
export function groupEndQuestion(name: string, lessons: number, students: number, action: 'archive' | 'delete'): string {
  const parts = [lessons ? `${lessons} planlı dersi ${action === 'archive' ? 'iptal edilecek' : 'silinecek'}` : '',
    students ? `${students} öğrencisi gruptan çıkacak` : ''].filter(Boolean).join(', ');
  const after = action === 'archive'
    ? 'Geçmiş dersler, yoklama ve ödevler kalır; öğrencileri başka bir gruba ekleyebilirsiniz.'
    : 'Öğrencilerin kendisi silinmez; başka bir gruba ekleyebilirsiniz.';
  return `${name} grubunun ${parts}. ${after} Devam edeyim mi?`;
}

/** Why moving a group's lessons to another teacher needs a confirmation (live test 21.11.2026). */
export function teacherChangeQuestion(lessons: number, overlapping: number, duringTimeOff: number, outsideHours: number): string {
  const reasons = [
    overlapping ? `${overlapping} ders öğretmenin başka bir dersiyle aynı saatte` : '',
    duringTimeOff ? `${duringTimeOff} ders öğretmenin izinli olduğu saatte` : '',
    outsideHours ? `${outsideHours} ders öğretmenin çalışma saatleri dışında` : ''
  ].filter(Boolean);
  return `Grubun ${lessons} planlı dersi yeni öğretmene uymuyor: ${reasons.join(', ') || 'saatler uygun değil'}. Yine de öğretmeni değiştireyim mi?`;
}
