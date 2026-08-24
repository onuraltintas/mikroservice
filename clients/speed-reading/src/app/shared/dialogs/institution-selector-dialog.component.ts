import { Component, inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { debounceTime, distinctUntilChanged, switchMap, catchError, map, startWith } from 'rxjs/operators';
import { of, BehaviorSubject, Observable } from 'rxjs';

@Component({
  selector: 'app-institution-selector-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatAutocompleteModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule
  ],
  template: `
    <h2 mat-dialog-title>Kurum Seç</h2>
    <mat-dialog-content class="dialog-content">
      <div class="info-text">
        İşlem yapmak istediğiniz kurumu listeden seçin veya aratın.
      </div>
      
      <mat-form-field appearance="outline" class="w-full search-field">
        <mat-label>Kurum Ara</mat-label>
        <input type="text"
               placeholder="Örn: Merkez Lisesi"
               matInput
               [formControl]="searchControl"
               [matAutocomplete]="auto"
               #trigger="matAutocompleteTrigger"
               (click)="trigger.openPanel()"
               (focus)="trigger.openPanel()">
        <mat-icon matSuffix *ngIf="!(loading$ | async)">search</mat-icon>
        <mat-spinner matSuffix *ngIf="loading$ | async" diameter="20"></mat-spinner>
        
        <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayFn" (optionSelected)="onOptionSelected($event)">
          <mat-option *ngFor="let inst of institutions$ | async" [value]="inst">
            <div class="option-container">
              <span class="inst-name">{{ inst.name }}</span>
              <span class="inst-meta">{{ inst.code || 'Kod Yok' }} | {{ inst.type || 'Kurum' }}</span>
            </div>
          </mat-option>
          
          <mat-option *ngIf="(institutions$ | async)?.length === 0 && searchControl.value && typeof searchControl.value === 'string'" disabled>
            Sonuç bulunamadı.
          </mat-option>
        </mat-autocomplete>
      </mat-form-field>

    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>iptal</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-content {
      padding-top: 1rem !important; /* Ensure space for label */
      min-height: 300px; /* Make dialog taller */
      display: flex;
      flex-direction: column;
    }
    .info-text {
      margin-bottom: 1.5rem;
      color: #6b7280;
      font-size: 0.95rem;
    }
    .w-full {
      width: 100%;
    }
    .search-field {
      margin-top: 0.5rem;
    }
    .option-container {
      display: flex;
      flex-direction: column;
      padding: 0.25rem 0;
    }
    .inst-name {
      font-weight: 500;
      font-size: 0.95rem;
      color: #1f2937;
    }
    .inst-meta {
      font-size: 0.75rem;
      color: #9ca3af;
    }
    /* Fix for label clipping usually involves ensuring overflow isn't hidden too aggressively or padding */
    ::ng-deep .mat-mdc-dialog-container .mdc-dialog__surface {
      overflow: visible !important; 
    }
  `],
  encapsulation: ViewEncapsulation.None
})
export class InstitutionSelectorDialogComponent {
  private http = inject(HttpClient);
  public dialogRef = inject(MatDialogRef<InstitutionSelectorDialogComponent>);

  searchControl = new FormControl('');
  loading$ = new BehaviorSubject<boolean>(false);

  institutions$: Observable<any[]>;

  constructor() {
    this.institutions$ = this.searchControl.valueChanges.pipe(
      startWith(''), // Trigger initial load
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        // If term is object (selection), don't verify, just return list or empty? 
        // Actually if selection is made, we might want to keep the list or filter it. 
        // Let's filter by string only.
        const searchTerm = typeof term === 'string' ? term : '';

        this.loading$.next(true);
        // Assuming API search returns all if searchTerm is empty
        return this.http.get<any[]>(`${environment.apiUrl}/v1/institutions`, { params: { searchTerm: searchTerm } }).pipe(
          map(res => (res as any).items || res),
          catchError(() => of([])),
          map(items => {
            this.loading$.next(false);
            return items;
          })
        );
      })
    );
  }

  displayFn(inst: any): string {
    return inst && inst.name ? inst.name : '';
  }

  onOptionSelected(event: any) {
    const inst = event.option.value;
    this.dialogRef.close(inst);
  }
}
