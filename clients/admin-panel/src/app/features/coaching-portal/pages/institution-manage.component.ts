import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  CoachingAdminAssignmentDetail, CoachingAdminAssignmentListItem, CoachingAdminService,
  CoachingAdminSessionDetail, CoachingAdminSessionListItem,
  CoachingTeacherRosterItem
} from '../../../core/services/coaching-admin.service';
import { InstitutionCoachingService, InstitutionStudent } from '../../../core/services/institution-coaching.service';

@Component({
  selector: 'app-institution-manage',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './institution-manage.component.html'
})
export class InstitutionManageComponent implements OnInit {
  private readonly coaching = inject(CoachingAdminService);
  private readonly institution = inject(InstitutionCoachingService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly teachers = signal<CoachingTeacherRosterItem[]>([]);
  readonly students = signal<InstitutionStudent[]>([]);
  readonly assignments = signal<CoachingAdminAssignmentListItem[]>([]);
  readonly sessions = signal<CoachingAdminSessionListItem[]>([]);
  readonly institutionId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly selectedStudentIds = new Set<string>();
  readonly selectedSessionStudentIds = new Set<string>();
  readonly editingAssignmentId = signal<string | null>(null);
  readonly editingSessionId = signal<string | null>(null);
  readonly selectedAssignment = signal<CoachingAdminAssignmentDetail | null>(null);
  readonly selectedSession = signal<CoachingAdminSessionDetail | null>(null);
  readonly teacherPage = signal(1);
  readonly teacherTotal = signal(0);
  readonly studentPage = signal(1);
  readonly studentTotal = signal(0);

  teacherSearch = '';
  studentSearch = '';
  selectedTeacherId = '';
  assignmentTitle = '';
  assignmentSubject = '';
  assignmentDueDate = '';
  sessionStudentId = '';
  sessionType: 'OneOnOne' | 'Group' = 'OneOnOne';
  sessionSubject = '';
  sessionStartTime = '';
  sessionDurationMinutes = 45;
  editAssignmentTitle = '';
  editAssignmentDueDate = '';
  editSessionTitle = '';
  editSessionStartTime = '';
  editSessionDurationMinutes = 45;
  gradeStudentId = '';
  gradeScore: number | null = null;
  gradeFeedback = '';

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) void this.load();
  }

  async load(): Promise<boolean> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const scope = await firstValueFrom(this.coaching.getReadScope());
      if (scope.isGlobal || !scope.institutionId) throw new Error('Kurum kapsamı bulunamadı.');
      this.institutionId.set(scope.institutionId);
      const [teachers, students, assignments, sessions] = await Promise.all([
        firstValueFrom(this.coaching.getTeacherRoster(scope.institutionId, this.teacherPage(), this.teacherSearch)),
        firstValueFrom(this.institution.getStudents(this.studentPage(), this.studentSearch)),
        firstValueFrom(this.coaching.getAssignments()),
        firstValueFrom(this.coaching.getSessions())
      ]);
      this.teachers.set(teachers.teachers);
      this.teacherTotal.set(teachers.totalCount);
      this.students.set(students.items);
      this.studentTotal.set(students.totalCount);
      this.assignments.set(assignments.items);
      this.sessions.set(sessions.items);
      return true;
    } catch {
      this.error.set('Kurum yönetim verileri yüklenemedi. Lütfen yeniden deneyin.');
      return false;
    } finally {
      this.loading.set(false);
    }
  }

  search(): void {
    this.teacherPage.set(1);
    this.studentPage.set(1);
    void this.load();
  }

  changeTeacherPage(delta: number): void {
    this.teacherPage.update(page => Math.max(1, page + delta));
    void this.load();
  }

  changeStudentPage(delta: number): void {
    this.studentPage.update(page => Math.max(1, page + delta));
    void this.load();
  }

  studentsForTeacher(): InstitutionStudent[] {
    return this.students().filter(student => student.isActive !== false && student.teacherUserId === this.selectedTeacherId);
  }

  toggleStudent(id: string): void {
    if (this.selectedStudentIds.has(id)) this.selectedStudentIds.delete(id);
    else this.selectedStudentIds.add(id);
  }

  toggleSessionStudent(id: string): void {
    if (this.selectedSessionStudentIds.has(id)) this.selectedSessionStudentIds.delete(id);
    else this.selectedSessionStudentIds.add(id);
  }

  async assignTeacher(student: InstitutionStudent, teacherUserId: string): Promise<void> {
    if (!student.gradeLevel || student.gradeLevel < 1 || student.gradeLevel > 12) {
      this.error.set('Öğretmen ataması için öğrencinin 1–12 arasında sınıf düzeyi olmalıdır.');
      return;
    }
    await this.save('Öğretmen ataması güncellendi.', async () => {
      await firstValueFrom(this.institution.assignTeacher(student, teacherUserId || null));
    });
  }

  async createAssignment(): Promise<void> {
    const institutionId = this.institutionId();
    const studentIds = [...this.selectedStudentIds].filter(id =>
      this.studentsForTeacher().some(student => student.userId === id));
    const dueDate = new Date(this.assignmentDueDate);
    if (!institutionId || !this.selectedTeacherId || !this.assignmentTitle.trim() ||
        !studentIds.length || Number.isNaN(dueDate.getTime()) || dueDate <= new Date()) {
      this.error.set('Öğretmen, başlık, bağlı öğrenci ve ileri tarihli son teslim seçin.');
      return;
    }
    await this.save('Ödev oluşturuldu.', async () => {
      await firstValueFrom(this.institution.createAssignment({
        teacherId: this.selectedTeacherId, institutionId,
        title: this.assignmentTitle.trim(), subject: this.assignmentSubject.trim(),
        assignmentType: studentIds.length > 1 ? 'Group' : 'Individual',
        assignmentSource: 'Digital', dueDate: dueDate.toISOString(), studentIds
      }, crypto.randomUUID()));
      this.assignmentTitle = '';
      this.selectedStudentIds.clear();
    });
  }

  async createSession(): Promise<void> {
    const startTime = new Date(this.sessionStartTime);
    const eligibleStudents = this.studentsForTeacher().map(student => student.userId);
    const groupStudentIds = [...this.selectedSessionStudentIds].filter(id => eligibleStudents.includes(id));
    const validTargets = this.sessionType === 'Group'
      ? groupStudentIds.length >= 2
      : eligibleStudents.includes(this.sessionStudentId);
    if (!this.selectedTeacherId || !validTargets ||
        Number.isNaN(startTime.getTime()) || startTime <= new Date() ||
        this.sessionDurationMinutes < 1 || this.sessionDurationMinutes > 240) {
      this.error.set('Bağlı öğrenci(ler), ileri tarihli başlangıç ve 1–240 dakika süre seçin.');
      return;
    }
    await this.save('Seans oluşturuldu.', async () => {
      await firstValueFrom(this.institution.createSession({
        teacherId: this.selectedTeacherId,
        studentId: this.sessionType === 'Group' ? groupStudentIds[0] : this.sessionStudentId,
        startTime: startTime.toISOString(), durationMinutes: this.sessionDurationMinutes,
        subject: this.sessionSubject.trim(), type: this.sessionType,
        studentIds: this.sessionType === 'Group' ? groupStudentIds : undefined
      }, crypto.randomUUID()));
      this.sessionStudentId = '';
      this.sessionSubject = '';
      this.selectedSessionStudentIds.clear();
    });
  }

  async cancelAssignment(id: string): Promise<void> {
    if (!confirm('Bu ödev iptal edilsin mi?')) return;
    await this.save('Ödev iptal edildi.', async () => {
      await firstValueFrom(this.institution.cancelAssignment(id));
    });
  }

  async showAssignment(id: string): Promise<void> {
    this.error.set(null);
    try { this.selectedAssignment.set(await firstValueFrom(this.coaching.getAssignment(id))); }
    catch { this.error.set('Ödev ayrıntıları yüklenemedi.'); }
  }

  async gradeAssignment(): Promise<void> {
    const assignment = this.selectedAssignment();
    if (!assignment || !this.gradeStudentId || this.gradeScore === null ||
        !Number.isFinite(this.gradeScore) || this.gradeScore < 0 ||
        (assignment.maxScore != null && this.gradeScore > assignment.maxScore)) {
      this.error.set('Öğrenci ve geçerli puan seçin.');
      return;
    }
    await this.save('Ödev puanlandı.', async () => {
      await firstValueFrom(this.institution.gradeAssignment(
        assignment.id, this.gradeStudentId, this.gradeScore!, this.gradeFeedback.trim()));
      this.selectedAssignment.set(await firstValueFrom(this.coaching.getAssignment(assignment.id)));
      this.gradeScore = null;
      this.gradeFeedback = '';
    });
  }

  async showSession(id: string): Promise<void> {
    this.error.set(null);
    try { this.selectedSession.set(await firstValueFrom(this.coaching.getSession(id))); }
    catch { this.error.set('Seans ayrıntıları yüklenemedi.'); }
  }

  async recordAttendance(studentId: string, attended: boolean): Promise<void> {
    const session = this.selectedSession();
    if (!session) return;
    await this.save('Katılım kaydedildi.', async () => {
      await firstValueFrom(this.institution.updateAttendance(session.id, studentId, attended));
      this.selectedSession.set(await firstValueFrom(this.coaching.getSession(session.id)));
    });
  }

  studentLabel(id: string): string {
    const student = this.students().find(item => item.userId === id);
    return student ? `${student.firstName} ${student.lastName}` : id;
  }

  async beginEditAssignment(id: string): Promise<void> {
    this.error.set(null);
    try {
      const assignment = await firstValueFrom(this.coaching.getAssignment(id));
      this.editingAssignmentId.set(id);
      this.editAssignmentTitle = assignment.title;
      this.editAssignmentDueDate = this.localDateTime(assignment.dueDate);
    } catch { this.error.set('Ödev ayrıntıları yüklenemedi.'); }
  }

  async updateAssignment(): Promise<void> {
    const id = this.editingAssignmentId();
    const dueDate = new Date(this.editAssignmentDueDate);
    if (!id || !this.editAssignmentTitle.trim() || Number.isNaN(dueDate.getTime()) || dueDate <= new Date()) {
      this.error.set('Başlık ve ileri tarihli son teslim zorunludur.');
      return;
    }
    await this.save('Ödev güncellendi.', async () => {
      const current = await firstValueFrom(this.coaching.getAssignment(id));
      await firstValueFrom(this.institution.updateAssignment(id, {
        assignmentId: id, title: this.editAssignmentTitle.trim(),
        description: current.description, subject: current.subject,
        assignmentSource: current.source, targetGradeLevel: current.targetGradeLevel,
        bookTitle: current.bookTitle, bookIsbn: current.bookIsbn,
        bookEdition: current.bookEdition, bookChapter: current.bookChapter,
        bookStartPage: current.bookStartPage, bookEndPage: current.bookEndPage,
        bookStartQuestion: current.bookStartQuestion, bookEndQuestion: current.bookEndQuestion,
        dueDate: dueDate.toISOString(), estimatedDurationMinutes: current.estimatedDurationMinutes,
        maxScore: current.maxScore, passingScore: current.passingScore
      }));
      this.editingAssignmentId.set(null);
    });
  }

  async beginEditSession(id: string): Promise<void> {
    this.error.set(null);
    try {
      const session = await firstValueFrom(this.coaching.getSession(id));
      this.editingSessionId.set(id);
      this.editSessionTitle = session.title;
      this.editSessionStartTime = this.localDateTime(session.scheduledDate);
      this.editSessionDurationMinutes = session.durationMinutes;
    } catch { this.error.set('Seans ayrıntıları yüklenemedi.'); }
  }

  async updateSession(): Promise<void> {
    const id = this.editingSessionId();
    const startTime = new Date(this.editSessionStartTime);
    if (!id || !this.editSessionTitle.trim() || Number.isNaN(startTime.getTime()) ||
        startTime <= new Date() || this.editSessionDurationMinutes < 1 || this.editSessionDurationMinutes > 240) {
      this.error.set('Başlık, ileri tarihli başlangıç ve 1–240 dakika süre zorunludur.');
      return;
    }
    await this.save('Seans güncellendi.', async () => {
      const current = await firstValueFrom(this.coaching.getSession(id));
      await firstValueFrom(this.institution.updateSession(id, {
        sessionId: id, title: this.editSessionTitle.trim(), description: current.description,
        scheduledDate: startTime.toISOString(), durationMinutes: this.editSessionDurationMinutes,
        meetingLink: current.meetingLink, teacherNotes: current.teacherNotes
      }));
      this.editingSessionId.set(null);
    });
  }

  async cancelSession(id: string): Promise<void> {
    if (!confirm('Bu seans iptal edilsin mi?')) return;
    await this.save('Seans iptal edildi.', async () => {
      await firstValueFrom(this.institution.cancelSession(id));
    });
  }

  private async save(message: string, action: () => Promise<void>): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      await action();
      if (await this.load()) this.success.set(message);
    } catch {
      this.error.set('İşlem tamamlanamadı. Kurum kapsamını, atamayı ve alanları kontrol edin.');
    } finally {
      this.saving.set(false);
    }
  }

  private localDateTime(value: string): string {
    const date = new Date(value);
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  }
}
