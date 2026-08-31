import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ToasterService } from '../../../core/services/toaster.service';
import { AuthService } from '../../../core/services/auth.service';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-profile-setup',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  templateUrl: './profile-setup.component.html',
  styleUrls: ['./profile-setup.component.scss']
})
export class ProfileSetupComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private http = inject(HttpClient);

  private authService = inject(AuthService);
  private ageGroupService = inject(AgeGroupConfigurationService);

  basicInfoForm: FormGroup;
  goalsForm: FormGroup;
  saving = false;
  currentStep = signal(1);
  calculatedAgeGroup: string | null = null;
  calculatedAgeGroupText: string | null = null;
  recommendedWPM = 250;
  recommendedComprehension = 75;
  recommendedDailyMinutes = 20;
  private ageConfigs: AgeGroupConfiguration[] = [];

  readonly todayDateStr = new Date().toISOString().split('T')[0]; // YYYY-MM-DD (max değer)

  constructor() {
    this.basicInfoForm = this.fb.group({
      dateOfBirth: ['', Validators.required],  // 'YYYY-MM-DD' string saklar
      learningStyle: ['visual', Validators.required]
    });

    this.goalsForm = this.fb.group({
      targetWPM: [250, [Validators.required, Validators.min(100)]],
      targetComprehension: [75, [Validators.required, Validators.min(60), Validators.max(100)]],
      dailyGoalMinutes: [20, [Validators.required, Validators.min(10), Validators.max(120)]]
    });

    this.loadAgeConfigs();
  }

  private loadAgeConfigs() {
    this.ageGroupService.getActive().subscribe(configs => this.ageConfigs = configs);
  }

  /** Native <input type="date"> değiştiğinde çağrılır */
  onNativeDateChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value; // 'YYYY-MM-DD'
    if (val) {
      const [year, month, day] = val.split('-').map(Number);
      const date = new Date(year, month - 1, day);
      if (!isNaN(date.getTime())) {
        this.basicInfoForm.patchValue({ dateOfBirth: val });
        this.calculateAgeGroup(date);
      }
    } else {
      this.basicInfoForm.patchValue({ dateOfBirth: '' });
    }
  }

  nextStep(): void {
    if (this.currentStep() === 1) {
      if (this.basicInfoForm.invalid) {
        this.basicInfoForm.markAllAsTouched();
        this.toaster.error('Lütfen tüm alanları doldurun', 2500);
        return;
      }
    }
    if (this.currentStep() === 2) {
      if (this.goalsForm.invalid) {
        this.goalsForm.markAllAsTouched();
        this.toaster.error('Lütfen tüm alanları doldurun', 2500);
        return;
      }
    }
    this.currentStep.update(s => Math.min(s + 1, 3));
  }

  prevStep(): void {
    this.currentStep.update(s => Math.max(s - 1, 1));
  }

  calculateAgeGroup(dateOfBirth: Date): void {

    const today = new Date();
    const birthDate = new Date(dateOfBirth);
    let age = today.getFullYear() - birthDate.getFullYear();
    const monthDiff = today.getMonth() - birthDate.getMonth();

    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
      age--;
    }



    if (this.ageConfigs.length > 0) {
      const config = this.ageConfigs.find(c =>
        age >= c.minAge && (c.maxAge === null || age <= c.maxAge)
      );

      if (config) {
        this.calculatedAgeGroupText = config.displayName;
        this.recommendedWPM = config.recommendedWPM;
        this.recommendedComprehension = config.recommendedComprehension;
        this.recommendedDailyMinutes = config.recommendedDailyMinutes;
        this.calculatedAgeGroup = config.id;
      }
    } else {
      // Fallback or wait for load
    }

    // Update goals form with recommendations
    this.goalsForm.patchValue({
      targetWPM: this.recommendedWPM,
      targetComprehension: this.recommendedComprehension,
      dailyGoalMinutes: this.recommendedDailyMinutes
    });


  }

  getAgeGroupText(ageGroup: string | number | null): string {
    return this.calculatedAgeGroupText || '';
  }

  getLearningStyleText(style: string): string {
    switch (style) {
      case 'visual': return 'Görsel';
      case 'auditory': return 'İşitsel';
      case 'kinesthetic': return 'Kinestetik';
      default: return style;
    }
  }

  saveProfile(): void {


    if (this.basicInfoForm.invalid || this.goalsForm.invalid) {

      this.toaster.error('Lütfen tüm alanları doldurun', 3000);
      return;
    }

    // Check if date of birth is set
    if (!this.basicInfoForm.value.dateOfBirth) {
      this.toaster.error('Lütfen doğum tarihinizi giriniz', 3000);
      return;
    }

    // Calculate age group if not already calculated
    if (this.calculatedAgeGroup === null) {
      this.calculateAgeGroup(this.basicInfoForm.value.dateOfBirth);

      if (this.calculatedAgeGroup === null) {
        this.toaster.error('Yaş grubu hesaplanamadı. Lütfen geçerli bir doğum tarihi giriniz.', 3000);
        return;
      }
    }

    this.saving = true;

    const birthDate = new Date(
      `${this.basicInfoForm.value.dateOfBirth}T00:00:00.000Z`
    ).toISOString();
    const identityProfile = {
      birthDate,
      learningStyle: this.basicInfoForm.value.learningStyle
    };
    const speedReadingProfile = {
      currentLevel: 1,
      targetWPM: this.goalsForm.value.targetWPM,
      targetComprehension: this.goalsForm.value.targetComprehension,
      dailyGoalMinutes: this.goalsForm.value.dailyGoalMinutes,
      ageGroupConfigurationId: this.calculatedAgeGroup
    };

    this.http.put(`${environment.apiUrl}/users/me`, identityProfile).pipe(
      switchMap(() => this.http.put(
        `${environment.apiUrl}/speed-reading/adaptive-learning/profile`,
        speedReadingProfile
      ))
    ).subscribe({
      next: (response) => {
        this.saving = false;

        // Update currentUser with dateOfBirth and ageGroup for assessment
        this.authService.updateUser({
          dateOfBirth: this.basicInfoForm.value.dateOfBirth,
          ageGroupId: this.calculatedAgeGroup,
          ageGroupName: this.calculatedAgeGroupText
        });


        this.toaster.success('Profiliniz başarıyla oluşturuldu!', 3000);

        // Use setTimeout to ensure toaster is shown before navigation
        setTimeout(() => {
          // Navigate to assessment intro for first-time users
          this.router.navigate(['/student/assessment-intro']).catch(err => {
            console.error('❌ Navigation error:', err);
          });
        }, 500);
      },
      error: (error) => {
        console.error('❌ Error saving profile:', error);
        console.error('❌ Error details:', {
          status: error.status,
          message: error.message,
          error: error.error
        });
        this.toaster.error('Profil kaydedilirken hata oluştu', 3000);
        this.saving = false;
      }
    });
  }
}
