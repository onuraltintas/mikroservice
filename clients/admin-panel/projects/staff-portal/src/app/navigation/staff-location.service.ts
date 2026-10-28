import { Location } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { StaffProduct } from '../auth/staff-auth.models';
import type { WeeklyDestination } from '../../../../../shared/coaching-notification-target';
import { validCoachingId, validWeeklyDate } from '../../../../../shared/coaching-notification-target';

/**
 * Roadmap O2: the staff portal keeps its place in the address, so refresh, the back button and links
 * from notifications work. Paths are relative to the portal's base href, for example
 * /coaching/teacher/assignments/{id} or /coaching/institution/teachers.
 */
export type StaffWorkspaceRole = 'teacher' | 'institution';
export interface StaffPlace {
  product: StaffProduct;
  role: StaffWorkspaceRole | null;
  section: string | null;
  id: string | null;
  week: WeeklyDestination | null;
}

const products: StaffProduct[] = ['coaching', 'speed-reading', 'course-management'];
export const COACHING_TEACHER_SECTIONS = ['tracking', 'study', 'students', 'assignments', 'sessions', 'goals', 'exams', 'profile', 'finance', 'subscription'] as const;
export const COACHING_INSTITUTION_SECTIONS = ['overview', 'students', 'teachers', 'analytics', 'subscription', 'finance'] as const;

function sectionsOf(product: StaffProduct, role: StaffWorkspaceRole | null): readonly string[] {
  if (product !== 'coaching' || !role) return [];
  return role === 'teacher' ? COACHING_TEACHER_SECTIONS : COACHING_INSTITUTION_SECTIONS;
}

export function parseStaffPath(path: string): StaffPlace | null {
  const parts = path.split(/[?#]/)[0].split('/').filter(Boolean).map(part => decodeURIComponent(part));
  const product = parts[0] as StaffProduct;
  if (!products.includes(product)) return null;
  const role = parts[1] === 'teacher' || parts[1] === 'institution' ? parts[1] : null;
  const section = role && sectionsOf(product, role).includes(parts[2]) ? parts[2] : null;
  const id = section === 'assignments' && role === 'teacher' && validCoachingId(parts[3]) ? parts[3] : null;
  const week = section === 'tracking' && role === 'teacher' && validCoachingId(parts[3]) && validWeeklyDate(parts[4])
    && (parts.length === 5 || parts.length === 6 && validCoachingId(parts[5]))
    ? { studentId: parts[3], weekStart: parts[4], decisionId: parts[5] ?? null } : null;
  return { product, role, section, id, week };
}

export function formatStaffPath(place: StaffPlace): string {
  const parts: string[] = [place.product];
  if (place.role) {
    parts.push(place.role);
    if (place.section && sectionsOf(place.product, place.role).includes(place.section)) {
      parts.push(place.section);
      if (place.section === 'assignments' && place.id) parts.push(place.id);
      if (place.section === 'tracking' && place.week) parts.push(place.week.studentId, place.week.weekStart, ...(place.week.decisionId ? [place.week.decisionId] : []));
    }
  }
  return '/' + parts.map(encodeURIComponent).join('/');
}

@Injectable()
export class StaffLocationService {
  private readonly location = inject(Location);
  private readonly popped = new Subject<StaffPlace | null>();
  /** Where the portal is now. */
  readonly place = signal<StaffPlace | null>(parseStaffPath(this.location.path()));
  /** Back and forward buttons. */
  readonly pops = this.popped.asObservable();

  constructor() {
    this.location.subscribe(event => {
      const place = parseStaffPath(event.url ?? this.location.path());
      this.place.set(place);
      this.popped.next(place);
    });
  }

  /** Back to the portal root without adding a history entry. */
  goRoot(): void {
    this.place.set(null);
    if (this.location.path().split(/[?#]/)[0] !== '') this.location.replaceState('/');
  }

  go(place: StaffPlace, replace = false): void {
    const url = formatStaffPath(place);
    this.place.set(parseStaffPath(url));
    if (this.location.path().split(/[?#]/)[0] === url) return;
    if (replace) this.location.replaceState(url); else this.location.go(url);
  }
}

/** On the phone each table row is shown as a card; every cell carries its column name. */
export function labelTableCells(root: ParentNode): void {
  for (const table of Array.from(root.querySelectorAll('table'))) {
    const head = table.tHead?.rows[table.tHead.rows.length - 1];
    if (!head) continue;
    const labels = Array.from(head.cells).map(cell => cell.textContent?.trim() ?? '');
    for (const body of Array.from(table.tBodies)) for (const row of Array.from(body.rows)) Array.from(row.cells).forEach((cell, index) => {
      if (!cell.hasAttribute('data-label') && labels[index]) cell.setAttribute('data-label', labels[index]);
    });
  }
}
