import { Injectable } from '@angular/core';
import Swal from 'sweetalert2';

@Injectable({
    providedIn: 'root'
})
export class ToasterService {

    // ToastMixin: SweetAlert2'nin "Toast" modu ayarları
    private Toast = Swal.mixin({
        toast: true,
        position: 'top-end',
        showConfirmButton: false,
        timer: 3000,
        timerProgressBar: true,
        didOpen: (toast) => {
            toast.onmouseenter = Swal.stopTimer;
            toast.onmouseleave = Swal.resumeTimer;
        },
        customClass: {
            popup: 'colored-toast' // İlerde CSS ile özelleştirmek istersek
        }
    });

    /**
     * Başarılı işlem bildirimi (Yeşil Tik Animasyonu)
     */
    success(message: string, title?: string) {
        this.Toast.fire({
            icon: 'success',
            title: title || message,
            text: title ? message : undefined
        });
    }

    /**
     * Hata bildirimi (Kırmızı Çarpı Animasyonu)
     */
    error(message: string, title?: string) {
        this.Toast.fire({
            icon: 'error',
            title: title || message,
            text: title ? message : undefined
        });
    }

    /**
     * Uyarı bildirimi (Turuncu Ünlem)
     */
    warning(message: string, title?: string) {
        this.Toast.fire({
            icon: 'warning',
            title: title || message,
            text: title ? message : undefined
        });
    }

    /**
     * Bilgi bildirimi (Mavi İkon)
     */
    info(message: string, title?: string) {
        this.Toast.fire({
            icon: 'info',
            title: title || message,
            text: title ? message : undefined
        });
    }

    /**
     * Özel Onay Penceresi (Örn: Silmek istediğinize emin misiniz?)
     */
    confirm(title: string, text: string, confirmButtonText: string = 'Evet', cancelButtonText: string = 'İptal'): Promise<boolean> {
        return Swal.fire({
            title: title,
            text: text,
            icon: 'question',
            showCancelButton: true,
            confirmButtonColor: '#3085d6',
            cancelButtonColor: '#d33',
            confirmButtonText: confirmButtonText,
            cancelButtonText: cancelButtonText,
            heightAuto: false
        }).then((result) => {
            return result.isConfirmed;
        });
    }
}
