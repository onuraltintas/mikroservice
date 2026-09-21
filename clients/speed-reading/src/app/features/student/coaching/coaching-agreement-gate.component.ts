import { CommonModule } from '@angular/common';
import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CoachingAgreementService,
  CurrentCoachingAgreement
} from '../../../core/services/coaching-agreement.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-coaching-agreement-gate',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-agreement-gate.component.html',
  styleUrls: ['./coaching-agreement-gate.component.scss']
})
export class CoachingAgreementGateComponent implements OnInit {
  private readonly agreementService = inject(CoachingAgreementService);

  @Output() readonly ready = new EventEmitter<void>();

  agreement: CurrentCoachingAgreement | null = null;
  reviewConfirmed = false;
  loading = true;
  submitting = false;
  loadFailed = false;
  submitFailed = false;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.loadFailed = false;
    this.agreementService.getCurrent('tr-TR')
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: agreement => {
          if (agreement.acknowledgedByCurrentStudent) {
            this.ready.emit();
            return;
          }
          this.agreement = agreement;
        },
        error: () => this.loadFailed = true
      });
  }

  accept(): void {
    if (!this.agreement || !this.reviewConfirmed || this.submitting) {
      return;
    }

    this.submitting = true;
    this.submitFailed = false;
    this.agreementService.acknowledge(this.agreement.documentId)
      .pipe(finalize(() => this.submitting = false))
      .subscribe({
        next: () => this.ready.emit(),
        error: () => this.submitFailed = true
      });
  }
}
