import { Component, EventEmitter, inject, Input, OnInit, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService, RoleDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-edit-role-modal',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    templateUrl: './edit-role-modal.html'
})
export class EditRoleModalComponent implements OnInit {
    @Input() role!: RoleDto;
    @Output() close = new EventEmitter<boolean>();

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

    ngOnInit() {
        if (this.role) {
            this.form.patchValue({
                name: this.role.name,
                description: this.role.description
            });

            // System roles cannot change name
            if (this.role.isSystemRole) {
                this.form.get('name')?.disable();
            }
        }
    }

    onSubmit() {
        if (this.form.invalid) return;

        this.loading.set(true);
        // If name is disabled, we need to manually add it to the request or ensure backend handles it.
        // ReactiveForms doesn't include disabled fields in value.
        const requestData = this.form.getRawValue();

        this.identityService.updateRole(this.role.id, requestData).subscribe({
            next: () => {
                this.toaster.success('Rol başarıyla güncellendi');
                this.close.emit(true);
            },
            error: (err) => {
                console.error('Update role failed', err);
                this.toaster.error('Rol güncellenemedi');
                this.loading.set(false);
            }
        });
    }

    onCancel() {
        this.close.emit(false);
    }
}
