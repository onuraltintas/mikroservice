import { Component, DestroyRef, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { StaffLocationService, StaffPlace } from '../../navigation/staff-location.service';
import { StaffSectionGroup, StaffSectionNavComponent } from '../../navigation/staff-section-nav.component';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';
import { CoachingTeacherSessionsComponent } from './coaching-teacher-sessions.component';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';
import { CoachingTeacherExamsComponent } from './coaching-teacher-exams.component';
import { CoachingTeacherSubscriptionComponent } from './coaching-teacher-subscription.component';
import { CoachingFinanceComponent } from '../../../../../../src/app/features/coaching-portal/pages/coaching-finance.component';
import { CoachingProfileComponent } from '../../../../../../shared/coaching-profile.component';
import { CoachingMessagesComponent } from '../../../../../../shared/coaching-messages.component';
import { environment } from '../../../environments/environment';
import {CoachingTeacherStudyOverviewComponent} from './coaching-teacher-study-overview.component';

import { CoachingTeacherTrackingComponent } from './coaching-teacher-tracking.component';
import { CoachingTeacherTrackingContext } from './coaching-teacher-tracking.service';
import type { WeeklyDestination } from '../../../../../../shared/coaching-notification-target';

type CoachingTeacherSection = 'students' | 'assignments' | 'sessions' | 'goals' | 'exams' | 'subscription' | 'finance' | 'profile' | 'study' | 'tracking' | 'messages';

@Component({
  selector: 'staff-coaching-teacher-workspace',
  standalone: true,
  providers: [CoachingTeacherTrackingContext],
  imports: [CoachingTeacherStudentsComponent, CoachingTeacherAssignmentsComponent, CoachingTeacherSessionsComponent, CoachingTeacherGoalsComponent, CoachingTeacherExamsComponent, CoachingTeacherSubscriptionComponent, CoachingFinanceComponent, CoachingProfileComponent,CoachingTeacherStudyOverviewComponent, CoachingTeacherTrackingComponent, StaffSectionNavComponent, CoachingMessagesComponent],
  templateUrl: './coaching-teacher-workspace.component.html',
  styleUrl: './coaching-teacher-workspace.component.scss'
})
export class CoachingTeacherWorkspaceComponent {
  readonly apiUrl = environment.apiUrl;
  @ViewChild(CoachingProfileComponent) private profile?: CoachingProfileComponent;
  @ViewChild('trackingPanel') private tracking?: Pick<CoachingTeacherTrackingComponent, 'canLeavePage' | 'openWeekly'>;
  @ViewChild(CoachingTeacherStudyOverviewComponent) private study?:CoachingTeacherStudyOverviewComponent;
  @ViewChild(CoachingTeacherStudentsComponent) private students?: CoachingTeacherStudentsComponent;
  @ViewChild(CoachingTeacherGoalsComponent) private goals?: CoachingTeacherGoalsComponent;
  @ViewChild(CoachingTeacherAssignmentsComponent) private assignments?: CoachingTeacherAssignmentsComponent;
  readonly notificationAssignmentId = signal<string | null>(null);
  readonly notificationWeeklyTarget = signal<WeeklyDestination | null>(null);
  async openWeeklyFromNotification(target: WeeklyDestination): Promise<boolean> {
    if (this.activeSection() === 'tracking' && this.tracking) {
      const opened = await this.tracking.openWeekly(target);
      if (opened) this.remember({ section: 'tracking', week: target });
      return opened;
    }
    if (!await this.canLeavePage()) return false;
    this.notificationWeeklyTarget.set(target); this.activeSection.set('tracking');
    this.remember({ section: 'tracking', week: target });
    return true;
  }
  async openAssignmentFromNotification(id: string): Promise<boolean> {
    if (this.assignments) {
      const opened = await this.assignments.reviewAssignment(id);
      if (opened) this.remember({ section: 'assignments', id });
      return opened;
    }
    if (!await this.canLeavePage()) return false;
    this.notificationAssignmentId.set(id);
    this.activeSection.set('assignments');
    this.remember({ section: 'assignments', id });
    return true;
  }
  /** Roadmap O3: a message notification opens its conversation. */
  async openMessageFromNotification(id: string): Promise<boolean> {
    if (this.activeSection() !== 'messages' && !await this.canLeavePage()) return false;
    this.notificationAssignmentId.set(id); this.notificationWeeklyTarget.set(null);
    this.activeSection.set('messages');
    this.remember({ section: 'messages', id });
    return true;
  }
  /** The open conversation is kept in the address. */
  rememberConversation(id: string | null): void {
    if (this.activeSection() !== 'messages' || id === this.notificationAssignmentId()) return;
    this.notificationAssignmentId.set(id);
    this.remember({ section: 'messages', id });
  }
  @ViewChild(CoachingFinanceComponent) financePanel?: Pick<CoachingFinanceComponent, 'canLeavePage'>;
  async canLeavePage(): Promise<boolean> { return (await this.tracking?.canLeavePage() ?? true) && (await this.study?.canLeavePage()??true) && (await this.profile?.canLeavePage() ?? true) && (await this.financePanel?.canLeavePage() ?? true) && (await this.students?.canLeavePage() ?? true) && (await this.goals?.canLeavePage() ?? true) && (await this.assignments?.canLeavePage() ?? true); }
  // Roadmap O2: the teacher starts on the tracking centre; sections are grouped and kept in the address.
  readonly activeSection = signal<CoachingTeacherSection>('tracking');
  readonly sectionGroups: StaffSectionGroup[] = [
    { label: 'Günlük takip', items: [
      { value: 'tracking', label: 'Takip merkezi', testId: 'tracking-tab' },
      { value: 'study', label: 'Çalışma takibi', testId: 'study-tab' },
      { value: 'messages', label: 'Mesajlar', testId: 'messages-tab' },
      { value: 'assignments', label: 'Ödevler', testId: 'assignments-tab' },
      { value: 'sessions', label: 'Seanslar', testId: 'sessions-tab' }] },
    { label: 'Öğrenciler', items: [
      { value: 'students', label: 'Öğrenciler ve raporlar', testId: 'students-tab' },
      { value: 'goals', label: 'Hedefler', testId: 'goals-tab' },
      { value: 'exams', label: 'Sınavlar', testId: 'exams-tab' }] },
    { label: 'Hesabım', items: [
      { value: 'profile', label: 'Koçluk profilim ve davetlerim', testId: 'teacher-profile-tab' },
      { value: 'finance', label: 'Tahsilat hareketleri', testId: 'teacher-finance-tab' },
      { value: 'subscription', label: 'Öğretmen planım', testId: 'teacher-subscription-tab' }] }
  ];
  private readonly staffLocation = inject(StaffLocationService, { optional: true });

  constructor() {
    const place = this.staffLocation?.place();
    if (this.isMine(place)) this.applyPlace(place!);
    this.staffLocation?.pops.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(next => { if (this.isMine(next)) void this.followAddress(next!); });
  }

  async selectSection(section: CoachingTeacherSection): Promise<void> {
    if (section === this.activeSection()) return;
    if (!await this.canLeavePage()) return;
    this.notificationAssignmentId.set(null);
    this.notificationWeeklyTarget.set(null);
    this.activeSection.set(section);
    this.remember({ section });
  }

  chooseSection(section: string): void { void this.selectSection(section as CoachingTeacherSection); }

  private isMine(place: StaffPlace | null | undefined): boolean { return place?.product === 'coaching' && place.role === 'teacher'; }

  private applyPlace(place: StaffPlace): void {
    this.activeSection.set((place.section ?? 'tracking') as CoachingTeacherSection);
    this.notificationAssignmentId.set(place.id);
    this.notificationWeeklyTarget.set(place.week);
  }

  /** Back and forward: follow the address unless unsaved work would be lost; then put the address back. */
  private async followAddress(place: StaffPlace): Promise<void> {
    const section = (place.section ?? 'tracking') as CoachingTeacherSection;
    if (section === this.activeSection() && !place.id && !place.week) return;
    if (!await this.canLeavePage()) { this.remember({ section: this.activeSection(), id: this.notificationAssignmentId(), week: this.notificationWeeklyTarget() }, true); return; }
    this.applyPlace(place);
  }

  private remember(place: { section: CoachingTeacherSection; id?: string | null; week?: WeeklyDestination | null }, replace = false): void {
    this.staffLocation?.go({ product: 'coaching', role: 'teacher', section: place.section, id: place.id ?? null, week: place.week ?? null }, replace);
  }

}
