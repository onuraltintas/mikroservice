import { Component, EventEmitter, inject, Input, OnInit, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService, PermissionDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-edit-permission-modal',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    templateUrl: './edit-permission-modal.html'
})
export class EditPermissionModalComponent implements OnInit {
    @Input() permission: PermissionDto | null = null;
    @Output() close = new EventEmitter<boolean>();

    private fb = inject(FormBuilder);
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);

    form: FormGroup;
    loading = signal(false);

    constructor() {
        this.form = this.fb.group({
            description: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
            group: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]]
        });
    }

    ngOnInit() {
        if (this.permission) {
            this.form.patchValue({
                description: this.permission.description,
                group: this.permission.group
            });
            // System permissions might restrict editing some fields in future, but for now we allow description/group edit.
        }
    }

    onSubmit() {
        if (this.form.invalid || !this.permission) return;

        this.loading.set(true);
        this.identityService.updatePermission(this.permission.id, this.form.value).subscribe({
            next: () => {
                this.toaster.success('İzin güncellendi');
                this.close.emit(true);
            },
            error: (err) => {
                console.error('Update permission failed', err);
                this.toaster.error('İzin güncellenemedi');
                this.loading.set(false);
            }
        });
    }

    onCancel() {
        this.close.emit(false);
    }
}
