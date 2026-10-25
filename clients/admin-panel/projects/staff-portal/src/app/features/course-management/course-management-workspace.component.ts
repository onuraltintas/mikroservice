import { ChangeDetectorRef, Component, Input, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CourseManagementService, CourseWorkspace, CourseWorkspaceSubscription, CourseMember, CourseInvitation, CourseAuditEntry, PendingCourseInvitation, CourseStudent, CourseSummary, CourseGroup, CourseBranch, CourseRoom, CourseLesson, CourseLessonSeries, CourseMakeupCredit, TeacherTimeOff, TeacherAvailabilityWindow, GuardianContact, StudentGuardianLink, StudentCsvImportPreview, LegacyStudentImportField, LegacyStudentImportPreview, LegacyInstitutionImportField, LegacyInstitutionImportPreview, LegacyInstitutionImportRequest, CourseFeeAgreement, CourseInstallment, CoursePayment, CoursePackageMovement, CourseFinanceSummary, CourseStudentAccount, CourseAssignment, CourseAssignmentSubmission, CourseAssignmentAttachment, CourseReportTable, buildCourseAssignmentProgressReport, buildCourseFinanceSummaryCsv, buildCourseFinanceWorkspaceReport, buildCourseLessonAttendanceReport, buildCourseReportCsv, buildCourseReportPrintHtml, buildCourseStudentAccountCsv, buildCourseStudentAccountPrintHtml, buildCourseStudentRosterReport } from './course-management.service';
import { CourseManagementLegacyImportService, LegacyInstitutionCategory, LegacyInstitutionSourceInfo, LegacyInstitutionSourcePage, LegacyStudentSource, LegacyStudentSourcePage, LegacyStudentSourceRow, LegacyTeacherSourcePage, LegacyTeacherSourceRow } from './course-management-legacy-import.service';

const courseAuditActionLabels: Record<string, string> = {
  'Workspace.Created': 'Çalışma alanı oluşturuldu',
  'Subscription.Assigned': 'Platform planı atandı',
  'Subscription.Updated': 'Platform planı güncellendi',
  'Invitation.Created': 'Davet oluşturuldu',
  'Invitation.Accepted': 'Davet kabul edildi',
  'Invitation.Cancelled': 'Davet iptal edildi',
  'Membership.Revoked': 'Üyelik kapatıldı',
  'Student.Created': 'Öğrenci kaydı oluşturuldu',
  'Student.Imported': 'Öğrenci içe aktarıldı',
  'Student.Updated': 'Öğrenci kaydı güncellendi',
  'Student.Archived': 'Öğrenci arşivlendi',
  'Student.Restored': 'Öğrenci kaydı yeniden açıldı',
  'GuardianContact.Created': 'Veli iletişim kaydı oluşturuldu',
  'GuardianContact.Updated': 'Veli iletişim kaydı güncellendi',
  'StudentGuardian.Linked': 'Veli öğrenciye bağlandı',
  'StudentGuardian.Unlinked': 'Veli bağlantısı kaldırıldı',
  'Course.Created': 'Kurs oluşturuldu',
  'CourseBranch.Created': 'Şube oluşturuldu',
  'CourseRoom.Created': 'Derslik oluşturuldu',
  'Group.Created': 'Grup oluşturuldu',
  'Enrollment.Created': 'Grup kaydı oluşturuldu',
  'Enrollment.Restored': 'Grup kaydı yeniden açıldı',
  'MakeupCredit.Granted': 'Telafi hakkı verildi',
  'MakeupCredit.Approved': 'Telafi hakkı onaylandı',
  'MakeupCredit.Scheduled': 'Telafi dersi planlandı',
  'MakeupCredit.Released': 'Telafi hakkı serbest bırakıldı',
  'MakeupCredit.Consumed': 'Telafi hakkı kullanıldı',
  'TeacherTimeOff.Created': 'Öğretmen izin aralığı oluşturuldu',
  'TeacherTimeOff.Deleted': 'Öğretmen izin aralığı kaldırıldı',
  'TeacherAvailability.Created': 'Öğretmen uygunluk saati oluşturuldu',
  'TeacherAvailability.Deleted': 'Öğretmen uygunluk saati kaldırıldı',
  'LessonSeries.Created': 'Tekrarlayan ders serisi oluşturuldu',
  'LessonSeries.ScheduleUpdated': 'Ders serisi güncellendi',
  'LessonSeries.FutureCancelled': 'Ders serisinin gelecek oturumları iptal edildi',
  'LessonSession.Created': 'Ders planlandı',
  'LessonSession.Rescheduled': 'Dersin zamanı değiştirildi',
  'LessonSession.Cancelled': 'Ders iptal edildi',
  'LessonSession.SeriesSlotCancelled': 'Ders serisi oturumu iptal edildi',
  'LessonSession.Completed': 'Ders tamamlandı',
  'LessonParticipation.Cancelled': 'Öğrenci derse katılımını iptal etti',
  'Attendance.Marked': 'Yoklama alındı',
  'Attendance.Updated': 'Yoklama güncellendi',
  'Finance.AgreementCreated': 'Ücret anlaşması oluşturuldu',
  'Finance.AgreementClosed': 'Ücret anlaşması kapatıldı',
  'Finance.InstallmentCreated': 'Taksit oluşturuldu',
  'Finance.PaymentRecorded': 'Tahsilat kaydedildi',
  'Finance.PaymentReversed': 'Tahsilat ters kayda alındı',
  'Finance.InstallmentReversed': 'Borç ters kayda alındı',
  'Finance.PackageMovementReversed': 'Paket hareketi ters kayda alındı',
  'Finance.LessonCharged': 'Ders ücreti tahakkuk etti',
  'Finance.PackageConsumed': 'Paket hakkı tüketildi',
  'Assignment.Created': 'Ödev oluşturuldu',
  'Assignment.Reviewed': 'Ödev değerlendirildi',
  'Assignment.MaterialUploaded': 'Ödev dosyası yüklendi',
  'Assignment.FileSubmitted': 'Ödev dosyası teslim edildi',
  'Assignment.FileResubmitted': 'Ödev dosyası yeniden teslim edildi',
  'Assignment.Submitted': 'Ödev teslim edildi'
};

