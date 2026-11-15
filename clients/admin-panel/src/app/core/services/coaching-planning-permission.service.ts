import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * User decision 15.11.2026: a student with an active teacher makes or changes her own plan only when the teacher allows it
 * (off by default); without a teacher she plans freely. studentMayPlan is the outcome of the two.
 */
export interface PlanningPermission { hasTeacher: boolean; allowedByTeacher: boolean; studentMayPlan: boolean }

@Injectable({ providedIn: 'root' })
export class CoachingPlanningPermissionService {
  private readonly http = inject(HttpClient);
  private readonly staffUrl = (studentId: string) =>
    `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/planning-permission`;

  /** The signed-in student's own state. */
  own() {
    return this.http.get<{ data: PlanningPermission }>(`${environment.apiUrl}/coaching/study-planning/permission`).pipe(map(r => r.data));
  }
  forStudent(studentId: string) {
    return this.http.get<{ data: PlanningPermission }>(this.staffUrl(studentId)).pipe(map(r => r.data));
  }
  /** Only the student's teacher may change it. */
  set(studentId: string, studentMayPlan: boolean) {
    return this.http.put<{ data: PlanningPermission }>(this.staffUrl(studentId), { studentMayPlan }).pipe(map(r => r.data));
  }
}
