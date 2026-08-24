import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { takeUntil, finalize, switchMap } from 'rxjs/operators';
import { UsersService } from '../../../core/services/users.service';
import { UserDetailDto } from '../../../core/models/user.model';
import { BaseComponent } from '../../../core/components/base.component';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

@Component({
    selector: 'app-editor-dialog',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './editor-dialog.component.html',
    styleUrls: ['./editor-dialog.component.scss']
})
export class EditorDialogComponent extends BaseComponent implements OnInit {
    private fb = inject(FormBuilder);
    private usersService = inject(UsersService);

    userForm: FormGroup;
    isEditMode = false;
    saving = false;
    hidePassword = true;
    passwordErrorMessages = Object.entries(PASSWORD_ERROR_MESSAGES).map(([type, message]) => ({ type, message }));

    constructor(
        public dialogRef: MatDialogRef<EditorDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: UserDetailDto | null
    ) {
        super();
        this.isEditMode = !!data;
        this.userForm = this.createForm();
    }

    ngOnInit() { }

    createForm(): FormGroup {
        const formConfig: any = {
            firstName: [this.data?.firstName || '', [Validators.required, Validators.minLength(2)]],
            lastName: [this.data?.lastName || '', [Validators.required, Validators.minLength(2)]],
            email: [this.data?.email || '', [Validators.required, Validators.email]],
            phoneNumber: [this.data?.phoneNumber || '', [Validators.pattern(/^[+]?[(]?[0-9]{3}[)]?[-\s.]?[0-9]{3}[-\s.]?[0-9]{4,6}$/)]]
        };

        if (!this.isEditMode) {
            // Create: Password Required
            formConfig.password = ['', [Validators.required, strongPasswordValidator()]];
        } else {
            // Edit: Password Optional
            formConfig.password = ['', [strongPasswordValidator()]];
        }

        return this.fb.group(formConfig);
    }

    onSave() {
        if (this.userForm.invalid) {
            return;
        }

        this.saving = true;
        const formValue = this.userForm.value;

        // Prepare DTO
        let userData: any = {
            ...formValue,
            roles: ['Editor']
        };

        if (this.isEditMode) {
            // Update User
            const updateOp = this.usersService.updateUser(this.data!.id, userData);

            let finalOp = updateOp;
            if (formValue.password) {
                finalOp = this.usersService.adminResetPassword(this.data!.id, formValue.password).pipe(
                    switchMap(() => updateOp)
                );
            }

            finalOp
                .pipe(
                    takeUntil(this.destroy$),
                    finalize(() => this.saving = false)
                )
                .subscribe({
                    next: () => {
                        const msg = formValue.password ? 'Editör ve şifresi güncellendi' : 'Editör güncellendi';
                        this.handleSuccess(msg);
                        this.dialogRef.close(true);
                    },
                    error: (error) => {
                        this.toaster.error('İşlem başarısız oldu. Lütfen bilgileri kontrol edin.');
                    }
                });

        } else {
            // Create User
            this.usersService.createUser(userData)
                .pipe(
                    takeUntil(this.destroy$),
                    finalize(() => this.saving = false)
                )
                .subscribe({
                    next: () => {
                        this.handleSuccess('Editör oluşturuldu');
                        this.dialogRef.close(true);
                    },
                    error: (error) => {
                        this.toaster.error('İşlem başarısız oldu. Lütfen e-posta adresinin benzersiz olduğundan emin olun.');
                    }
                });
        }
    }

    onCancel() {
        this.dialogRef.close(false);
    }
}
