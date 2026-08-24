import { Injectable, inject } from '@angular/core';
import { ToastrService, IndividualConfig } from 'ngx-toastr';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmationDialogComponent, ConfirmationDialogData } from '../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { firstValueFrom } from 'rxjs';

export enum ToastType {
  Success = 'success',
  Error = 'error',
  Warning = 'warning',
  Info = 'info'
}

@Injectable({
  providedIn: 'root'
})
export class ToasterService {
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);

  private readonly defaultConfig: Partial<IndividualConfig> = {
    positionClass: 'toast-top-right',
    timeOut: 4000,
    closeButton: true,
    progressBar: true,
    enableHtml: false,
    tapToDismiss: true
  };

  success(message: string, duration: number = 4000, title: string = 'Başarılı'): void {
    this.toastr.success(message, title, {
      ...this.defaultConfig,
      timeOut: duration
    });
  }

  error(message: string, duration: number = 5000, title: string = 'Hata'): void {
    this.toastr.error(message, title, {
      ...this.defaultConfig,
      timeOut: duration
    });
  }

  warning(message: string, duration: number = 4500, title: string = 'Uyarı'): void {
    this.toastr.warning(message, title, {
      ...this.defaultConfig,
      timeOut: duration
    });
  }

  info(message: string, duration: number = 3500, title: string = 'Bilgi'): void {
    this.toastr.info(message, title, {
      ...this.defaultConfig,
      timeOut: duration
    });
  }

  // Dismiss all toasts
  dismiss(): void {
    this.toastr.clear();
  }

  // Confirmation dialog using Material Dialog
  async confirm(message: string, title: string = 'Onay', confirmText?: string, cancelText?: string): Promise<boolean> {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      maxWidth: '500px',
      panelClass: 'custom-dialog-container',
      autoFocus: false,
      disableClose: false,
      data: {
        title,
        message,
        confirmText,
        cancelText
      } as ConfirmationDialogData
    });

    const result = await firstValueFrom(dialogRef.afterClosed());
    return result === true;
  }

  // Alert dialog using toaster
  alert(message: string, title: string = 'Bilgi'): void {
    this.info(message, 5000, title);
  }
}
