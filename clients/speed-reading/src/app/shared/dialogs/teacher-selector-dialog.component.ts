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
  selector: 'app-teacher-selector-dialog',
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
    <h2 mat-dialog-title>Öğretmen Seç</h2>
    <mat-dialog-content class="dialog-content">
      <div class="info-text">
        İşlem yapmak istediğiniz öğretmeni listeden seçin veya aratın.
      </div>
      
      <mat-form-field appearance="outline" class="w-full search-field">
        <mat-label>Öğretmen Ara</mat-label>
        <input type="text"
               placeholder="İsim veya E-posta..."
               matInput
               [formControl]="searchControl"
               [matAutocomplete]="auto"
               #trigger="matAutocompleteTrigger"
               (click)="trigger.openPanel()"
               (focus)="trigger.openPanel()">
        <mat-icon matSuffix *ngIf="!(loading$ | async)">search</mat-icon>
        <mat-spinner matSuffix *ngIf="loading$ | async" diameter="20"></mat-spinner>
        
        <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayFn" (optionSelected)="onOptionSelected($event)">
          <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher">
             <div class="option-container">
              <span class="teacher-name">{{ teacher.firstName }} {{ teacher.lastName }}</span>
              <span class="teacher-email">{{ teacher.email }}</span>
              <span class="teacher-inst">{{ teacher.institutionName || 'Kurum Yok' }}</span>
            </div>
          </mat-option>
          
          <mat-option *ngIf="(teachers$ | async)?.length === 0 && searchControl.value && typeof searchControl.value === 'string'" disabled>
            Sonuç bulunamadı.
          </mat-option>
        </mat-autocomplete>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-content {
      padding-top: 1rem !important; 
      min-height: 350px; 
      display: flex;
      flex-direction: column;
    }
    .info-text {
      margin-bottom: 1.5rem;
      color: #6b7280;
      font-size: 0.95rem;
    }
    .w-full { width: 100%; }
    .search-field { margin-top: 0.5rem; }
    .option-container {
      display: flex;
      flex-direction: column;
      padding: 0.25rem 0;
      line-height: normal;
    }
    .teacher-name { font-weight: 500; font-size: 0.95rem; color: #1f2937; margin-bottom: 2px; }
    .teacher-email { font-size: 0.8rem; color: #4b5563; }
    .teacher-inst { font-size: 0.75rem; color: #9ca3af; margin-top: 2px; font-style: italic; }
    
    ::ng-deep .mat-mdc-dialog-container .mdc-dialog__surface {
      overflow: visible !important; 
    }
  `],
  encapsulation: ViewEncapsulation.None
})
export class TeacherSelectorDialogComponent {
  private http = inject(HttpClient);
  public dialogRef = inject(MatDialogRef<TeacherSelectorDialogComponent>);

  searchControl = new FormControl('');
  loading$ = new BehaviorSubject<boolean>(false);

  teachers$: Observable<any[]>;

  constructor() {
    this.teachers$ = this.searchControl.valueChanges.pipe(
      startWith(''),
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        const searchTerm = typeof term === 'string' ? term : '';
        this.loading$.next(true);
        // Assuming Admin can search all teachers
        return this.http.get<any[]>(`${environment.apiUrl}/v1/teachers`, { params: { searchTerm: searchTerm } }).pipe(
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

  displayFn(teacher: any): string {
    return teacher ? `${teacher.firstName} ${teacher.lastName}` : '';
  }

  onOptionSelected(event: any) {
    const teacher = event.option.value;
    this.dialogRef.close(teacher);
  }
}
