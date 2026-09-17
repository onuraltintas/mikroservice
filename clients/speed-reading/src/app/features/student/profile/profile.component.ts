import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { UserDto, UpdateCurrentUserProfileRequest } from '../../../core/models/user.model';

// Register Turkish locale data
import localeTr from '@angular/common/locales/tr';
import { registerLocaleData } from '@angular/common';
registerLocaleData(localeTr);

@Component({
    selector: 'app-profile',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule
    ],
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit {
    private fb = inject(FormBuilder);
    private authService = inject(AuthService);
    private usersService = inject(UsersService);
    private toaster = inject(ToasterService);

    profileForm: FormGroup;
    loading = signal(true);
    saving = signal(false);
    currentUser = signal<UserDto | null>(null);

    constructor() {
        this.profileForm = this.fb.group({
            firstName: ['', [Validators.required, Validators.minLength(2)]],
            lastName: ['', [Validators.required, Validators.minLength(2)]],
            email: [{ value: '', disabled: true }, [Validators.required, Validators.email]],
            phoneNumber: [''],
            dateOfBirth: [null]
        });
    }

    ngOnInit(): void {
        this.loadUserProfile();
    }

    loadUserProfile(): void {
        const userId = this.authService.currentUserValue?.id;
        if (!userId) {
            this.toaster.error('Kullanıcı bilgisi bulunamadı');
            this.loading.set(false);
            return;
        }

        this.usersService.getMyProfile().subscribe({
            next: (user) => {
                this.currentUser.set(user);
                this.profileForm.patchValue({
                    firstName: user.firstName,
                    lastName: user.lastName,
                    email: user.email,
                    phoneNumber: user.phoneNumber,
                    dateOfBirth: user.dateOfBirth ? new Date(user.dateOfBirth).toISOString().substring(0, 10) : null
                });
                this.loading.set(false);
            },
            error: (err) => {
                console.error('Error loading profile:', err);
                this.toaster.error('Profil bilgileri yüklenemedi');
                this.loading.set(false);
            }
        });
    }

    onSubmit(): void {
        if (this.profileForm.invalid) return;

        const userId = this.authService.currentUserValue?.id;
        if (!userId) return;

        this.saving.set(true);

        const formValue = this.profileForm.getRawValue();
        const updateRequest: UpdateCurrentUserProfileRequest = {
            firstName: formValue.firstName,
            lastName: formValue.lastName,
            phoneNumber: formValue.phoneNumber,
            birthDate: formValue.dateOfBirth ? new Date(formValue.dateOfBirth).toISOString() : null
        };

        this.usersService.updateMyProfile(updateRequest).subscribe({
            next: () => {
                this.toaster.success('Profil başarıyla güncellendi');
                this.saving.set(false);
                // Update local auth state if needed
                this.authService.updateUser({
                    firstName: formValue.firstName,
                    lastName: formValue.lastName,
                    dateOfBirth: formValue.dateOfBirth
                });
            },
            error: (err) => {
                console.error('Error updating profile:', err);
                this.toaster.error('Profil güncellenemedi');
                this.saving.set(false);
            }
        });
    }
}
