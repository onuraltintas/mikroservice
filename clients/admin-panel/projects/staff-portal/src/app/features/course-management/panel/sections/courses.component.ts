import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseManagementService } from '../../course-management.service';
import { CoursePanelState, problemText } from '../panel-state';

/** Kurslar ve gruplar: what is taught, and which teacher teaches which group. Students join groups from their card. */
@Component({
  selector: 'course-courses-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head"><div><h1>Kurslar ve gruplar</h1><p>{{ state.courses().length }} kurs, {{ state.groups().length }} grup</p></div></header>
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
                <li>{{ group.name }} <span class="muted">{{ state.teacherName(group.teacherUserId) }}</span></li>
              } @empty { <li class="muted">Henüz grup yok.</li> }
            </ul>
          </div>
        } @empty { <p class="empty">Henüz kurs yok. Sağdaki formdan ilk kursunuzu ekleyin.</p> }
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
  `
})
export class CoursesSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly service = inject(CourseManagementService);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly onlyOwner = computed(() => this.state.teachers().every(member => member.role === 'Owner'));
  courseName = ''; subject = ''; groupCourseId = ''; groupName = ''; groupTeacherId = '';

  groupsOf(courseId: string) { return this.state.groups().filter(group => group.courseId === courseId); }

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

  addGroup(): Promise<void> {
    return this.act(async () => {
      await this.service.createGroup(this.state.id(), this.groupCourseId, this.groupTeacherId, this.groupName.trim());
      this.groupName = '';
    }, 'Grup eklendi. Öğrencileri kartlarından bu gruba ekleyebilirsiniz.');
  }
}
