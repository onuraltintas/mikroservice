import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { RouterLink } from '@angular/router';

// Material
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSelectModule } from '@angular/material/select';
import { SupportService } from '../../../core/services/support.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { inject } from '@angular/core';

@Component({
    selector: 'app-support',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        RouterLink,
        MatIconModule,
        MatButtonModule,
        MatInputModule,
        MatFormFieldModule,
        MatProgressSpinnerModule,
        MatExpansionModule,
        MatSelectModule
    ],
    templateUrl: './support.component.html',
    styleUrl: './support.component.scss'
})
export class SupportComponent {
    supportForm: FormGroup;
    isLoading = signal(false);
    isSubmitted = signal(false);

    faqs = [
        {
            question: 'Hesabım neden pasif durumda görünüyor?',
            answer: 'Hesabınız güvenlik politikaları, abonelik durumu veya kurum yöneticiniz tarafından yapılan bir güncelleme nedeniyle pasif duruma getirilmiş olabilir. Bu durum genellikle idari bir işlemdir.'
        },
        {
            question: 'Nasıl tekrar aktif olabilirim?',
            answer: 'Eğer bir kurum bünyesindeyseniz, öncelikle kurum yöneticinizle görüşmeniz önerilir. Bireysel kullanıcıysanız, aşağıdaki destek formunu doldurarak bizimle iletişime geçebilirsiniz.'
        },
        {
            question: 'Doğrulama e-postası gelmedi, ne yapmalıyım?',
            answer: 'Giriş ekranında "Tekrar doğrulama e-postası gönder" butonunu kullanabilirsiniz. Lütfen Spam/Gereksiz klasörünü de kontrol etmeyi unutmayın.'
        }
    ];

    private supportService = inject(SupportService);
    private toaster = inject(ToasterService);

    constructor(private fb: FormBuilder) {
        this.supportForm = this.fb.group({
            firstName: ['', Validators.required],
            lastName: ['', Validators.required],
            email: ['', [Validators.required, Validators.email]],
            subject: ['Hesap Sorunları', Validators.required],
            message: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(1000)]]
        });
    }

    onSubmit() {
        if (this.supportForm.valid) {
            this.isLoading.set(true);
            this.supportService.submitRequest(this.supportForm.value).subscribe({
                next: () => {
                    this.isLoading.set(false);
                    this.isSubmitted.set(true);
                    this.supportForm.reset();
                    this.toaster.success('Destek talebiniz başarıyla alındı.');
                },
                error: (err) => {
                    this.isLoading.set(false);
                    this.toaster.error('Talebiniz gönderilirken bir hata oluştu.');
                    console.error('Support submission error', err);
                }
            });
        }
    }
}
