import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { CourseStudentService, StudentInvitation, StudentWorkspace, StudentCourse, StudentLesson, StudentMakeupCredit, StudentAssignment, StudentAttendance, StudentFinanceSummary, StudentAssignmentAttachment, StudentNotification } from './course-student.service';

@Component({
  selector: 'course-student-root',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  readonly auth = inject(CourseStudentService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  email = ''; password = ''; product = 'course-management'; displayName = ''; mfaCode = ''; challenge = '';
  busy = false; error = ''; notice = '';
  invitations: StudentInvitation[] = []; workspaces: StudentWorkspace[] = []; courses: StudentCourse[] = []; lessons: StudentLesson[] = [];
  makeupCredits: StudentMakeupCredit[] = [];
  assignments: StudentAssignment[] = []; finance: StudentFinanceSummary[] = [];
  attendance: StudentAttendance[] = []; notifications: StudentNotification[] = []; notificationsError = '';
  selectedSubmissionFiles: Record<string, File[]> = {};
  private submissionFileInputs: Record<string, HTMLInputElement> = {};
  get hasUnreadNotifications(): boolean { return this.notifications.some(item => !item.isRead); }
  ngOnInit(): void { void this.run(async () => { if (await this.auth.restore()) await this.load(); }); }
  async login(): Promise<void> { await this.run(async () => {
    const session = await this.auth.login(this.product, this.email.trim(), this.password);
    this.password = '';
    if (session.requiresMfa) {
      if (session.mfaEnrollmentRequired) throw new Error('Bu hesap için MFA kurulumu gerekiyor. Personel portalından kurulumu tamamlayın.');
      this.challenge = session.mfaChallengeToken ?? '';
      if (!this.challenge) throw new Error('Doğrulama başlatılamadı.');
    } else await this.load();
  }); }
  async verify(): Promise<void> { await this.run(async () => { await this.auth.verifyMfa(this.challenge, this.mfaCode); this.challenge = ''; this.mfaCode = ''; await this.load(); }); }
  async join(): Promise<void> { await this.run(async () => { await this.auth.join(); await this.load(); this.notice = 'Kurs öğrenci alanına katıldınız.'; }); }
  async save(): Promise<void> { await this.run(async () => { await this.auth.saveProfile(this.displayName.trim()); this.notice = 'Kurs profiliniz kaydedildi.'; }); }
  async accept(id: string): Promise<void> {
    if (this.invitations.some(invitation => invitation.id === id && invitation.isReadOnly)) return;
    await this.run(async () => { await this.auth.accept(id, this.displayName.trim()); await this.load(); this.notice = 'Davet kabul edildi.'; });
  }
  async logout(): Promise<void> { await this.run(async () => { this.invitations = []; this.workspaces = []; this.courses = []; this.lessons = []; this.makeupCredits = []; this.assignments = []; this.finance = []; this.attendance = []; this.notifications = []; this.notificationsError = ''; this.selectedSubmissionFiles = {}; this.submissionFileInputs = {}; this.displayName = ''; await this.auth.logout(); }); }
  private async load(): Promise<void> {
    this.notifications = [];
    this.notificationsError = '';
    this.invitations = await this.auth.getInvitations();
    if (this.auth.product === 'course-management') {
      [this.workspaces, this.courses, this.lessons, this.makeupCredits, this.assignments, this.finance, this.attendance] = await Promise.all([
        this.auth.getWorkspaces(), this.auth.getCourses(), this.auth.getLessons(), this.auth.getMakeupCredits(),
        this.auth.getAssignments(), this.auth.getFinanceSummary(), this.auth.getAttendance()
      ]);
      for (const assignment of this.assignments) {
        assignment.submissionText ??= '';
        assignment.materials ??= [];
        assignment.submissionAttachments ??= [];
      }
      try { this.notifications = await this.auth.getNotifications(); this.notificationsError = ''; }
      catch { this.notificationsError = 'Kurs bildirimleri yüklenemedi. Yeniden deneyin.'; }
      try { this.displayName = (await this.auth.getProfile()).displayName; }
      catch (error) { if (!(error instanceof HttpErrorResponse && error.status === 404)) throw error; }
    }
  }
  async markNotificationAsRead(notification: StudentNotification): Promise<void> {
    if (notification.isRead) return;
    await this.run(async () => {
      await this.auth.markNotificationAsRead(notification.id);
      this.notifications = this.notifications.map(item => item.id === notification.id ? { ...item, isRead: true } : item);
    });
  }
  async refreshNotifications(): Promise<void> {
    this.notificationsError = '';
    try { this.notifications = await this.auth.getNotifications(); }
    catch { this.notificationsError = 'Kurs bildirimleri yüklenemedi. Yeniden deneyin.'; }
  }
  async markAllNotificationsAsRead(): Promise<void> {
    if (!this.notifications.some(item => !item.isRead)) return;
    await this.run(async () => {
      await this.auth.markAllNotificationsAsRead();
      this.notifications = this.notifications.map(item => ({ ...item, isRead: true }));
    });
  }
  canCancelLesson(lesson: StudentLesson): boolean {
    return !this.isWorkspaceReadOnly(lesson.workspaceId) && Date.parse(lesson.startsAt) > Date.now();
  }
  isWorkspaceReadOnly(workspaceId?: string): boolean {
    return !!workspaceId && this.workspaces.some(workspace => workspace.id === workspaceId && workspace.isReadOnly);
  }
  async cancelLesson(lesson: StudentLesson): Promise<void> {
    if (!this.canCancelLesson(lesson) || !window.confirm('Bu derse katılımınızı iptal etmek istiyor musunuz?')) return;
    await this.run(async () => {
      const result = await this.auth.cancelLessonParticipation(lesson.id);
      await this.load();
      this.notice = result.creditStatus === 'PendingTeacherApproval'
        ? 'Katılımınız iptal edildi. Telafi kredisi öğretmen onayı bekliyor.'
        : 'Katılımınız iptal edildi. 24 saatten az süre kaldığı için telafi kredisi oluşmadı.';
    });
  }
  async submitAssignment(assignment: StudentAssignment): Promise<void> {
    if (this.isWorkspaceReadOnly(assignment.workspaceId)) return;
    const text = assignment.submissionText?.trim() ?? '';
    if (!text) return;
    await this.run(async () => {
      await this.auth.submitAssignment(assignment.id, text);
      await this.load();
      this.notice = 'Ödev tesliminiz kaydedildi.';
    });
  }
  selectSubmissionFiles(assignmentId: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    this.submissionFileInputs[assignmentId] = input;
    const files = Array.from(input.files ?? []);
    if (files.some(file => !this.isSupportedAttachmentFile(file))) {
      this.error = 'PDF, JPEG veya PNG dosyalarını 10 MB sınırında ekleyebilirsiniz.';
      this.selectedSubmissionFiles[assignmentId] = [];
      input.value = '';
      return;
    }
    this.error = '';
    this.selectedSubmissionFiles[assignmentId] = files;
  }
  async uploadAssignmentFiles(assignment: StudentAssignment): Promise<void> {
    const files = this.selectedSubmissionFiles[assignment.id] ?? [];
    if (!files.length || !this.canUploadFile(assignment)) return;
    await this.run(async () => {
      for (const file of files) await this.auth.uploadAssignmentAttachment(assignment.id, file);
      this.selectedSubmissionFiles[assignment.id] = [];
      const input = this.submissionFileInputs[assignment.id];
      if (input) input.value = '';
      await this.load();
      this.notice = 'Ödev dosyaları teslim edildi.';
    });
  }
  async downloadAssignmentAttachment(assignment: StudentAssignment, attachment: StudentAssignmentAttachment): Promise<void> {
    await this.run(async () => {
      const blob = await this.auth.downloadAssignmentAttachment(assignment.id, attachment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = attachment.fileName; link.click();
      URL.revokeObjectURL(url);
    });
  }
  canUploadFile(assignment: StudentAssignment): boolean {
    return !this.isWorkspaceReadOnly(assignment.workspaceId)
      && Date.now() < Date.parse(assignment.dueAt) + 180 * 24 * 60 * 60 * 1000;
  }
  isSupportedAttachmentFile(file: File): boolean {
    return file.size > 0 && file.size <= 10 * 1024 * 1024
      && /\.(pdf|jpe?g|png)$/i.test(file.name);
  }
  assignmentStatus(status: string, isLate: boolean): string {
    if (status === 'Pending') return 'Teslim bekleniyor';
    if (status === 'Reviewed') return isLate ? 'Geç teslim · değerlendirildi' : 'Değerlendirildi';
    if (status === 'Late' || isLate) return 'Geç teslim';
    return 'Teslim edildi';
  }
  attendanceLabel(status: string): string {
    return ({ Present: 'Katıldı', Absent: 'Katılmadı', Excused: 'Mazeretli', Late: 'Geç katıldı' } as Record<string, string>)[status] ?? status;
  }
  money(value: number): string { return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(value); }
  lessonTime(value: string): string { return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  creditStatus(value: string): string {
    return ({ PendingTeacherApproval: 'Öğretmen onayı bekliyor', Available: 'Kullanılabilir', Scheduled: 'Telafi dersi planlandı',
      Consumed: 'Kullanıldı', Expired: 'Süresi doldu' } as Record<string, string>)[value] ?? value;
  }
  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy) return;
    this.busy = true; this.error = ''; this.notice = '';
    try { await action(); }
    catch (error) {
      const payload = error instanceof HttpErrorResponse ? error.error as { description?: string; message?: string } : null;
      const code = error instanceof HttpErrorResponse ? (error.error as { code?: string } | null)?.code : undefined;
      this.error = code === 'Workspace.ErasureReadOnly'
        ? 'Bu çalışma alanı hesap silme nedeniyle salt okunur.'
        : code === 'Subscription.ReadOnly'
        ? 'Bu çalışma alanı salt okunur. Mevcut verileri görüntüleyebilir ve indirebilirsiniz; yeniden etkinleştirmeyi yalnızca SystemAdmin yapabilir.'
        : code === 'Student.RecordSelectionRequired'
          ? 'Kurumdaki mevcut öğrenci kaydınızı bağlamak için owner’ın açıkça kayıt seçmesi gerekir. Kurum sahibinden bu daveti iptal edip doğru öğrenci kaydını seçerek yeniden göndermesini isteyin.'
        : payload?.description ?? payload?.message ?? (error instanceof Error ? error.message : 'İşlem tamamlanamadı. Lütfen tekrar deneyin.');
    } finally { this.busy = false; this.changeDetector.markForCheck(); }
  }
}
