import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, Input, OnChanges, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize, forkJoin } from 'rxjs';
import { CoachingInstitutionService, CoachingInstitutionStudent, CoachingInstitutionSubscription } from './coaching-institution.service';

@Component({
  selector: 'staff-coaching-institution-subscription', standalone: true, imports: [CommonModule],
  template: `
    <section aria-labelledby="institution-plan-title" class="institution-plan">
      <header><div><h3 id="institution-plan-title">Kurum aboneliği ve kontenjan</h3><p>Etkin kurum lisansınızın öğrenci erişimini yönetin.</p></div>
        <button type="button" (click)="load()" [disabled]="loading() || saving()">Yenile</button></header>
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @if (success()) { <p role="status">{{ success() }}</p> }
      @if (loading()) { <p role="status">Abonelik yükleniyor…</p> }
      @else if (subscription(); as plan) {
        <p><strong>{{ plan.planName }}</strong> · {{ plan.accessUntil | date:'dd.MM.yyyy' }} tarihine kadar</p>
        <p><strong>{{ plan.usedStudentSeats }} / {{ plan.includedStudentSeats }}</strong> öğrenci koltuğu ayrılmış.</p>
        <ul>
          @for (student of students(); track student.userId) {
            <li><span>{{ student.firstName }} {{ student.lastName }}<small>{{ student.email }}</small></span>
              <button type="button" [attr.data-testid]="'institution-seat-' + student.userId" [disabled]="saving() || (!isActive(student.userId) && atCapacity())"
                (click)="setSeat(student.userId, isActive(student.userId))">{{ isActive(student.userId) ? 'Kontenjanı kaldır' : 'Koltuk ata' }}</button></li>
          } @empty { <li>Kurumda öğrenci kaydı bulunamadı.</li> }
        </ul>
        <nav aria-label="Kontenjan öğrenci sayfaları"><button type="button" (click)="changePage(-1)" [disabled]="saving() || page() <= 1">Önceki</button><span>Sayfa {{ page() }} / {{ totalPages() }}</span><button type="button" (click)="changePage(1)" [disabled]="saving() || page() >= totalPages()">Sonraki</button></nav>
        @for (seat of plan.students; track seat.studentId) {
          @if (!seat.isCurrentMember && !seat.isSuspended) {
            <div class="departed"><span>Kurumdan ayrılmış öğrenci · {{ seat.assignedAt | date:'dd.MM.yyyy' }} tarihinde ayrılan koltuk</span>
              <button type="button" [disabled]="saving()" (click)="setSeat(seat.studentId, true)">Kontenjanı serbest bırak</button></div>
          }
        }
      } @else if (!error()) { <p>Etkin kurum aboneliği bulunamadı. Lisans ve kontenjan için platform yöneticisiyle iletişime geçin.</p> }
    </section>`,
  styles: [`
    .institution-plan { padding:1rem; border:1px solid #e0e5db; border-radius:.8rem; background:#fffefa; color:#243b36; }
    header, li, nav, .departed { display:flex; gap:.75rem; justify-content:space-between; align-items:center; flex-wrap:wrap; }
    h3 { margin:0; } p, small { color:#687872; } small { display:block; }
    ul { list-style:none; padding:0; } li, .departed { padding:.8rem 0; border-top:1px solid #e8ece5; }
    button { min-height:2.4rem; border:1px solid #9aa99b; border-radius:.5rem; background:#fffefa; padding:.35rem .7rem; color:#29473b; cursor:pointer; }
    button:disabled { opacity:.55; cursor:not-allowed; } .error { color:#991b1b; }
  `]
})
export class CoachingInstitutionSubscriptionComponent implements OnChanges {
  @Input({ required: true }) institutionId = '';
  private readonly service = inject(CoachingInstitutionService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  private write?: Subscription;
  readonly subscription = signal<CoachingInstitutionSubscription | null>(null);
  readonly students = signal<CoachingInstitutionStudent[]>([]);
  readonly page = signal(1);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  ngOnChanges(): void {
    this.request?.unsubscribe(); this.write?.unsubscribe(); this.saving.set(false);
    this.subscription.set(null); this.students.set([]); this.page.set(1); this.totalPages.set(1);
    this.success.set(null); this.error.set(null); this.load();
  }
  canLeavePage(): boolean {
    if (this.saving()) { this.error.set('Kontenjan işlemi sürüyor. Tamamlanmasını bekleyin.'); return false; }
    return true;
  }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void { if (this.saving()) { event.preventDefault(); event.returnValue = ''; } }
  load(): void {
    if (!this.institutionId || this.saving()) return;
    this.request?.unsubscribe(); this.loading.set(true); this.error.set(null);
    this.request = forkJoin({ plan: this.service.getSubscription(), students: this.service.getStudentRoster(this.institutionId, this.page()) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
        next: result => { this.subscription.set(result.plan); this.students.set(result.students.students); this.totalPages.set(Math.max(1, Math.ceil(result.students.totalCount / 25))); },
        error: () => { this.subscription.set(null); this.students.set([]); this.error.set('Kurum aboneliği yüklenemedi. Yetkinizi kontrol edip yeniden deneyin.'); }
      });
  }
  changePage(delta: number): void {
    const next = this.page() + delta;
    if (this.saving() || this.loading() || next < 1 || next > this.totalPages()) return;
    this.page.set(next); this.load();
  }
  isActive(id: string): boolean { return this.subscription()?.students.some(seat => seat.studentId === id && !seat.isSuspended) ?? false; }
  atCapacity(): boolean { const plan = this.subscription(); return !!plan && plan.usedStudentSeats >= plan.includedStudentSeats; }
  setSeat(id: string, suspended: boolean): void {
    const plan = this.subscription();
    if (!plan || this.saving() || this.loading()) return;
    if (suspended ? !this.isActive(id) : !this.students().some(student => student.userId === id) || this.atCapacity()) return;
    this.saving.set(true); this.error.set(null); this.success.set(null);
    this.write = this.service.setSubscriptionSeat(plan.subscriptionId, id, suspended).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.saving.set(false); this.success.set(suspended ? 'Öğrenci kontenjanı kaldırıldı.' : 'Öğrenciye kontenjan atandı.'); this.load(); },
      error: () => this.error.set('Kontenjan kaydedilemedi. Kurum üyeliği, lisans süresi ve boş kontenjanı kontrol edip yeniden deneyin.')
    });
  }
}
