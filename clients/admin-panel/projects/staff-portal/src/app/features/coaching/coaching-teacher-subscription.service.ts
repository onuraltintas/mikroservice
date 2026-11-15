import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherSubscriptionSeat {
  studentId: string;
  isSuspended: boolean;
  suspensionReason: string | null;
  assignedAt: string;
}

export interface CoachingTeacherSeatSummary {
  subscriptionId: string;
  planName: string;
  accessUntil: string;
  includedStudentSeats: number;
  usedStudentSeats: number;
  students: CoachingTeacherSubscriptionSeat[];
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherSubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly subscriptionsUrl = `${environment.apiUrl}/coaching/subscriptions`;

  getMySeatSummary() {
    return this.http.get<{ data: CoachingTeacherSeatSummary | null }>(`${this.subscriptionsUrl}/my-teacher-subscription`)
      .pipe(map(response => response.data));
  }

  assignStudent(studentId: string) {
    return this.http.put<void>(`${this.subscriptionsUrl}/my-teacher-subscription/students/${encodeURIComponent(studentId)}`, {});
  }

  removeStudent(studentId: string) {
    return this.http.delete<void>(`${this.subscriptionsUrl}/my-teacher-subscription/students/${encodeURIComponent(studentId)}`);
  }
}
