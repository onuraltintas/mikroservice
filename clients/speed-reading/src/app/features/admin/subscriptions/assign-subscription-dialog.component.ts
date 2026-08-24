import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, FormControl } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize, takeUntil, debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { BaseComponent } from '../../../core/components/base.component';
import { SubscriptionService, SubscriptionPlan, UserSearchResult } from '../../../core/services/subscription.service';

type DurationPreset = { label: string; days: number | null };

@Component({
  selector: 'app-assign-subscription-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './assign-subscription-dialog.component.html',
  styleUrls: ['./assign-subscription-dialog.component.scss']
})
export class AssignSubscriptionDialogComponent extends BaseComponent implements OnInit {
  private fb     = inject(FormBuilder);
  private service = inject(SubscriptionService);
  dialogRef       = inject(MatDialogRef<AssignSubscriptionDialogComponent>);

  // ── Data ─────────────────────────────────────────────────────────
  plans: SubscriptionPlan[]   = [];
  userResults: UserSearchResult[] = [];
  selectedUser: UserSearchResult | null = null;
  selectedPlan: SubscriptionPlan | null = null;
  loadingPlans   = false;
  searchingUsers = false;
  showDropdown   = false;

  // ── Step ─────────────────────────────────────────────────────────
  step = 1; // 1=kullanıcı, 2=plan, 3=süre

  // ── Duration presets ─────────────────────────────────────────────
  durationPresets: DurationPreset[] = [
    { label: '1 Ay',    days: 30   },
    { label: '3 Ay',    days: 90   },
    { label: '6 Ay',    days: 180  },
    { label: '1 Yıl',   days: 365  },
    { label: 'Sınırsız', days: null },
  ];
  selectedPreset: DurationPreset | null = null;

  today = new Date().toISOString().split('T')[0];

  userSearchControl = new FormControl('');

  form = this.fb.group({
    startDate: [this.today, [Validators.required]],
    endDate:   [null as string | null],
    notes:     [null as string | null],
  });

  ngOnInit(): void {
    this.loadPlans();

    this.userSearchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(value => {
        const q = (value ?? '').trim();
        if (q.length < 2) { this.userResults = []; this.showDropdown = false; return of([]); }
        this.searchingUsers = true;
        this.showDropdown   = true;
        return this.service.searchUsers(q).pipe(finalize(() => this.searchingUsers = false));
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => { this.userResults = results; });
  }

  loadPlans(): void {
    this.loadingPlans = true;
    this.service.getAllPlans()
      .pipe(takeUntil(this.destroy$), finalize(() => { this.loadingPlans = false; }))
      .subscribe({
        next: plans => { this.plans = plans.filter(p => p.isActive); },
        error: () => this.toaster.error('Planlar yüklenemedi')
      });
  }

  // ── User ─────────────────────────────────────────────────────────
  selectUser(user: UserSearchResult): void {
    this.selectedUser = user;
    this.userSearchControl.setValue(user.fullName || user.email, { emitEvent: false });
    this.userResults  = [];
    this.showDropdown = false;
  }

  clearUser(): void {
    this.selectedUser = null;
    this.userSearchControl.setValue('');
    this.step = 1;
  }

  goToStep2(): void {
    if (!this.selectedUser) return;
    this.step = 2;
  }

  // ── Plan ─────────────────────────────────────────────────────────
  selectPlan(plan: SubscriptionPlan): void {
    this.selectedPlan = plan;
  }

  goToStep3(): void {
    if (!this.selectedPlan) return;
    this.step = 3;
    // Apply first preset by default
    if (!this.selectedPreset) this.applyPreset(this.durationPresets[0]);
  }

  // ── Duration ─────────────────────────────────────────────────────
  applyPreset(preset: DurationPreset): void {
    this.selectedPreset = preset;
    const start = new Date(this.form.get('startDate')!.value || this.today);
    if (preset.days === null) {
      this.form.get('endDate')!.setValue(null);
    } else {
      const end = new Date(start);
      end.setDate(end.getDate() + preset.days);
      this.form.get('endDate')!.setValue(end.toISOString().split('T')[0]);
    }
  }

  // ── Submit ────────────────────────────────────────────────────────
  save(): void {
    if (!this.selectedUser || !this.selectedPlan || this.form.invalid) return;

    const val = this.form.getRawValue();
    this.loading.set(true);
    this.service.createSubscription({
      userId:    this.selectedUser.id,
      userName:  this.selectedUser.fullName,
      userEmail: this.selectedUser.email,
      planId:    this.selectedPlan.id,
      startDate: val.startDate!,
      endDate:   val.endDate || null,
      notes:     val.notes   || null,
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next:  () => { this.toaster.success('Abonelik başarıyla atandı'); this.dialogRef.close(true); },
        error: () => this.toaster.error('Abonelik atanırken hata oluştu')
      });
  }

  cancel(): void { this.dialogRef.close(false); }

  // ── Helpers ───────────────────────────────────────────────────────
  getRoleLabel(role: string): string {
    return ({ Student: 'Öğrenci', Teacher: 'Öğretmen', Coach: 'Koç', Admin: 'Admin' } as any)[role] ?? role;
  }

  getBillingPeriodLabel(period: string): string {
    return ({ Monthly: 'Aylık', Quarterly: '3 Aylık', Annual: 'Yıllık', Lifetime: 'Ömür Boyu' } as any)[period] ?? period;
  }

  getModuleLabel(mod: string): string {
    return mod === 'SpeedReading' ? 'Hızlı Okuma' : 'Koçluk';
  }

  getModuleIcon(mod: string): string {
    return mod === 'SpeedReading' ? 'speed' : 'psychology';
  }

  get endDateDisplay(): string {
    const d = this.form.get('endDate')?.value;
    if (!d) return 'Sınırsız';
    return new Date(d).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
  }
}
