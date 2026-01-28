import { Component, EventEmitter, inject, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-create-role-modal',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    templateUrl: './create-role-modal.html'
})
export class CreateRoleModalComponent {
    @Output() close = new EventEmitter<boolean>(); // true if saved

    private fb = inject(FormBuilder);
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);

    form: FormGroup;
    loading = signal(false);

    constructor() {
        this.form = this.fb.group({
            name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
            description: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]]
        });
    }

    onSubmit() {
        if (this.form.invalid) return;

        this.loading.set(true);
        this.identityService.createRole(this.form.value).subscribe({
            next: () => {
                this.toaster.success('Rol başarıyla oluşturuldu');
                this.close.emit(true);
            },
            error: (err) => {
                console.error('Create role failed', err);
                if (err.error?.code === 'Role.Exists') {
                    this.toaster.error('Bu isimde bir rol zaten mevcut.');
                } else {
                    this.toaster.error('Rol oluşturulamadı');
                }
                this.loading.set(false);
            }
        });
    }

    onCancel() {
        this.close.emit(false);
    }
}
