import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ReportsService } from '../../../core/services/reports.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, filter } from 'rxjs/operators';
import { subDays, subMonths } from 'date-fns';

@Component({
  selector: 'app-admin-reports',
  standalone: true,
  imports: [CommonModule, RouterModule, MatTabsModule, MatButtonModule, MatIconModule, MatSelectModule, MatFormFieldModule, MatDatepickerModule, MatNativeDateModule, MatInputModule, MatSnackBarModule, ReactiveFormsModule],
  templateUrl: './admin-reports.component.html',
  styleUrls: ['./admin-reports.component.scss']
})
export class AdminReportsComponent extends BaseComponent {
  private reportsService = inject(ReportsService);
  private router = inject(Router);

  dateRangeControl = new FormControl('last30days');
  startDateControl = new FormControl(subDays(new Date(), 30));
  endDateControl = new FormControl(new Date());

  // Hide global controls for students tab (has its own controls)
  showGlobalControls = signal(true);

  ngOnInit(): void {
    this.onDateRangeChange();
    this.checkRoute(this.router.url);

    // Subscribe to route changes
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntil(this.destroy$)
    ).subscribe((event: any) => {
      this.checkRoute(event.urlAfterRedirects || event.url);
    });
  }

  private checkRoute(url: string): void {
    // Hide global controls for students tab - it has its own controls
    this.showGlobalControls.set(!url.includes('/reports/students'));
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  onDateRangeChange(): void {
    const range = this.dateRangeControl.value;
    const now = new Date();
    switch (range) {
      case 'last7days': this.startDateControl.setValue(subDays(now, 7)); this.endDateControl.setValue(now); break;
      case 'last30days': this.startDateControl.setValue(subDays(now, 30)); this.endDateControl.setValue(now); break;
      case 'last3months': this.startDateControl.setValue(subMonths(now, 3)); this.endDateControl.setValue(now); break;
    }
  }

  exportReport(): void {
    const data = { startDate: this.startDateControl.value, endDate: this.endDateControl.value };
    this.reportsService.exportReportToPdf(data)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `admin-report-${Date.now()}.pdf`;
          a.click();
          window.URL.revokeObjectURL(url);
          this.handleSuccess('Exported');
        },
        error: (err) => this.handleError(err, 'Failed')
      });
  }
}

