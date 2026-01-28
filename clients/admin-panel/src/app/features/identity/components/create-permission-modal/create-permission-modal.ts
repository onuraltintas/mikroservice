import { Component, EventEmitter, inject, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-create-permission-modal',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    templateUrl: './create-permission-modal.html'
})
export class CreatePermissionModalComponent {
    @Output() close = new EventEmitter<boolean>();

    private fb = inject(FormBuilder);
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);

    form: FormGroup;
    loading = signal(false);

    constructor() {
        this.form = this.fb.group({
            key: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
            description: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
            group: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]]
        });
    }

    onSubmit() {
        if (this.form.invalid) return;

        this.loading.set(true);
        this.identityService.createPermission(this.form.value).subscribe({
            next: () => {
                this.toaster.success('İzin başarıyla oluşturuldu');
                this.close.emit(true);
            },
            error: (err) => {
                console.error('Create permission failed', err);
                if (err.error?.code === 'Permission.Exists') {
                    this.toaster.error('Bu anahtara (key) sahip bir izin zaten mevcut.');
                } else {
                    this.toaster.error('İzin oluşturulamadı');
                }
                this.loading.set(false);
            }
        });
    }

    onCancel() {
        this.close.emit(false);
    }
}
