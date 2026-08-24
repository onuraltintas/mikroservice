import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { subDays, subMonths } from 'date-fns';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil } from 'rxjs/operators';
import { StudentDashboardReportComponent } from './student-dashboard-report.component';

@Component({
  selector: 'app-student-reports',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, StudentDashboardReportComponent],
  templateUrl: './student-reports.component.html',
  styleUrls: ['./student-reports.component.scss']
})
export class StudentReportsComponent extends BaseComponent implements OnInit {
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);

  dateRangeControl = new FormControl('last7days');
  startDateControl = new FormControl(subDays(new Date(), 7));
  endDateControl = new FormControl(new Date());

  isExporting = signal(false);
  currentStudentId = this.authService.currentUserValue?.id || '';

  ngOnInit(): void {
    this.onDateRangeChange();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  onDateRangeChange(): void {
    const range = this.dateRangeControl.value;
    const now = new Date();

    switch (range) {
      case 'last7days':
        this.startDateControl.setValue(subDays(now, 7));
        this.endDateControl.setValue(now);
        break;
      case 'last30days':
        this.startDateControl.setValue(subDays(now, 30));
        this.endDateControl.setValue(now);
        break;
      case 'last3months':
        this.startDateControl.setValue(subMonths(now, 3));
        this.endDateControl.setValue(now);
        break;
    }
  }


}