@Component({
  selector: 'staff-course-management-workspace',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './course-management-workspace.component.html',
  styleUrl: './course-management-workspace.component.scss'
})
export class CourseManagementWorkspaceComponent implements OnInit {
  @Input({ required: true }) role!: 'teacher' | 'institution';
  readonly auth = inject(StaffAuthService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly service = inject(CourseManagementService);
  private readonly legacyImportService = inject(CourseManagementLegacyImportService);
  displayName = '';
  workspaceName = '';
  targetUserId = '';
  inviteRole: 1 | 2 = 1;
  inviteStudentRecordId = '';
  selectedId = '';
  workspaces: CourseWorkspace[] = [];
  ownerSubscriptions: CourseWorkspaceSubscription[] = [];
  members: CourseMember[] = [];
  invitations: CourseInvitation[] = [];
  auditEntries: CourseAuditEntry[] = [];
  pending: PendingCourseInvitation[] = [];
  students: CourseStudent[] = [];
  guardians: GuardianContact[] = [];
  studentGuardianLinks: StudentGuardianLink[] = [];
  courses: CourseSummary[] = [];
  groups: CourseGroup[] = [];
  branches: CourseBranch[] = [];
  rooms: CourseRoom[] = [];
  lessons: CourseLesson[] = [];
  lessonSeries: CourseLessonSeries[] = [];
  makeupCredits: CourseMakeupCredit[] = [];
  makeupStartsAt: Record<string, string> = {};
  teacherTimeOff: TeacherTimeOff[] = [];
  teacherAvailability: TeacherAvailabilityWindow[] = [];
  assignments: CourseAssignment[] = [];
  assignmentSubmissions: CourseAssignmentSubmission[] = [];
  assignmentMaterials: CourseAssignmentAttachment[] = [];
  selectedMaterialFiles: File[] = [];
  private assignmentMaterialInput?: HTMLInputElement;
  assignmentTarget: 'group' | 'student' = 'group';
  assignmentCourseId = '';
  assignmentGroupId = '';
  assignmentStudentId = '';
  assignmentTitle = '';
  assignmentInstructions = '';
  assignmentDueAt = '';
  selectedAssignmentId = '';
  assignmentScores: Record<string, number | null> = {};
  assignmentFeedback: Record<string, string> = {};
  financeSummary: CourseFinanceSummary | null = null;
  financeAccount: CourseStudentAccount | null = null;
  financeStudentId = '';
  financeCourseId = '';
  feeAgreementModel: 1 | 2 | 3 | 4 = 1;
  feeAgreementAmount: number | null = null;
  feeBillingUnit: 1 | 2 = 1;
  packageSessions: number | null = 5;
  packageExpiresOn = '';
  selectedFeeAgreementId = '';
  installmentLabel = '';
  installmentAmount: number | null = null;
  installmentDueOn = '';
  paymentAmount: number | null = null;
  paymentMethod: 1 | 2 = 1;
  paymentNote = '';
  paymentAllocationAmounts: Record<string, number> = {};
  private paymentRequestId = crypto.randomUUID();
  private paymentReceivedAt = new Date().toISOString();
  studentName = '';
  studentEmail = '';
  studentPhone = '';
  studentCsv = '';
  studentCsvFileName = '';
  studentCsvPreview: StudentCsvImportPreview | null = null;
  legacySource: LegacyStudentSource = 'coaching';
  legacySourcePage: LegacyStudentSourcePage | null = null;
  legacySourcePageNumber = 1;
  legacySelectedRows = new Map<string, LegacyStudentSourceRow>();
  legacyImportPreview: LegacyStudentImportPreview | null = null;
  legacyIncludeEmail = false;
  legacyIncludeActiveStatus = false;
  legacyTeacherSource: LegacyStudentSource = 'coaching';
  legacyTeacherInstitutionPage: LegacyInstitutionSourcePage | null = null;
  legacyTeacherInstitutionId = '';
  legacyTeacherSourcePage: LegacyTeacherSourcePage | null = null;
  legacyTeacherSourcePageNumber = 1;
  legacySelectedTeachers = new Map<string, LegacyTeacherSourceRow>();
  legacyInstitutionSource: LegacyStudentSource = 'coaching';
  legacyInstitutionSourcePage: LegacyInstitutionSourcePage | null = null;
  legacyInstitutionSourceId = '';
  legacyInstitutionInfo: LegacyInstitutionSourceInfo | null = null;
  legacyInstitutionIncludeName = false;
  legacyInstitutionIncludeType = false;
  legacyInstitutionPreview: LegacyInstitutionImportPreview | null = null;
  editingStudentId = '';
  editingStudentName = '';
  editingStudentEmail = '';
  editingStudentPhone = '';
  courseName = '';
  courseSubject = '';
  groupName = '';
  branchName = '';
  roomBranchId = '';
  roomName = '';
  roomCapacity: number | null = null;
  selectedCourseId = '';
  selectedTeacherId = '';
  selectedStudentId = '';
  guardianStudentId = '';
  selectedGuardianId = '';
  guardianName = '';
  guardianEmail = '';
  guardianPhone = '';
  guardianRelationship = 1;
  editingGuardianId = '';
  editingGuardianName = '';
  editingGuardianEmail = '';
  editingGuardianPhone = '';
  selectedGroupId = '';
  lessonMode: 'group' | 'private' = 'group';
  lessonGroupId = '';
  lessonCourseId = '';
  lessonStudentId = '';
  lessonTeacherId = '';
  lessonRoomId = '';
  lessonSchedule: 'once' | 'weekly' = 'once';
  lessonStartsAt = '';
  lessonEndsAt = '';
  lessonSeriesStartsOn = '';
  lessonSeriesEndsOn = '';
  lessonSeriesStartTime = '';
  lessonSeriesDurationMinutes = 60;
  lessonSeriesTimeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone || 'Europe/Istanbul';
  lessonDaysOfWeek = [false, false, false, false, false, false, false];
  editingSeriesId = '';
  editingSeriesEndsOn = '';
  editingSeriesStartTime = '';
  editingSeriesDurationMinutes = 60;
  editingSeriesDaysOfWeek = [false, false, false, false, false, false, false];
  editingSeriesRoomId = '';
  editingLessonId = '';
  editingLessonStartsAt = '';
  editingLessonEndsAt = '';
  editingLessonRoomId = '';
  timeOffTeacherUserId = this.auth.getCurrentUserId() ?? '';
  timeOffStartsAt = '';
  timeOffEndsAt = '';
  timeOffReason = '';
  availabilityTeacherUserId = this.auth.getCurrentUserId() ?? '';
  availabilityWeekday = 0;
  availabilityStartTime = '09:00';
  availabilityEndTime = '17:00';
  availabilityTimeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone || 'Europe/Istanbul';
  readonly weekdays = [
    { label: 'Pzt', index: 0 }, { label: 'Sal', index: 1 }, { label: 'Çar', index: 2 },
    { label: 'Per', index: 3 }, { label: 'Cum', index: 4 }, { label: 'Cmt', index: 5 }, { label: 'Paz', index: 6 }
  ];
  busy = false;
  error = '';
  notice = '';
  private workspaceRequestId = crypto.randomUUID();
  private invitationRequestId = crypto.randomUUID();
  private guardianRequestId = crypto.randomUUID();
  get selected(): CourseWorkspace | undefined { return this.workspaces.find(item => item.id === this.selectedId); }
  get legacySourceOptions(): LegacyStudentSource[] {
    return this.auth.products()
      .filter(access => access.product !== 'course-management'
        && access.roles.some(role => role === 'InstitutionAdmin' || role === 'InstitutionOwner'))
      .map(access => access.product as LegacyStudentSource);
  }
  get canImportLegacyStudents(): boolean { return this.isOwner && this.legacySourceOptions.length > 0; }
  get canImportLegacyTeachers(): boolean { return this.isOwner && this.legacySourceOptions.length > 0; }
  get canImportLegacyInstitution(): boolean {
    return this.isOwner && this.selected?.type === 'Institution' && this.legacySourceOptions.length > 0;
  }
  get legacySourcePageCount(): number {
    return this.legacySourcePage ? Math.max(1, Math.ceil(this.legacySourcePage.totalCount / this.legacySourcePage.pageSize)) : 0;
  }
  get legacyTeacherSourcePageCount(): number {
    return this.legacyTeacherSourcePage
      ? Math.max(1, Math.ceil(this.legacyTeacherSourcePage.totalCount / this.legacyTeacherSourcePage.pageSize)) : 0;
  }
  get legacyTeacherInstitutionPageCount(): number {
    return this.legacyTeacherInstitutionPage
      ? Math.max(1, Math.ceil(this.legacyTeacherInstitutionPage.totalCount / this.legacyTeacherInstitutionPage.pageSize)) : 0;
  }
  get legacyInstitutionSourcePageCount(): number {
    return this.legacyInstitutionSourcePage
      ? Math.max(1, Math.ceil(this.legacyInstitutionSourcePage.totalCount / this.legacyInstitutionSourcePage.pageSize)) : 0;
  }
  get selectedLegacyTeachers(): LegacyTeacherSourceRow[] { return Array.from(this.legacySelectedTeachers.values()); }
  get legacySelectedFields(): LegacyStudentImportField[] {
    return ['displayName', ...(this.legacyIncludeEmail ? ['email' as const] : []),
      ...(this.legacyIncludeActiveStatus ? ['isActive' as const] : [])];
  }
  legacySourceLabel(source: LegacyStudentSource): string {
    return source === 'coaching' ? 'Koçluk' : 'Hızlı Okuma';
  }
  legacyInstitutionCategoryLabel(category: LegacyInstitutionCategory | null): string {
    if (!category) return 'Belirtilmemiş';
    const labels: Record<LegacyInstitutionCategory, string> = {
      School: 'Okul', PrivateCourse: 'Dershane', StudyCenter: 'Etüt merkezi', OnlinePlatform: 'Online platform'
    };
    return labels[category];
  }
  changeLegacySource(source: LegacyStudentSource): void {
    if (!this.legacySourceOptions.includes(source)) return;
    this.legacySource = source;
    this.legacySourcePage = null;
    this.legacySourcePageNumber = 1;
    this.legacySelectedRows.clear();
    this.legacyImportPreview = null;
  }
  async loadLegacySourcePage(pageNumber = 1): Promise<void> {
    if (!this.canImportLegacyStudents || !this.selected || !this.legacySourceOptions.includes(this.legacySource)) return;
    await this.run(async () => {
      this.legacySourcePage = await this.legacyImportService.getStudentPage(this.legacySource, pageNumber);
      this.legacySourcePageNumber = this.legacySourcePage.pageNumber;
      this.legacyImportPreview = null;
      this.notice = `${this.legacySourceLabel(this.legacySource)} kaynağından ${this.legacySourcePage.items.length} öğrenci yüklendi.`;
    });
  }
  toggleLegacyStudent(row: LegacyStudentSourceRow, event: Event): void {
    const selected = (event.target as HTMLInputElement).checked;
    if (selected) {
      if (!this.legacySelectedRows.has(row.sourceRecordId) && this.legacySelectedRows.size >= 500) {
        this.error = 'Tek aktarımda en fazla 500 öğrenci seçebilirsiniz.';
        return;
      }
      this.legacySelectedRows.set(row.sourceRecordId, row);
    } else {
      this.legacySelectedRows.delete(row.sourceRecordId);
    }
    this.legacyImportPreview = null;
    this.error = '';
  }
  private legacyImportRows(): LegacyStudentSourceRow[] {
    return Array.from(this.legacySelectedRows.values(), row => ({
      sourceRecordId: row.sourceRecordId,
      displayName: row.displayName,
      email: this.legacyIncludeEmail ? row.email : null,
      isActive: this.legacyIncludeActiveStatus ? row.isActive : true
    }));
  }
  async previewLegacyStudents(): Promise<void> {
    if (!this.canImportLegacyStudents || !this.selected || this.legacySelectedRows.size === 0 || this.legacySelectedRows.size > 500) return;
    await this.run(async () => {
      this.legacyImportPreview = await this.service.previewLegacyStudents(this.selectedId, this.legacySource,
        this.legacySelectedFields, this.legacyImportRows());
      this.notice = 'Aktarım önizlemesi hazır. Çakışma, hatalı veya yinelenen kayıtlar düzeltilmeden aktarım yapılmaz.';
    });
  }
  async commitLegacyStudents(): Promise<void> {
    if (!this.canImportLegacyStudents || !this.selected || this.selected.isReadOnly || !this.legacyImportPreview
      || this.legacyImportPreview.readyCount === 0 || this.legacyImportPreview.invalidCount > 0
      || this.legacyImportPreview.conflictCount > 0 || this.legacyImportPreview.duplicateCount > 0) return;
    await this.run(async () => {
      const result = await this.service.commitLegacyStudents(this.selectedId, this.legacyImportPreview!.previewId,
        this.legacySource, this.legacySelectedFields, this.legacyImportRows());
      await this.loadLearningData();
      this.legacyImportPreview = await this.service.previewLegacyStudents(this.selectedId, this.legacySource,
        this.legacySelectedFields, this.legacyImportRows());
      this.notice = `${result.createdCount} öğrenci aktarıldı; ${result.alreadyImportedCount} kayıt zaten aktarılmıştı.`;
    });
  }
  changeLegacyTeacherSource(source: LegacyStudentSource): void {
    if (!this.legacySourceOptions.includes(source)) return;
    this.legacyTeacherSource = source;
    this.legacyTeacherInstitutionPage = null;
    this.legacyTeacherInstitutionId = '';
    this.legacyTeacherSourcePage = null;
    this.legacyTeacherSourcePageNumber = 1;
    this.legacySelectedTeachers.clear();
  }
  async loadLegacyTeacherInstitutions(pageNumber = 1): Promise<void> {
    if (!this.canImportLegacyTeachers || !this.selected || !this.legacySourceOptions.includes(this.legacyTeacherSource)) return;
    await this.run(async () => {
      this.legacyTeacherInstitutionPage = await this.legacyImportService.getInstitutionPage(this.legacyTeacherSource, pageNumber);
      if (this.legacyTeacherInstitutionId
        && !this.legacyTeacherInstitutionPage.items.some(item => item.sourceInstitutionId === this.legacyTeacherInstitutionId)) {
        this.legacyTeacherInstitutionId = '';
        this.legacyTeacherSourcePage = null;
        this.legacySelectedTeachers.clear();
      }
      this.notice = `${this.legacySourceLabel(this.legacyTeacherSource)} kaynağından ${this.legacyTeacherInstitutionPage.items.length} kurum yüklendi; aktarılacak kurumu siz seçin.`;
    });
  }
  selectLegacyTeacherInstitution(sourceInstitutionId: string): void {
    if (!sourceInstitutionId) {
      this.legacyTeacherInstitutionId = '';
      this.legacyTeacherSourcePage = null;
      this.legacySelectedTeachers.clear();
      return;
    }
    if (!this.legacyTeacherInstitutionPage?.items.some(item => item.sourceInstitutionId === sourceInstitutionId)) return;
    this.legacyTeacherInstitutionId = sourceInstitutionId;
    this.legacyTeacherSourcePage = null;
    this.legacySelectedTeachers.clear();
  }
  async loadLegacyTeacherPage(pageNumber = 1): Promise<void> {
    if (!this.canImportLegacyTeachers || !this.selected || !this.legacyTeacherInstitutionId
      || !this.legacySourceOptions.includes(this.legacyTeacherSource)) return;
    await this.run(async () => {
      this.legacyTeacherSourcePage = await this.legacyImportService.getTeacherPage(
        this.legacyTeacherSource, this.legacyTeacherInstitutionId, pageNumber);
      this.legacyTeacherSourcePageNumber = this.legacyTeacherSourcePage.pageNumber;
      this.notice = `${this.legacySourceLabel(this.legacyTeacherSource)} kaynağından ${this.legacyTeacherSourcePage.items.length} öğretmen yüklendi.`;
    });
  }
  legacyTeacherStatus(row: LegacyTeacherSourceRow): string | null {
    if (!row.isActive) return 'Kaynakta etkin değil';
    if (row.sourceUserId === this.auth.getCurrentUserId()) return 'Kendi hesabınız';
    if (this.members.some(member => member.userId === row.sourceUserId && member.role === 'Teacher' && member.isActive))
      return 'Bu alanda etkin öğretmen';
    const pending = this.invitations.find(invitation => invitation.targetUserId === row.sourceUserId && invitation.status === 'Pending');
    if (pending) return pending.role === 'Teacher' ? 'Öğretmen daveti bekliyor' : 'Başka rol için davet bekliyor';
    return null;
  }
  toggleLegacyTeacher(row: LegacyTeacherSourceRow, event: Event): void {
    if (!this.canImportLegacyTeachers || this.legacyTeacherStatus(row)) return;
    const selected = (event.target as HTMLInputElement).checked;
    if (selected) {
      if (!this.legacySelectedTeachers.has(row.sourceUserId) && this.legacySelectedTeachers.size >= 500) {
        this.error = 'Tek aktarımda en fazla 500 öğretmen seçebilirsiniz.';
        return;
      }
      this.legacySelectedTeachers.set(row.sourceUserId, row);
    } else {
      this.legacySelectedTeachers.delete(row.sourceUserId);
    }
    this.error = '';
  }
  async createLegacyTeacherInvitations(): Promise<void> {
    if (!this.canImportLegacyTeachers || !this.selected || this.selected.isReadOnly
      || !this.legacyTeacherInstitutionId || this.legacySelectedTeachers.size === 0) return;
    const teachers = this.selectedLegacyTeachers.filter(row => !this.legacyTeacherStatus(row));
    if (teachers.length === 0) return;
    await this.run(async () => {
      let createdCount = 0;
      let conflictCount = 0;
      for (const teacher of teachers) {
        try {
          await this.service.invite(this.selectedId, crypto.randomUUID(), teacher.sourceUserId, 2, null);
          createdCount++;
        } catch (error) {
          if (error instanceof HttpErrorResponse && error.status === 409) conflictCount++;
          else throw error;
        }
      }
      this.legacySelectedTeachers.clear();
      await this.loadOwnerData();
      this.notice = `${createdCount} öğretmen daveti oluşturuldu${conflictCount ? `; ${conflictCount} kayıt değişiklik nedeniyle atlandı` : ''}. Üyelik davet kabul edilince etkinleşir.`;
    });
  }
  changeLegacyInstitutionSource(source: LegacyStudentSource): void {
    if (!this.legacySourceOptions.includes(source)) return;
    this.legacyInstitutionSource = source;
    this.legacyInstitutionSourcePage = null;
    this.legacyInstitutionSourceId = '';
    this.legacyInstitutionInfo = null;
    this.legacyInstitutionPreview = null;
    this.legacyInstitutionIncludeName = false;
    this.legacyInstitutionIncludeType = false;
  }
  async loadLegacyInstitutionInfo(pageNumber = 1): Promise<void> {
    if (!this.canImportLegacyInstitution || !this.selected || !this.legacySourceOptions.includes(this.legacyInstitutionSource)) return;
    await this.run(async () => {
      this.legacyInstitutionSourcePage = await this.legacyImportService.getInstitutionPage(this.legacyInstitutionSource, pageNumber);
      this.legacyInstitutionSourceId = '';
      this.legacyInstitutionInfo = null;
      this.legacyInstitutionPreview = null;
      this.notice = `${this.legacySourceLabel(this.legacyInstitutionSource)} kaynağından ${this.legacyInstitutionSourcePage.items.length} kurum yüklendi; aktarılacak kurumu siz seçin.`;
    });
  }
  selectLegacyInstitutionSourceInstitution(sourceInstitutionId: string): void {
    if (!sourceInstitutionId) {
      this.legacyInstitutionSourceId = '';
      this.legacyInstitutionInfo = null;
      this.legacyInstitutionIncludeName = false;
      this.legacyInstitutionIncludeType = false;
      this.legacyInstitutionPreview = null;
      return;
    }
    const institution = this.legacyInstitutionSourcePage?.items.find(item => item.sourceInstitutionId === sourceInstitutionId);
    if (!institution) return;
    this.legacyInstitutionSourceId = sourceInstitutionId;
    this.legacyInstitutionInfo = institution;
    this.legacyInstitutionIncludeName = false;
    this.legacyInstitutionIncludeType = false;
    this.legacyInstitutionPreview = null;
  }
  private legacyInstitutionImportRequest(): LegacyInstitutionImportRequest | null {
    if (!this.legacyInstitutionInfo || this.legacyInstitutionInfo.sourceInstitutionId !== this.legacyInstitutionSourceId) return null;
    const selectedFields: LegacyInstitutionImportField[] = [
      ...(this.legacyInstitutionIncludeName ? ['name' as const] : []),
      ...(this.legacyInstitutionIncludeType ? ['type' as const] : [])
    ];
    if (selectedFields.length === 0) return null;
    return {
      sourceSystem: this.legacyInstitutionSource,
      sourceInstitutionId: this.legacyInstitutionInfo.sourceInstitutionId,
      selectedFields,
      name: this.legacyInstitutionIncludeName ? this.legacyInstitutionInfo.name : null,
      institutionCategory: this.legacyInstitutionIncludeType ? this.legacyInstitutionInfo.institutionCategory : null
    };
  }
  async previewLegacyInstitution(): Promise<void> {
    const request = this.legacyInstitutionImportRequest();
    if (!this.canImportLegacyInstitution || !this.selected || !request) return;
    await this.run(async () => {
      this.legacyInstitutionPreview = await this.service.previewLegacyInstitution(this.selectedId, request);
      this.notice = 'Kurum aktarım önizlemesi hazır. Yalnızca seçili alanlar kaydedilecek.';
    });
  }
  async commitLegacyInstitution(): Promise<void> {
    const request = this.legacyInstitutionImportRequest();
    if (!this.canImportLegacyInstitution || !this.selected || this.selected.isReadOnly || !request || !this.legacyInstitutionPreview) return;
    await this.run(async () => {
      const result = await this.service.commitLegacyInstitution(this.selectedId, request, this.legacyInstitutionPreview!);
      this.legacyInstitutionPreview = null;
      await this.reload();
      this.notice = result.alreadyApplied ? 'Bu kurum bilgileri zaten çalışma alanında kayıtlı.' : 'Seçilen kurum bilgileri aktarıldı.';
    });
  }
  get selectedSubscription(): CourseWorkspaceSubscription | undefined {
    return this.ownerSubscriptions.find(item => item.workspaceId === this.selectedId);
  }
  get selectedAssignment(): CourseAssignment | undefined { return this.assignments.find(item => item.id === this.selectedAssignmentId); }
  get isOwner(): boolean { return this.selected?.role === 'Owner'; }
  get canManageLearningData(): boolean { return this.isOwner || this.selected?.role === 'Teacher'; }
  auditActionLabel(action: string): string {
    return courseAuditActionLabels[action] ?? action;
  }
  invitationStudentLabel(invitation: CourseInvitation): string {
    if (invitation.role !== 'Student') return '';
    return invitation.studentRecordId ? ' · Mevcut öğrenci kaydı seçildi' : ' · Yeni öğrenci kaydı';
  }
  formatAuditTimestamp(value: string): string {
    return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  }
  get hasActiveStudent(): boolean { return this.students.some(student => student.isActive); }
  get unlinkedActiveStudents(): CourseStudent[] { return this.students.filter(student => student.isActive && !student.userId); }
  get assignmentGroups(): CourseGroup[] { return this.groups.filter(group => group.courseId === this.assignmentCourseId); }
  get canCreateAssignment(): boolean {
    return !!this.selectedId && this.canManageLearningData && !!this.assignmentCourseId && !!this.assignmentTitle.trim()
      && !!this.assignmentDueAt && (this.assignmentTarget === 'group'
        ? this.assignmentGroups.some(group => group.id === this.assignmentGroupId)
        : this.students.some(student => student.id === this.assignmentStudentId && student.isActive));
  }
  get financeAllocationsTotal(): number {
    return Object.values(this.paymentAllocationAmounts).reduce((total, amount) => total + (Number(amount) || 0), 0);
  }
  get canRecordPayment(): boolean {
    const amount = Number(this.paymentAmount);
    return !!this.financeAccount && amount > 0 && this.financeAllocationsTotal <= amount
      && this.financeAccount.installments.filter(item => Number(this.paymentAllocationAmounts[item.id]) > 0)
        .every(item => Number(this.paymentAllocationAmounts[item.id]) <= item.remainingAmount);
  }
  ngOnInit(): void { void this.run(async () => {
    this.resetLessonTimes();
    this.resetTimeOffTimes();
    this.resetFinanceDates();
    try { this.displayName = (await this.service.getProfile()).displayName; }
    catch (error) { if (!(error instanceof HttpErrorResponse && error.status === 404)) throw error; }
    await this.reload();
  }); }
  async saveProfile(): Promise<void> { await this.run(async () => {
    this.displayName = (await this.service.saveProfile(this.displayName.trim())).displayName;
    this.notice = 'Kurs profiliniz kaydedildi.';
  }); }
  async selectFinanceStudent(studentId: string): Promise<void> {
    this.financeStudentId = studentId;
    this.selectedFeeAgreementId = '';
    this.paymentAllocationAmounts = {};
    this.paymentFormChanged();
    await this.run(() => this.loadFinanceAccount());
  }
  exportFinanceSummary(): void {
    if (!this.isOwner || !this.financeSummary) return;
    const summary = this.financeSummary;
    const objectUrl = URL.createObjectURL(new Blob([buildCourseFinanceSummaryCsv(summary)], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = `kurs-finans-ozeti-${summary.from}-${summary.to}.csv`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
  }
  async exportFinanceWorkspaceReportCsv(): Promise<void> {
    if (!this.isOwner || !this.selected) return;
    await this.run(async () => this.downloadReportCsv(buildCourseFinanceWorkspaceReport(
      await this.service.getFinanceWorkspaceReport(this.selectedId)), 'kurs-finans-borc-paket'));
  }
  async exportFinanceWorkspaceReportPdf(): Promise<void> {
    if (!this.isOwner || !this.selected) return;
    await this.run(async () => this.printReport(buildCourseFinanceWorkspaceReport(
      await this.service.getFinanceWorkspaceReport(this.selectedId))));
  }
  exportStudentAccountCsv(): void {
    if (!this.isOwner || !this.financeAccount || !this.financeStudentId) return;
    const student = this.students.find(item => item.id === this.financeStudentId);
    const workspace = this.workspaces.find(item => item.id === this.selectedId);
    if (!student || !workspace) return;
    const objectUrl = URL.createObjectURL(new Blob([
      buildCourseStudentAccountCsv(workspace.name, student.displayName, this.financeAccount)
    ], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = `kurs-ogrenci-ekstresi-${this.financeStudentId}.csv`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
  }
  exportStudentAccountPdf(): void {
    if (!this.isOwner || !this.financeAccount || !this.financeStudentId) return;
    const student = this.students.find(item => item.id === this.financeStudentId);
    const workspace = this.workspaces.find(item => item.id === this.selectedId);
    if (!student || !workspace) return;
    const printWindow = window.open('', '_blank');
    if (!printWindow) {
      this.error = 'PDF yazdırma penceresi açılamadı. Tarayıcı açılır pencere ayarını kontrol edin.';
      return;
    }
    printWindow.opener = null;
    printWindow.document.open();
    printWindow.document.write(buildCourseStudentAccountPrintHtml(workspace.name, student.displayName, this.financeAccount));
    printWindow.document.close();
    printWindow.focus();
    window.setTimeout(() => printWindow.print(), 100);
  }
  async exportStudentRosterCsv(): Promise<void> {
    if (!this.canManageLearningData || !this.selected) return;
    await this.run(async () => this.downloadReportCsv(
      buildCourseStudentRosterReport(await this.service.getStudentRosterForReport(this.selectedId)), 'kurs-ogrenci-listesi'));
  }
  async exportStudentRosterPdf(): Promise<void> {
    if (!this.canManageLearningData || !this.selected) return;
    await this.run(async () => this.printReport(buildCourseStudentRosterReport(
      await this.service.getStudentRosterForReport(this.selectedId))));
  }
  async exportLessonAttendanceCsv(): Promise<void> {
    if (!this.canManageLearningData || !this.selected) return;
    await this.run(async () => this.downloadReportCsv(
      buildCourseLessonAttendanceReport(await this.service.getLessonAttendanceForReport(this.selectedId)), 'kurs-ders-yoklama'));
  }
  async exportLessonAttendancePdf(): Promise<void> {
    if (!this.canManageLearningData || !this.selected) return;
    await this.run(async () => this.printReport(buildCourseLessonAttendanceReport(
      await this.service.getLessonAttendanceForReport(this.selectedId))));
  }
  async exportAssignmentProgressCsv(): Promise<void> {
    if (!this.canManageLearningData || !this.selected) return;
    await this.run(async () => this.downloadReportCsv(
      buildCourseAssignmentProgressReport(await this.service.getAssignmentProgressForReport(this.selectedId)), 'kurs-odev-teslim-ozeti'));
  }
  async exportAssignmentProgressPdf(): Promise<void> {
    if (!this.canManageLearningData || !this.selected) return;
    await this.run(async () => this.printReport(buildCourseAssignmentProgressReport(
      await this.service.getAssignmentProgressForReport(this.selectedId))));
  }
  private downloadReportCsv(report: CourseReportTable, filePrefix: string): void {
    if (!this.selected) return;
    const objectUrl = URL.createObjectURL(new Blob([buildCourseReportCsv(this.selected.name, report)], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = `${filePrefix}-${this.selected.id}.csv`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
  }
  private printReport(report: CourseReportTable): void {
    if (!this.selected) return;
    const printWindow = window.open('', '_blank');
    if (!printWindow) {
      this.error = 'Yazdırma penceresi açılamadı. Tarayıcı açılır pencere ayarını kontrol edin.';
      return;
    }
    printWindow.opener = null;
    printWindow.document.open();
    printWindow.document.write(buildCourseReportPrintHtml(this.selected.name, report));
    printWindow.document.close();
    printWindow.focus();
    window.setTimeout(() => printWindow.print(), 100);
  }
  async createAssignment(): Promise<void> {
    if (!this.selected || !this.canCreateAssignment) return;
    await this.run(async () => {
      const assignment = await this.service.createAssignment(this.selectedId, {
        id: crypto.randomUUID(), courseId: this.assignmentCourseId,
        groupId: this.assignmentTarget === 'group' ? this.assignmentGroupId : null,
        studentId: this.assignmentTarget === 'student' ? this.assignmentStudentId : null,
        title: this.assignmentTitle.trim(), instructions: this.assignmentInstructions.trim() || null,
        dueAt: new Date(this.assignmentDueAt).toISOString()
      });
      this.assignmentTitle = ''; this.assignmentInstructions = '';
      this.assignments = await this.service.getAssignments(this.selectedId);
      await this.selectAssignment(assignment.id);
      this.notice = 'Ödev yayımlandı.';
    });
  }
  async selectAssignment(assignmentId: string): Promise<void> {
    this.selectedAssignmentId = assignmentId;
    this.selectedMaterialFiles = [];
    await this.loadAssignmentSubmissions();
  }
  selectAssignmentMaterialFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.assignmentMaterialInput = input;
    const files = Array.from(input.files ?? []);
    if (files.some(file => !this.isSupportedAttachmentFile(file))) {
      this.error = 'PDF, JPEG veya PNG dosyalarını 10 MB sınırında ekleyebilirsiniz.';
      this.selectedMaterialFiles = [];
      input.value = '';
      return;
    }
    this.error = '';
    this.selectedMaterialFiles = files;
  }
  async uploadAssignmentMaterials(): Promise<void> {
    if (!this.selected || !this.selectedAssignmentId || !this.selectedMaterialFiles.length) return;
    await this.run(async () => {
      for (const file of this.selectedMaterialFiles)
        await this.service.uploadAssignmentAttachment(this.selectedId, this.selectedAssignmentId, file);
      this.selectedMaterialFiles = [];
      if (this.assignmentMaterialInput) this.assignmentMaterialInput.value = '';
      await this.loadAssignmentSubmissions();
      this.notice = 'Ödev dosyaları eklendi.';
    });
  }
  async downloadAssignmentAttachment(attachment: CourseAssignmentAttachment): Promise<void> {
    if (!this.selected || !this.selectedAssignmentId) return;
    await this.run(async () => {
      const blob = await this.service.downloadAssignmentAttachment(this.selectedId, this.selectedAssignmentId, attachment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = attachment.fileName; link.click();
      URL.revokeObjectURL(url);
    });
  }
  isSupportedAttachmentFile(file: File): boolean {
    return file.size > 0 && file.size <= 10 * 1024 * 1024
      && /\.(pdf|jpe?g|png)$/i.test(file.name);
  }
  async saveAssignmentReview(submission: CourseAssignmentSubmission): Promise<void> {
    if (!this.selected || !this.selectedAssignmentId || !submission.submissionId) return;
    await this.run(async () => {
      const score = this.assignmentScores[submission.studentId];
      await this.service.reviewAssignment(this.selectedId, this.selectedAssignmentId, submission.studentId,
        score === null || score === undefined ? null : Number(score),
        this.assignmentFeedback[submission.studentId]?.trim() ?? '');
      await this.loadAssignmentSubmissions();
      this.notice = 'Ödev değerlendirmesi kaydedildi.';
    });
  }
  assignmentStatus(status: string, isLate: boolean): string {
    if (status === 'Pending') return 'Teslim bekleniyor';
    if (status === 'Reviewed') return isLate ? 'Geç teslim · değerlendirildi' : 'Değerlendirildi';
    if (status === 'Late' || isLate) return 'Geç teslim';
    return 'Teslim edildi';
  }
  setFeeAgreementModel(model: 1 | 2 | 3 | 4): void { this.feeAgreementModel = model; }
  selectFeeAgreement(agreementId: string): void {
    this.selectedFeeAgreementId = agreementId;
    const agreement = this.financeAccount?.agreements.find(item => item.id === agreementId);
    if (agreement && (agreement.model === 'MonthlyFixed' || agreement.model === 'LessonPackage'))
      this.installmentAmount = agreement.amount;
  }
  paymentFormChanged(): void {
    this.paymentRequestId = crypto.randomUUID();
    this.paymentReceivedAt = new Date().toISOString();
  }
  setPaymentAllocation(installmentId: string, value: number): void {
    if (Number(value) > 0) this.paymentAllocationAmounts[installmentId] = Number(value);
    else delete this.paymentAllocationAmounts[installmentId];
    this.paymentFormChanged();
  }
  paymentAllocationValue(installmentId: string): number { return Number(this.paymentAllocationAmounts[installmentId]) || 0; }
  async createFeeAgreement(): Promise<void> {
    if (!this.selected || !this.financeStudentId || !this.financeCourseId || !this.feeAgreementAmount || this.feeAgreementAmount <= 0) return;
    await this.run(async () => {
      const expiresAt = this.feeAgreementModel === 4 && this.packageExpiresOn
        ? new Date(`${this.packageExpiresOn}T23:59:59.999Z`).toISOString() : null;
      const created = await this.service.createFeeAgreement(this.selectedId, {
        id: crypto.randomUUID(), studentId: this.financeStudentId, courseId: this.financeCourseId,
        model: this.feeAgreementModel, amount: Number(this.feeAgreementAmount),
        billingUnit: this.feeAgreementModel === 3 ? this.feeBillingUnit : null,
        packageSessions: this.feeAgreementModel === 4 ? Number(this.packageSessions) : null,
        packageExpiresAt: expiresAt
      });
      await this.loadFinanceAccount();
      this.selectedFeeAgreementId = created.id;
      this.notice = 'Ücret anlaşması kaydedildi. Taksit ve vade tarihleri ayrıca tanımlanır.';
    });
  }
  async createInstallment(): Promise<void> {
    if (!this.selected || !this.selectedFeeAgreementId || !this.installmentLabel.trim()
      || !this.installmentAmount || this.installmentAmount <= 0 || !this.installmentDueOn) return;
    await this.run(async () => {
      await this.service.createInstallment(this.selectedId, this.selectedFeeAgreementId, {
        id: crypto.randomUUID(), label: this.installmentLabel.trim(), amount: Number(this.installmentAmount), dueOn: this.installmentDueOn
      });
      this.installmentLabel = '';
      this.installmentAmount = null;
      await this.loadFinanceAccount();
      this.notice = 'Borç kaydı eklendi. Vade ve tutar seçtiğiniz şekilde saklandı.';
    });
  }
  async recordPayment(): Promise<void> {
    if (!this.selected || !this.financeAccount || !this.canRecordPayment || !this.financeStudentId) return;
    await this.run(async () => {
      await this.service.recordPayment(this.selectedId, {
        id: this.paymentRequestId, studentId: this.financeStudentId, method: this.paymentMethod,
        amount: Number(this.paymentAmount), receivedAt: this.paymentReceivedAt, note: this.paymentNote.trim() || null,
        allocations: Object.entries(this.paymentAllocationAmounts)
          .filter(([, amount]) => Number(amount) > 0)
          .map(([installmentId, amount]) => ({ installmentId, amount: Number(amount) }))
      });
      this.paymentRequestId = crypto.randomUUID();
      this.paymentReceivedAt = new Date().toISOString();
      this.paymentAmount = null;
      this.paymentNote = '';
      this.paymentAllocationAmounts = {};
      await this.loadFinanceAccount();
      this.notice = 'Manuel tahsilat kaydedildi.';
    });
  }
  async reversePayment(payment: CoursePayment): Promise<void> {
    if (!this.selected || payment.amount <= 0 || this.isPaymentReversed(payment.id)) return;
    await this.run(async () => {
      await this.service.reversePayment(this.selectedId, payment.id, crypto.randomUUID(), new Date().toISOString(), 'Panelden ters kayıt');
      await this.loadFinanceAccount();
      this.notice = 'Tahsilat silinmeden ters kaydı eklendi.';
    });
  }
  async reverseInstallment(installment: CourseInstallment): Promise<void> {
    if (!this.selected || installment.reversalOfInstallmentId || this.isInstallmentReversed(installment.id)) return;
    await this.run(async () => {
      await this.service.reverseInstallment(this.selectedId, installment.id, crypto.randomUUID());
      await this.loadFinanceAccount();
      this.notice = 'Borç silinmeden ters kaydı eklendi.';
    });
  }
  async reversePackageMovement(movement: CoursePackageMovement): Promise<void> {
    if (!this.selected || movement.type !== 'LessonConsumed' || this.isPackageMovementReversed(movement.id)) return;
    await this.run(async () => {
      await this.service.reversePackageMovement(this.selectedId, movement.id, crypto.randomUUID());
      await this.loadFinanceAccount();
      this.notice = 'Paket ders hakkı silinmeden ters kaydı eklendi.';
    });
  }
  isPaymentReversed(paymentId: string): boolean {
    return !!this.financeAccount?.payments.some(payment => payment.reversalOfPaymentId === paymentId);
  }
  isInstallmentReversed(installmentId: string): boolean {
    return !!this.financeAccount?.installments.some(item => item.reversalOfInstallmentId === installmentId);
  }
  isPackageMovementReversed(movementId: string): boolean {
    return !!this.financeAccount?.packageMovements.some(item => item.reversalOfMovementId === movementId);
  }
  packageMovementLabel(movement: CoursePackageMovement): string {
    return movement.type === 'Granted' ? 'Paket tanımlandı' : movement.type === 'LessonConsumed' ? 'Tamamlanan derste kullanıldı' : 'Hak geri yüklendi';
  }
  feeModelLabel(model: string): string {
    return ({ Term: 'Dönemlik / taksitli', MonthlyFixed: 'Aylık sabit', PerLesson: 'Ders veya saat başı', LessonPackage: 'Ders paketi' } as Record<string, string>)[model] ?? model;
  }
  courseDisplayName(courseId: string): string { return this.courses.find(course => course.id === courseId)?.name ?? 'Kurs'; }
  packageRemaining(account: CourseStudentAccount, agreementId: string): number {
    return account.packageBalances.find(item => item.agreementId === agreementId)?.remainingSessions ?? 0;
  }
  money(amount: number): string {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(amount);
  }
  async closeFeeAgreement(agreement: CourseFeeAgreement): Promise<void> {
    if (!this.selected || !agreement.isActive) return;
    await this.run(async () => {
      await this.service.closeFeeAgreement(this.selectedId, agreement.id);
      await this.loadFinanceAccount();
      this.notice = 'Anlaşma kapatıldı; önceki borç ve tahsilatlar korundu.';
    });
  }
  changeWorkspaceName(name: string): void { this.workspaceName = name; this.workspaceRequestId = crypto.randomUUID(); }
  changeTarget(userId: string): void { this.targetUserId = userId; this.invitationRequestId = crypto.randomUUID(); }
  changeInviteRole(role: 1 | 2): void { this.inviteRole = role; this.inviteStudentRecordId = ''; this.invitationRequestId = crypto.randomUUID(); }
  changeInviteStudentRecord(studentRecordId: string): void { this.inviteStudentRecordId = studentRecordId; this.invitationRequestId = crypto.randomUUID(); }
  async createWorkspace(): Promise<void> { await this.run(async () => {
    await this.service.saveProfile(this.displayName.trim());
    const created = await this.service.createWorkspace(this.workspaceRequestId, this.workspaceName.trim(), this.role === 'institution' ? 1 : 2);
    this.selectedId = created.id;
    this.workspaceRequestId = crypto.randomUUID();
    this.workspaceName = '';
    await this.reload();
    this.notice = 'Çalışma alanınız oluşturuldu.';
  }); }
  async addStudent(): Promise<void> { if (!this.selected || !this.studentName.trim()) return; await this.run(async () => {
    await this.service.addStudent(this.selectedId, this.studentName.trim(), this.studentEmail, this.studentPhone);
    this.studentName = ''; this.studentEmail = ''; this.studentPhone = '';
    await this.loadLearningData(); this.notice = 'Öğrenci kaydı oluşturuldu.';
  }); }
  async selectStudentCsv(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.studentCsv = ''; this.studentCsvFileName = ''; this.studentCsvPreview = null;
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.csv') || file.size > 1_000_000) {
      this.error = 'En fazla 1 MB büyüklüğünde bir .csv dosyası seçin.';
      input.value = '';
      this.changeDetector.markForCheck();
      return;
    }
    try {
      this.studentCsv = await file.text();
      this.studentCsvFileName = file.name;
      this.error = '';
    } catch {
      this.error = 'CSV dosyası okunamadı.';
    }
    this.changeDetector.markForCheck();
  }
  async previewStudentCsv(): Promise<void> {
    if (!this.selected || !this.studentCsv) return;
    await this.run(async () => {
      this.studentCsvPreview = await this.service.previewStudentCsv(this.selectedId, this.studentCsv);
      this.notice = 'CSV önizlemesi hazır. Hatalı ve yinelenen satırlar düzeltilmeden aktarım yapılmaz.';
    });
  }
  async commitStudentCsv(): Promise<void> {
    if (!this.selected || !this.studentCsvPreview) return;
    await this.run(async () => {
      const result = await this.service.commitStudentCsv(this.selectedId, this.studentCsvPreview!.previewId, this.studentCsv);
      await this.loadLearningData();
      this.studentCsvPreview = await this.service.previewStudentCsv(this.selectedId, this.studentCsv);
      this.notice = `${result.createdCount} öğrenci aktarıldı; ${result.alreadyImportedCount} kayıt önceki gönderimden zaten mevcut.`;
    });
  }
  editStudent(student: CourseStudent): void {
    if (!this.isOwner) return;
    this.editingStudentId = student.id;
    this.editingStudentName = student.displayName;
    this.editingStudentEmail = student.email ?? '';
    this.editingStudentPhone = student.phoneNumber ?? '';
  }
  async saveStudent(): Promise<void> { if (!this.isOwner || !this.selected || !this.editingStudentId || !this.editingStudentName.trim()) return; await this.run(async () => {
    const student = this.students.find(item => item.id === this.editingStudentId);
    if (!student) return;
    await this.service.updateStudent(this.selectedId, student.id, this.editingStudentName.trim(), this.editingStudentEmail, this.editingStudentPhone, student.isActive);
    this.editingStudentId = '';
    await this.loadLearningData(); this.notice = 'Öğrenci bilgileri güncellendi.';
  }); }
  async setStudentActive(student: CourseStudent, isActive: boolean): Promise<void> { if (!this.isOwner || !this.selected) return; await this.run(async () => {
    await this.service.updateStudent(this.selectedId, student.id, student.displayName, student.email ?? '', student.phoneNumber ?? '', isActive);
    if (this.editingStudentId === student.id) this.editingStudentId = '';
    await this.loadLearningData();
    this.notice = isActive ? 'Öğrenci kaydı etkinleştirildi. Eski grup kayıtları yeniden etkinleştirilmedi.' : 'Öğrenci arşivlendi; aktif grup kayıtları ve bu çalışma alanındaki öğrenci erişimi kapatıldı.';
  }); }
  async createCourse(): Promise<void> { if (!this.selected || !this.courseName.trim() || !this.courseSubject.trim()) return; await this.run(async () => {
    const course = await this.service.createCourse(this.selectedId, this.courseName.trim(), this.courseSubject.trim());
    this.courseName = ''; this.courseSubject = ''; await this.loadLearningData(); this.selectedCourseId = course.id;
    this.notice = 'Kurs oluşturuldu.';
  }); }
  async createGroup(): Promise<void> { if (!this.selected || !this.selectedCourseId || !this.groupName.trim()) return; await this.run(async () => {
    const teacherUserId = this.isOwner ? this.selectedTeacherId || this.auth.getCurrentUserId() || '' : this.auth.getCurrentUserId() || '';
    await this.service.createGroup(this.selectedId, this.selectedCourseId, teacherUserId, this.groupName.trim());
    this.groupName = ''; await this.loadLearningData(); this.notice = 'Grup oluşturuldu ve öğretmen atandı.';
  }); }
  async createBranch(): Promise<void> { if (!this.selected || !this.branchName.trim()) return; await this.run(async () => {
    const branch = await this.service.createBranch(this.selectedId, this.branchName.trim());
    this.branchName = ''; await this.loadLearningData(); this.roomBranchId = branch.id;
    this.notice = 'Şube oluşturuldu.';
  }); }
  async createRoom(): Promise<void> { if (!this.selected || !this.roomBranchId || !this.roomName.trim()) return; await this.run(async () => {
    await this.service.createRoom(this.selectedId, this.roomBranchId, this.roomName.trim(), this.roomCapacity);
    this.roomName = ''; this.roomCapacity = null; await this.loadLearningData();
    this.notice = 'Derslik oluşturuldu.';
  }); }
  async enrollStudent(): Promise<void> { if (!this.selected || !this.selectedGroupId || !this.selectedStudentId) return; await this.run(async () => {
    await this.service.enrollStudent(this.selectedId, this.selectedGroupId, this.selectedStudentId);
    this.notice = 'Öğrenci gruba kaydedildi.';
  }); }
  async createLesson(): Promise<void> {
    if (!this.selected) return;
    const group = this.lessonMode === 'group' ? this.groups.find(item => item.id === this.lessonGroupId) : undefined;
    const student = this.lessonMode === 'private' ? this.students.find(item => item.id === this.lessonStudentId && item.isActive) : undefined;
    const courseId = group?.courseId ?? this.lessonCourseId;
    const teacherUserId = group?.teacherUserId ?? (this.isOwner ? this.lessonTeacherId : this.auth.getCurrentUserId()) ?? '';
    if (!courseId || !teacherUserId || (this.lessonMode === 'group' && !group) || (this.lessonMode === 'private' && !student)
      || (this.lessonSchedule === 'once' && (!this.lessonStartsAt || !this.lessonEndsAt))
      || (this.lessonSchedule === 'weekly' && (!this.lessonSeriesStartsOn || !this.lessonSeriesEndsOn
        || !this.lessonSeriesStartTime || !this.lessonSeriesTimeZoneId.trim() || !this.lessonDaysOfWeekMask))) return;
    await this.run(async () => {
      if (this.lessonSchedule === 'weekly') {
        const result = await this.service.createLessonSeries(this.selectedId, courseId, group?.id ?? null, student?.id ?? null,
          teacherUserId, this.lessonSeriesTimeZoneId.trim(), this.lessonSeriesStartsOn, this.lessonSeriesEndsOn,
          this.lessonSeriesStartTime, this.lessonSeriesDurationMinutes, this.lessonDaysOfWeekMask, this.lessonRoomId || null);
        this.notice = `${result.sessionsCreated} derslik haftalık seri takvime eklendi.`;
      } else {
        await this.service.createLesson(this.selectedId, courseId, group?.id ?? null, student?.id ?? null,
          teacherUserId, new Date(this.lessonStartsAt).toISOString(), new Date(this.lessonEndsAt).toISOString(), this.lessonRoomId || null);
        this.notice = 'Ders takvime eklendi.';
      }
      this.resetLessonTimes();
      await this.loadLessons();
    });
  }
  async cancelLesson(lessonId: string): Promise<void> { if (!this.selected) return; await this.run(async () => {
    await this.service.cancelLesson(this.selectedId, lessonId);
    await Promise.all([this.loadLessons(), this.loadMakeupCredits()]);
    this.notice = 'Ders iptal edildi; etkin öğrenciler için telafi kredileri açıldı.';
  }); }
  canRescheduleLesson(lesson: CourseLesson): boolean {
    const userId = this.auth.getCurrentUserId();
    return lesson.status === 'Scheduled' && lesson.seriesId === null && new Date(lesson.startsAt).getTime() > Date.now()
      && lesson.students.length > 0 && lesson.students.every(student => student.attendanceStatus === null)
      && (this.isOwner || lesson.teacherUserId === userId);
  }
  beginLessonReschedule(lesson: CourseLesson): void {
    if (!this.canRescheduleLesson(lesson)) return;
    this.editingLessonId = lesson.id;
    this.editingLessonStartsAt = this.dateTimeLocal(new Date(lesson.startsAt));
    this.editingLessonEndsAt = this.dateTimeLocal(new Date(lesson.endsAt));
    this.editingLessonRoomId = lesson.roomId ?? '';
  }
  canSaveLessonReschedule(): boolean {
    const startsAt = Date.parse(this.editingLessonStartsAt);
    const endsAt = Date.parse(this.editingLessonEndsAt);
    return !!this.editingLessonId && !!this.editingLessonStartsAt && !!this.editingLessonEndsAt
      && startsAt > Date.now() && endsAt > startsAt;
  }
  async saveLessonReschedule(): Promise<void> {
    if (!this.selected || !this.canSaveLessonReschedule()) return;
    await this.run(async () => {
      await this.service.rescheduleLesson(this.selectedId, this.editingLessonId,
        new Date(this.editingLessonStartsAt).toISOString(), new Date(this.editingLessonEndsAt).toISOString(),
        this.editingLessonRoomId || null);
      this.editingLessonId = '';
      await this.loadLessons();
      this.notice = 'Tek dersin zamanı güncellendi.';
    });
  }
  async cancelFutureSeries(seriesId: string): Promise<void> {
    if (!this.selected) return;
    const series = this.lessonSeries.find(item => item.id === seriesId);
    if (!series || !window.confirm(`${series.remainingSessions} gelecek ders iptal edilecek. Devam edilsin mi?`)) return;
    await this.run(async () => {
      const result = await this.service.cancelFutureLessonSeries(this.selectedId, seriesId);
      [this.lessons, this.lessonSeries] = await Promise.all([
        this.service.getLessons(this.selectedId), this.service.getLessonSeries(this.selectedId)
      ]);
      this.notice = result.sessionsCancelled
        ? `${result.sessionsCancelled} gelecek ders iptal edildi.`
        : 'İptal edilecek planlı gelecek ders kalmamış.';
    });
  }
  editLessonSeries(series: CourseLessonSeries): void {
    this.editingSeriesId = series.id;
    this.editingSeriesEndsOn = series.endsOn;
    this.editingSeriesStartTime = series.startTime.slice(0, 5);
    this.editingSeriesDurationMinutes = series.durationMinutes;
    this.editingSeriesDaysOfWeek = this.weekdays.map(weekday => (series.daysOfWeekMask & (1 << weekday.index)) !== 0);
    this.editingSeriesRoomId = series.roomId ?? '';
  }
  async saveLessonSeriesSchedule(): Promise<void> {
    if (!this.selected || !this.editingSeriesId || !this.editingSeriesEndsOn || !this.editingSeriesStartTime || !this.editingSeriesDaysOfWeekMask) return;
    await this.run(async () => {
      const result = await this.service.updateLessonSeriesSchedule(this.selectedId, this.editingSeriesId,
        this.editingSeriesEndsOn, this.editingSeriesStartTime, this.editingSeriesDurationMinutes,
        this.editingSeriesDaysOfWeekMask, this.editingSeriesRoomId || null);
      this.editingSeriesId = '';
      [this.lessons, this.lessonSeries] = await Promise.all([
        this.service.getLessons(this.selectedId), this.service.getLessonSeries(this.selectedId)
      ]);
      this.notice = `Seri güncellendi: ${result.sessionsUpdated} oturum güncellendi, ${result.sessionsCreated} yeni oturum eklendi, ${result.sessionsCancelled} eski oturum iptal edildi.`;
    });
  }
  get editingSeriesDaysOfWeekMask(): number {
    return this.editingSeriesDaysOfWeek.reduce((mask, selected, index) => selected ? mask | (1 << index) : mask, 0);
  }
  async markAttendance(lessonId: string, studentId: string, status: string): Promise<void> {
    if (!this.selected) return;
    const statusValue = ({ Present: 1, Absent: 2, Excused: 3, Late: 4 } as Record<string, number>)[status];
    if (!statusValue) return;
    await this.run(async () => {
      await this.service.markAttendance(this.selectedId, lessonId, studentId, statusValue);
      await this.loadLessons();
      this.notice = 'Yoklama kaydedildi.';
    });
  }
  async completeLesson(lessonId: string): Promise<void> { if (!this.selected) return; await this.run(async () => {
    await this.service.completeLesson(this.selectedId, lessonId);
    await Promise.all([this.loadLessons(), this.loadMakeupCredits()]);
    this.notice = 'Ders tamamlandı ve kapatıldı.';
  }); }
  async approveMakeupCredit(creditId: string): Promise<void> { if (!this.selected) return; await this.run(async () => {
    await this.service.approveMakeupCredit(this.selectedId, creditId);
    await this.loadMakeupCredits();
    this.notice = 'Telafi kredisi onaylandı. 30 gün içinde ders planlayın.';
  }); }
  canScheduleMakeupCredit(credit: CourseMakeupCredit): boolean {
    const startsAt = Date.parse(this.makeupStartsAt[credit.id] ?? '');
    const endsAt = startsAt + credit.durationMinutes * 60000;
    return credit.status === 'Available' && startsAt > Date.now() && endsAt > startsAt && endsAt <= Date.parse(credit.expiresAt);
  }
  async scheduleMakeupCredit(credit: CourseMakeupCredit): Promise<void> {
    if (!this.selected || !this.canScheduleMakeupCredit(credit)) return;
    await this.run(async () => {
      const startsAt = new Date(this.makeupStartsAt[credit.id]);
      const endsAt = new Date(startsAt.getTime() + credit.durationMinutes * 60000);
      await this.service.createLesson(this.selectedId, credit.sourceCourseId, null, credit.studentId,
        credit.sourceTeacherUserId, startsAt.toISOString(), endsAt.toISOString(), null, credit.id);
      await Promise.all([this.loadLessons(), this.loadMakeupCredits()]);
      this.notice = 'Telafi dersi planlandı ve kredi bu derse bağlandı.';
    });
  }
  async addTeacherTimeOff(): Promise<void> {
    if (!this.selected || !this.timeOffStartsAt || !this.timeOffEndsAt) return;
    const teacherUserId = this.isOwner ? this.timeOffTeacherUserId : this.auth.getCurrentUserId() ?? '';
    if (!teacherUserId) return;
    await this.run(async () => {
      await this.service.createTeacherTimeOff(this.selectedId, teacherUserId,
        new Date(this.timeOffStartsAt).toISOString(), new Date(this.timeOffEndsAt).toISOString(), this.timeOffReason);
      this.timeOffReason = '';
      this.resetTimeOffTimes();
      await this.loadTeacherTimeOff();
      this.notice = 'Öğretmen izin aralığı kaydedildi.';
    });
  }
  async removeTeacherTimeOff(blockId: string): Promise<void> { if (!this.selected) return; await this.run(async () => {
    await this.service.deleteTeacherTimeOff(this.selectedId, blockId);
    await this.loadTeacherTimeOff();
    this.notice = 'Öğretmen izin aralığı kaldırıldı.';
  }); }
  async addTeacherAvailability(): Promise<void> {
    if (!this.selected || !this.availabilityStartTime || !this.availabilityEndTime
      || this.availabilityEndTime <= this.availabilityStartTime) return;
    const teacherUserId = this.isOwner ? this.availabilityTeacherUserId : this.auth.getCurrentUserId() ?? '';
    if (!teacherUserId) return;
    await this.run(async () => {
      await this.service.createTeacherAvailability(this.selectedId, teacherUserId, this.availabilityWeekday,
        this.availabilityStartTime, this.availabilityEndTime, this.availabilityTimeZoneId);
      await this.loadTeacherAvailability();
      this.notice = 'Haftalık çalışma saati eklendi. Bu kural yalnızca yeni ders planlarını denetler.';
    });
  }
  async removeTeacherAvailability(windowId: string): Promise<void> { if (!this.selected) return; await this.run(async () => {
    await this.service.deleteTeacherAvailability(this.selectedId, windowId);
    await this.loadTeacherAvailability();
    this.notice = 'Haftalık çalışma saati kaldırıldı.';
  }); }
  weekdayLabel(weekday: number): string { return this.weekdays[weekday]?.label ?? ''; }
  lessonTime(value: string, timeZoneId: string | null): string {
    return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short', timeZone: timeZoneId ?? undefined }).format(new Date(value));
  }
  lessonStatus(status: string): string { return status === 'Scheduled' ? 'Planlandı' : status === 'Cancelled' ? 'İptal edildi' : 'Tamamlandı'; }
  attendanceLabel(status: string | null, isActive = true): string {
    if (!isActive) return 'Katılım iptal edildi';
    return ({ Present: 'Katıldı', Absent: 'Katılmadı', Excused: 'Mazeretli', Late: 'Geç katıldı' } as Record<string, string>)[status ?? ''] ?? 'Yoklama bekliyor';
  }
  canTakeAttendance(lesson: CourseLesson): boolean {
    return lesson.status === 'Scheduled' && this.activeLessonStudents(lesson).length > 0 && new Date(lesson.startsAt).getTime() <= Date.now();
  }
  canCancelLesson(lesson: CourseLesson): boolean {
    return lesson.status === 'Scheduled' && new Date(lesson.startsAt).getTime() > Date.now()
      && this.activeLessonStudents(lesson).every(student => student.attendanceStatus === null);
  }
  canCompleteLesson(lesson: CourseLesson): boolean {
    return lesson.status === 'Scheduled' && new Date(lesson.endsAt).getTime() <= Date.now()
      && this.activeLessonStudents(lesson).length > 0 && this.activeLessonStudents(lesson).every(student => student.attendanceStatus !== null);
  }
  private activeLessonStudents(lesson: CourseLesson) { return lesson.students.filter(student => student.isActive !== false); }
  makeupCreditStatus(status: string): string {
    return ({ PendingTeacherApproval: 'Öğretmen onayı bekliyor', Available: 'Kullanılabilir', Scheduled: 'Planlandı',
      Consumed: 'Kullanıldı', Expired: 'Süresi doldu' } as Record<string, string>)[status] ?? status;
  }
  get lessonDaysOfWeekMask(): number {
    return this.lessonDaysOfWeek.reduce((mask, selected, index) => selected ? mask | (1 << index) : mask, 0);
  }
  async selectGuardianStudent(id: string): Promise<void> {
    this.guardianStudentId = id;
    await this.run(() => this.loadGuardianLinks());
  }
  changeGuardianSelection(id: string): void { this.selectedGuardianId = id; }
  async linkGuardian(): Promise<void> { if (!this.isOwner || !this.selected || !this.guardianStudentId) return; await this.run(async () => {
    const contact = this.selectedGuardianId ? undefined : {
      displayName: this.guardianName.trim(), email: this.guardianEmail, phoneNumber: this.guardianPhone
    };
    if (contact && !contact.displayName) return;
    await this.service.linkGuardian(this.selectedId, this.guardianStudentId,
      this.selectedGuardianId || this.guardianRequestId, this.guardianRelationship, contact);
    if (!this.selectedGuardianId) {
      this.guardianRequestId = crypto.randomUUID(); this.guardianName = ''; this.guardianEmail = ''; this.guardianPhone = '';
    }
    this.selectedGuardianId = '';
    await this.loadLearningData();
    this.notice = 'Veli iletişim bilgisi öğrenciye bağlandı. Bu kayıt portal erişimi veya ödeme yetkisi vermez.';
  }); }
  async unlinkGuardian(guardianId: string): Promise<void> { if (!this.isOwner || !this.selected || !this.guardianStudentId) return; await this.run(async () => {
    await this.service.unlinkGuardian(this.selectedId, this.guardianStudentId, guardianId);
    await this.loadGuardianLinks(); this.notice = 'Veli bağlantısı kaldırıldı.';
  }); }
  editGuardian(guardian: GuardianContact): void {
    if (!this.isOwner) return;
    this.editingGuardianId = guardian.id;
    this.editingGuardianName = guardian.displayName;
    this.editingGuardianEmail = guardian.email ?? '';
    this.editingGuardianPhone = guardian.phoneNumber ?? '';
  }
  async saveGuardian(): Promise<void> { if (!this.isOwner || !this.selected || !this.editingGuardianId || !this.editingGuardianName.trim()) return; await this.run(async () => {
    await this.service.updateGuardian(this.selectedId, this.editingGuardianId, this.editingGuardianName.trim(), this.editingGuardianEmail, this.editingGuardianPhone);
    this.editingGuardianId = '';
    await this.loadLearningData(); this.notice = 'Veli iletişim bilgisi güncellendi.';
  }); }
  guardianRelationshipLabel(value: string): string {
    return ({ Parent: 'Veli', Mother: 'Anne', Father: 'Baba', LegalGuardian: 'Yasal vasi', Other: 'Diğer' } as Record<string, string>)[value] ?? 'Diğer';
  }
  async selectWorkspace(id: string): Promise<void> {
    this.selectedId = id;
    this.legacySourcePage = null;
    this.legacySourcePageNumber = 1;
    this.legacySelectedRows.clear();
    this.legacyImportPreview = null;
    this.legacyTeacherInstitutionPage = null;
    this.legacyTeacherInstitutionId = '';
    this.legacyTeacherSourcePage = null;
    this.legacyTeacherSourcePageNumber = 1;
    this.legacySelectedTeachers.clear();
    this.legacyInstitutionSourcePage = null;
    this.legacyInstitutionSourceId = '';
    this.legacyInstitutionInfo = null;
    this.legacyInstitutionPreview = null;
    this.legacyInstitutionIncludeName = false;
    this.legacyInstitutionIncludeType = false;
    this.auditEntries = [];
    this.editingSeriesId = '';
    this.editingLessonId = '';
    this.guardianStudentId = ''; this.selectedGuardianId = ''; this.studentGuardianLinks = [];
    this.editingStudentId = ''; this.editingGuardianId = '';
    this.guardianRequestId = crypto.randomUUID();
    this.inviteStudentRecordId = '';
    this.selectedAssignmentId = ''; this.assignmentSubmissions = []; this.assignmentMaterials = [];
    await this.run(() => this.loadOwnerData());
  }
  async invite(): Promise<void> {
    if (!this.isOwner) return;
    await this.run(async () => {
      await this.service.invite(this.selectedId, this.invitationRequestId, this.targetUserId.trim(), this.inviteRole,
        this.inviteRole === 1 ? this.inviteStudentRecordId || null : null);
      this.invitationRequestId = crypto.randomUUID();
      this.targetUserId = '';
      this.inviteStudentRecordId = '';
      await this.loadOwnerData();
      this.notice = 'Davet oluşturuldu. Davetli kendi hesabından kabul edebilir.';
    });
  }
  async accept(id: string): Promise<void> { await this.run(async () => {
    const invitation = this.pending.find(item => item.id === id);
    if (invitation?.role !== 'Teacher') {
      throw new Error('Öğrenci davetlerini öğrenci alanından kabul edebilirsiniz.');
    }
    await this.auth.joinCourseManagement('Teacher');
    await this.service.accept(id, this.displayName.trim());
    await this.reload();
    this.notice = 'Davet kabul edildi.';
  }); }
  async cancel(id: string): Promise<void> { await this.run(async () => { await this.service.cancel(this.selectedId, id); await this.loadOwnerData(); }); }
  async revoke(userId: string): Promise<void> { await this.run(async () => { await this.service.revoke(this.selectedId, userId); await this.loadOwnerData(); this.notice = 'Bu çalışma alanındaki üyelik kapatıldı.'; }); }
  private async reload(): Promise<void> {
    [this.workspaces, this.pending, this.ownerSubscriptions] = await Promise.all([
      this.service.getWorkspaces(), this.service.getPendingInvitations(), this.service.getMySubscriptions()
    ]);
    if (!this.workspaces.some(item => item.id === this.selectedId)) this.selectedId = this.workspaces[0]?.id ?? '';
    await this.loadOwnerData();
  }
  private async loadOwnerData(): Promise<void> {
    const workspaceId = this.selectedId;
    this.members = []; this.invitations = []; this.auditEntries = [];
    if (this.isOwner && workspaceId) {
      const [members, invitations, auditEntries] = await Promise.all([
        this.service.getMembers(workspaceId), this.service.getInvitations(workspaceId), this.service.getAudit(workspaceId)
      ]);
      if (this.selectedId === workspaceId && this.isOwner) {
        this.members = members; this.invitations = invitations; this.auditEntries = auditEntries;
      }
    }
    await this.loadLearningData();
  }
  private async loadLearningData(): Promise<void> {
    if (!this.selectedId || !this.canManageLearningData) {
      this.students = []; this.courses = []; this.groups = []; this.branches = []; this.rooms = []; this.guardians = []; this.studentGuardianLinks = []; this.lessons = []; this.lessonSeries = []; this.makeupCredits = []; this.teacherTimeOff = []; this.teacherAvailability = []; this.assignments = []; this.assignmentSubmissions = []; this.assignmentMaterials = []; this.financeAccount = null; return;
    }
    [this.students, this.courses, this.groups, this.branches, this.rooms, this.guardians, this.lessons, this.lessonSeries, this.makeupCredits, this.teacherTimeOff, this.teacherAvailability, this.assignments] = await Promise.all([
      this.service.getStudents(this.selectedId), this.service.getCourses(this.selectedId), this.service.getGroups(this.selectedId),
      this.service.getBranches(this.selectedId), this.service.getRooms(this.selectedId), this.service.getGuardians(this.selectedId), this.service.getLessons(this.selectedId),
      this.service.getLessonSeries(this.selectedId), this.service.getMakeupCredits(this.selectedId), this.service.getTeacherTimeOff(this.selectedId),
      this.service.getTeacherAvailability(this.selectedId), this.service.getAssignments(this.selectedId)
    ]);
    if (!this.courses.some(course => course.id === this.selectedCourseId)) this.selectedCourseId = this.courses[0]?.id ?? '';
    if (!this.courses.some(course => course.id === this.lessonCourseId)) this.lessonCourseId = this.courses[0]?.id ?? '';
    if (!this.groups.some(group => group.id === this.selectedGroupId)) this.selectedGroupId = this.groups[0]?.id ?? '';
    if (!this.groups.some(group => group.id === this.lessonGroupId)) this.lessonGroupId = this.groups[0]?.id ?? '';
    if (!this.branches.some(branch => branch.id === this.roomBranchId)) this.roomBranchId = this.branches[0]?.id ?? '';
    if (!this.rooms.some(room => room.id === this.lessonRoomId)) this.lessonRoomId = '';
    if (!this.students.some(student => student.id === this.lessonStudentId && student.isActive))
      this.lessonStudentId = this.students.find(student => student.isActive)?.id ?? '';
    if (!this.members.some(member => member.userId === this.selectedTeacherId && member.role === 'Teacher' && member.isActive))
      this.selectedTeacherId = this.members.find(member => member.role === 'Teacher' && member.isActive)?.userId ?? '';
    if (!this.members.some(member => member.userId === this.lessonTeacherId && member.role === 'Teacher' && member.isActive))
      this.lessonTeacherId = this.members.find(member => member.role === 'Teacher' && member.isActive)?.userId ?? this.auth.getCurrentUserId() ?? '';
    if (!this.students.some(student => student.id === this.guardianStudentId && student.isActive))
      this.guardianStudentId = this.students.find(student => student.isActive)?.id ?? '';
    if (!this.students.some(student => student.id === this.financeStudentId && student.isActive))
      this.financeStudentId = this.students.find(student => student.isActive)?.id ?? '';
    if (!this.courses.some(course => course.id === this.financeCourseId)) this.financeCourseId = this.courses[0]?.id ?? '';
    if (!this.courses.some(course => course.id === this.assignmentCourseId)) this.assignmentCourseId = this.courses[0]?.id ?? '';
    if (!this.assignmentGroups.some(group => group.id === this.assignmentGroupId)) this.assignmentGroupId = this.assignmentGroups[0]?.id ?? '';
    if (!this.students.some(student => student.id === this.assignmentStudentId && student.isActive))
      this.assignmentStudentId = this.students.find(student => student.isActive)?.id ?? '';
    if (!this.assignments.some(assignment => assignment.id === this.selectedAssignmentId)) {
      this.selectedAssignmentId = '';
      this.assignmentSubmissions = [];
    } else await this.loadAssignmentSubmissions();
    const currentUserId = this.auth.getCurrentUserId() ?? '';
    if (!this.isOwner || (this.availabilityTeacherUserId !== currentUserId
      && !this.members.some(member => member.userId === this.availabilityTeacherUserId && member.role === 'Teacher' && member.isActive)))
      this.availabilityTeacherUserId = currentUserId;
    await Promise.all([this.loadGuardianLinks(), this.loadFinanceAccount()]);
  }
  private async loadGuardianLinks(): Promise<void> {
    this.studentGuardianLinks = this.selectedId && this.guardianStudentId
      ? await this.service.getStudentGuardians(this.selectedId, this.guardianStudentId) : [];
  }
  private async loadLessons(): Promise<void> { this.lessons = this.selectedId ? await this.service.getLessons(this.selectedId) : []; }
  private async loadFinanceAccount(): Promise<void> {
    if (!this.isOwner || !this.selectedId) {
      this.financeSummary = null;
      this.financeAccount = null;
      return;
    }
    const [summary, account] = await Promise.all([
      this.service.getFinanceSummary(this.selectedId),
      this.financeStudentId ? this.service.getStudentAccount(this.selectedId, this.financeStudentId) : Promise.resolve(null)
    ]);
    this.financeSummary = summary;
    this.financeAccount = account;
    if (!this.financeAccount?.agreements.some(item => item.id === this.selectedFeeAgreementId && item.isActive))
      this.selectedFeeAgreementId = this.financeAccount?.agreements.find(item => item.isActive)?.id ?? '';
  }
  private async loadAssignmentSubmissions(): Promise<void> {
    if (this.selectedId && this.selectedAssignmentId) {
      [this.assignmentSubmissions, this.assignmentMaterials] = await Promise.all([
        this.service.getAssignmentSubmissions(this.selectedId, this.selectedAssignmentId),
        this.service.getAssignmentAttachments(this.selectedId, this.selectedAssignmentId)
      ]);
      this.assignmentMaterials = this.assignmentMaterials.filter(item => item.purpose === 'TeacherMaterial');
    } else {
      this.assignmentSubmissions = [];
      this.assignmentMaterials = [];
    }
    for (const submission of this.assignmentSubmissions) {
      this.assignmentScores[submission.studentId] = submission.score;
      this.assignmentFeedback[submission.studentId] = submission.feedback ?? '';
    }
  }
  private async loadMakeupCredits(): Promise<void> { this.makeupCredits = this.selectedId ? await this.service.getMakeupCredits(this.selectedId) : []; }
  private async loadTeacherTimeOff(): Promise<void> { this.teacherTimeOff = this.selectedId ? await this.service.getTeacherTimeOff(this.selectedId) : []; }
  private async loadTeacherAvailability(): Promise<void> { this.teacherAvailability = this.selectedId ? await this.service.getTeacherAvailability(this.selectedId) : []; }
  private resetLessonTimes(): void {
    const start = new Date(); start.setMinutes(0, 0, 0); start.setHours(start.getHours() + 1);
    const end = new Date(start.getTime() + 60 * 60 * 1000);
    this.lessonStartsAt = this.dateTimeLocal(start); this.lessonEndsAt = this.dateTimeLocal(end);
    this.lessonSeriesStartsOn = this.dateLocal(start);
    const seriesEnd = new Date(start); seriesEnd.setDate(seriesEnd.getDate() + 30);
    this.lessonSeriesEndsOn = this.dateLocal(seriesEnd);
    this.lessonSeriesStartTime = `${String(start.getHours()).padStart(2, '0')}:${String(start.getMinutes()).padStart(2, '0')}`;
    this.lessonSeriesDurationMinutes = 60;
    this.lessonDaysOfWeek = [false, false, false, false, false, false, false];
    this.lessonDaysOfWeek[(start.getDay() + 6) % 7] = true;
  }
  private resetTimeOffTimes(): void {
    const start = new Date(); start.setHours(9, 0, 0, 0); start.setDate(start.getDate() + 1);
    const end = new Date(start.getTime() + 60 * 60 * 1000);
    this.timeOffStartsAt = this.dateTimeLocal(start); this.timeOffEndsAt = this.dateTimeLocal(end);
  }
  private resetFinanceDates(): void {
    const due = new Date(); due.setDate(due.getDate() + 30);
    this.installmentDueOn = this.dateLocal(due);
    const expiry = new Date(); expiry.setDate(expiry.getDate() + 365);
    this.packageExpiresOn = this.dateLocal(expiry);
  }
  private dateLocal(value: Date): string {
    const pad = (part: number) => String(part).padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
  }
  private dateTimeLocal(value: Date): string {
    const pad = (part: number) => String(part).padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
  }
  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy) return;
    this.busy = true; this.error = ''; this.notice = '';
    try { await action(); }
    catch (error) {
      const code = error instanceof HttpErrorResponse ? (error.error as { code?: string } | null)?.code : undefined;
      this.error = code === 'Workspace.ErasureReadOnly' ? 'Bu çalışma alanı hesap silme nedeniyle salt okunur.'
        : code === 'Subscription.ReadOnly' ? 'Bu çalışma alanı salt okunur. Planı yalnız SystemAdmin yeniden etkinleştirebilir.'
        : code === 'Invitation.StudentUnavailable' ? 'Seçilen öğrenci kaydı artık uygun değil. Öğrenci listesini yenileyip yeniden seçin.'
        : code === 'StudentImport.InvalidHeader' ? 'CSV başlıkları displayName, email ve phoneNumber alanlarından oluşmalıdır.'
        : code === 'StudentImport.FileTooLarge' ? 'CSV dosyası 1 MB sınırını aşıyor.'
        : code === 'StudentImport.TooManyRows' ? 'CSV dosyası en fazla 500 öğrenci satırı içerebilir.'
        : code === 'StudentImport.ResolveRows' ? 'Hatalı veya yinelenen satırlar var. Dosyayı düzeltip yeniden önizleyin.'
        : code === 'StudentImport.PreviewChanged' ? 'Önizleme ile dosya içeriği eşleşmiyor. Dosyayı yeniden önizleyin.'
        : code === 'LegacyStudentImport.ResolveRows' ? 'Aktarımda hatalı, çakışan veya yinelenen kayıt var. Seçimi düzeltip yeniden önizleyin.'
        : code === 'LegacyStudentImport.PreviewChanged' ? 'Kaynak kayıtlar önizlemeden sonra değişti. Öğrencileri yeniden yükleyip önizleyin.'
        : code === 'LegacyInstitutionImport.PreviewChanged' ? 'Çalışma alanı veya kaynak kurum bilgisi önizlemeden sonra değişti. Bilgileri yeniden yükleyip önizleyin.'
        : code === 'LegacyInstitutionImport.InstitutionWorkspaceRequired' ? 'Kurum bilgisi yalnızca kurum çalışma alanına aktarılabilir.'
        : code === 'LegacyInstitutionImport.InvalidFields' || code === 'LegacyInstitutionImport.UnselectedFieldProvided'
        || code === 'LegacyInstitutionImport.InvalidName' || code === 'LegacyInstitutionImport.InvalidType'
        ? 'Kurum aktarımı için seçilen alanları ve önizleme bilgilerini kontrol edin.'
        : error instanceof HttpErrorResponse && error.status === 403 ? 'Bu kaynak ürün için kurum yöneticisi erişiminiz bulunmuyor.'
        : code === 'Lesson.TeacherConflict' ? 'Öğretmenin bu saatte başka bir dersi var.'
        : code === 'Lesson.StudentConflict' ? 'Seçilen öğrencinin bu saatte başka bir dersi var.'
        : code === 'Lesson.InvalidTime' ? 'Ders gelecekte başlamalı, bitişi başlangıçtan sonra olmalı ve süre 8 saati aşmamalı.'
        : code === 'Lesson.AttendanceStarted' ? 'Başlamış veya yoklaması girilmiş dersin saati değiştirilemez.'
        : code === 'Lesson.SeriesOccurrenceMustUseSeriesEditor' ? 'Haftalık seri oturumlarını seri planından düzenleyin.'
        : code === 'Lesson.NoParticipants' ? 'Katılımcısı olmayan dersin saati değiştirilemez.'
        : code === 'Availability.LessonConflict' ? 'Bu izin aralığında öğretmenin planlı dersi var. Önce dersi ayrı olarak iptal edin.'
        : code === 'Availability.TimeOffConflict' || code === 'Availability.BlockConflict' ? 'Öğretmen izin aralıkları çakışamaz.'
        : code === 'Availability.TeacherNotInWorkspace' ? 'Seçilen öğretmen bu çalışma alanında etkin değil.'
        : code === 'Availability.InvalidTime' ? 'İzin gelecekte başlamalı, bitişi başlangıçtan sonra olmalı ve süre 90 günü aşmamalı.'
        : code === 'Availability.OutsideWorkingHours' ? 'Ders, öğretmenin seçili haftalık çalışma saatlerinin içinde kalmıyor.'
        : code === 'Availability.InvalidWindow' ? 'Haftanın gününü ve başlangıç/bitiş saatlerini kontrol edin.'
        : code === 'Availability.InvalidTimeZone' ? 'IANA saat dilimini kontrol edin; örnek: Europe/Istanbul.'
        : code === 'Availability.DuplicateWindow' ? 'Bu çalışma saati zaten kayıtlı.'
        : code === 'Lesson.GroupHasNoActiveStudents' ? 'Bu grupta etkin öğrenci bulunmuyor.'
        : code === 'Room.Conflict' ? 'Seçilen derslik bu saatlerde başka bir ders için kullanılıyor.'
        : code === 'Room.CapacityExceeded' ? 'Seçilen dersliğin kapasitesi bu gruptaki öğrenci sayısından düşük.'
        : code === 'Room.DuplicateName' || code === 'Branch.DuplicateName' ? 'Bu ad aynı şube veya çalışma alanında zaten kullanılıyor.'
        : code === 'Room.InvalidInput' ? 'Derslik adını ve kapasitesini kontrol edin.'
        : code === 'Branch.InvalidInput' ? 'Şube adını kontrol edin.'
        : code === 'LessonSeries.InvalidTimeZone' ? 'IANA saat dilimini kontrol edin; örnek: Europe/Istanbul.'
        : code === 'LessonSeries.InvalidLocalTime' ? 'Seride yaz saati nedeniyle oluşmayan veya iki kez oluşan bir saat var. Başka saat seçin.'
        : code === 'LessonSeries.NoFutureOccurrences' ? 'Seçilen tarih aralığında gelecekte ders oluşmuyor.'
        : code === 'LessonSeries.NoFutureSessions' ? 'Düzenlenecek gelecek planlı oturum kalmamış; yeni bir seri oluşturun.'
        : code === 'LessonSeries.NoParticipants' ? 'Serinin katılımcı kaydı bulunamadı; yeni bir seri oluşturun.'
        : code === 'LessonSeries.AttendanceStarted' ? 'Seride yoklaması başlamış bir gelecek oturumu var; seri düzenlenemiyor.'
        : code === 'LessonSeries.InvalidSchedule' ? 'Bitiş tarihini, başlangıç saatini, günleri ve ders süresini kontrol edin.'
        : code === 'LessonSeries.InvalidDateRange' ? 'Seri başlangıç tarihini bugünden önce seçemezsiniz.'
        : code === 'LessonSeries.InvalidInput' ? 'Seri tarihlerini, günlerini ve süresini kontrol edin.'
        : code === 'Attendance.LessonNotStarted' ? 'Yoklama dersin başlangıç saatinden önce girilemez.'
        : code === 'Attendance.LessonClosed' ? 'İptal edilmiş veya tamamlanmış dersin yoklaması değiştirilemez.'
        : code === 'Lesson.NotEnded' ? 'Ders, bitiş saatinden sonra tamamlanabilir.'
        : code === 'Lesson.AttendanceIncomplete' ? 'Dersi tamamlamak için tüm katılımcılara yoklama girin.'
        : code === 'FeeAgreement.ActiveAgreementExists' ? 'Bu öğrenci ve kurs için etkin bir ücret anlaşması var. Önce mevcut anlaşmayı kapatın.'
        : code === 'FeeAgreement.InstallmentsDoNotMatchAmount' ? 'Anlaşmayı kapatmak için taksitlerin toplamı anlaşma tutarına eşit olmalıdır.'
        : code === 'Installment.MonthlyAmountMustMatchAgreement' ? 'Aylık sabit modelde her vade tam aylık tutar olmalıdır; prorata yapılmaz.'
        : code === 'Installment.ExceedsAgreementAmount' ? 'Eklenen borçlar anlaşma tutarını aşamaz.'
        : code === 'Payment.AllocationExceedsBalance' ? 'Dağıtım, seçilen taksidin kalan bakiyesini aşıyor.'
        : code === 'Payment.InvalidAllocation' ? 'Dağıtılan taksitler seçili öğrenciye ait değil veya artık geçerli değil.'
        : code === 'Payment.InstallmentReversed' ? 'Ters kaydı bulunan takside tahsilat dağıtılamaz.'
        : code === 'Finance.PackageExpired' ? 'Paket geçerlilik süresi bu ders için dolmuş.'
        : code === 'Finance.PackageInsufficient' ? 'Bu pakette kullanılabilir ders hakkı kalmamış.'
        : code === 'Lesson.AttendanceStarted' ? 'Ders başladıktan veya yoklama girildikten sonra iptal edilemez.'
        : error instanceof Error && !(error instanceof HttpErrorResponse)
        ? error.message
        : error instanceof HttpErrorResponse && error.status === 409
        ? 'Kayıt başka bir işlemde değişti veya takvimde çakışma oluştu. Yenileyip tekrar deneyin.'
        : 'İşlem tamamlanamadı. Bilgilerinizi ve ürün erişiminizi kontrol edip tekrar deneyin.';
    } finally { this.busy = false; this.changeDetector.markForCheck(); }
  }
}
