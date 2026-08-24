import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { UserDetailDto, UpdateUserRequest } from '../../../core/models/user.model';

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
    currentUser = signal<UserDetailDto | null>(null);

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

        this.usersService.getUserById(userId).subscribe({
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
        const updateRequest: UpdateUserRequest = {
            firstName: formValue.firstName,
            lastName: formValue.lastName,
            phoneNumber: formValue.phoneNumber,
            dateOfBirth: formValue.dateOfBirth ? new Date(formValue.dateOfBirth).toISOString() : undefined,
            email: formValue.email, // Email is usually not editable directly or requires special handling
            isActive: true, // Maintain active status
            roles: this.currentUser()?.roles || [] // Maintain roles
        };

        this.usersService.updateUser(userId, updateRequest).subscribe({
            next: (updatedUser) => {
                this.toaster.success('Profil başarıyla güncellendi');
                this.saving.set(false);
                // Update local auth state if needed
                this.authService.updateUser({
                    firstName: updatedUser.firstName,
                    lastName: updatedUser.lastName
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
