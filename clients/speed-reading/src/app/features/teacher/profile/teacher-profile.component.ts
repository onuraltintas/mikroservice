import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { UserDetailDto, UpdateUserRequest } from '../../../core/models/user.model';
import { finalize } from 'rxjs/operators';

@Component({
    selector: 'app-teacher-profile',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatTabsModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatDividerModule,
        MatDividerModule,
        MatProgressBarModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './teacher-profile.component.html',
    styleUrls: ['./teacher-profile.component.scss']
})
export class TeacherProfileComponent implements OnInit {
    private authService = inject(AuthService);
    private usersService = inject(UsersService);
    private toaster = inject(ToasterService);
    private fb = inject(FormBuilder);

    user = signal<UserDetailDto | null>(null);
    loading = signal(false);
    saving = signal(false);

    profileForm: FormGroup;
    passwordForm: FormGroup;

    constructor() {
        this.profileForm = this.fb.group({
            firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
            lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
            email: [{ value: '', disabled: true }, [Validators.required, Validators.email]],
            phoneNumber: ['', [Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
            dateOfBirth: [null]
        });

        this.passwordForm = this.fb.group({
            currentPassword: ['', [Validators.required]],
            newPassword: ['', [
                Validators.required,
                Validators.minLength(8),
                Validators.pattern('^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$')
            ]],
            confirmNewPassword: ['', [Validators.required]]
        }, { validators: this.passwordMatchValidator });
    }

    passwordMatchValidator(g: FormGroup) {
        return g.get('newPassword')?.value === g.get('confirmNewPassword')?.value
            ? null : { mismatch: true };
    }

    ngOnInit() {
        this.loadProfile();
    }

    loadProfile() {
        this.loading.set(true);
        const currentUser = this.authService.currentUserValue;

        if (!currentUser?.id) {
            this.loading.set(false);
            return;
        }

        this.usersService.getUserById(currentUser.id)
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (user) => {
                    this.user.set(user);
                    this.patchForm(user);
                },
                error: (err) => {
                    console.error('Error loading profile', err);
                    this.toaster.error('Profil bilgileri yüklenemedi.');
                }
            });
    }

    patchForm(user: UserDetailDto) {
        this.profileForm.patchValue({
            firstName: user.firstName,
            lastName: user.lastName,
            email: user.email,
            phoneNumber: user.phoneNumber,
            dateOfBirth: user.dateOfBirth ? new Date(user.dateOfBirth) : null
        });
    }

    updateProfile() {
        if (this.profileForm.invalid) return;

        const currentUser = this.user();
        if (!currentUser) return;

        this.saving.set(true);
        const val = this.profileForm.value;

        const request: UpdateUserRequest = {
            firstName: val.firstName,
            lastName: val.lastName,
            phoneNumber: val.phoneNumber,
            dateOfBirth: val.dateOfBirth ? val.dateOfBirth.toISOString() : undefined,
            institutionId: currentUser.institutionId,
            email: currentUser.email
        };

        this.usersService.updateUser(currentUser.id, request)
            .pipe(finalize(() => this.saving.set(false)))
            .subscribe({
                next: (updatedUser) => {
                    this.toaster.success('Profil başarıyla güncellendi.');
                    this.authService.updateUser({
                        firstName: updatedUser.firstName,
                        lastName: updatedUser.lastName
                    });
                    this.loadProfile();
                },
                error: (err) => {
                    console.error('Error updating profile', err);
                    this.toaster.error('Profil güncellenirken bir hata oluştu.');
                }
            });
    }

    changePassword() {
        if (this.passwordForm.invalid) return;

        this.saving.set(true);
        const val = this.passwordForm.value;

        this.authService.changePassword({
            currentPassword: val.currentPassword,
            newPassword: val.newPassword,
            confirmNewPassword: val.confirmNewPassword
        })
            .pipe(finalize(() => this.saving.set(false)))
            .subscribe({
                next: () => {
                    this.toaster.success('Şifreniz başarıyla değiştirildi.');
                    this.passwordForm.reset();
                },
                error: (err) => {
                    console.error('Error changing password', err);
                    const msg = err.error?.detail || err.error?.title || 'Şifre değiştirilemedi.';
                    this.toaster.error(msg);
                }
            });
    }

    getInitials(firstName: string, lastName: string): string {
        return (firstName?.charAt(0) || '') + (lastName?.charAt(0) || '');
    }
}
