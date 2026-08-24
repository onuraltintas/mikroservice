import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule } from '@angular/forms';
import { BackupService, BackupInfo, ScheduleConfig } from '../../../../core/services/backup.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-backup-list',
    standalone: true,
    imports: [
        CommonModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatTableModule,
        MatTooltipModule,
        MatProgressBarModule,
        MatExpansionModule,
        MatSlideToggleModule,
        MatInputModule,
        MatFormFieldModule,
        MatSelectModule,
        FormsModule
    ],
    templateUrl: './backup-list.component.html',
    styleUrls: ['./backup-list.component.scss']
})
export class BackupListComponent implements OnInit {
    private backupService = inject(BackupService);
    private toaster = inject(ToasterService);

    backups: BackupInfo[] = [];
    displayedColumns = ['fileName', 'createdAt', 'size', 'actions'];
    loading = false;
    creating = false;

    // Settings
    configLoading = false;
    scheduleConfig: ScheduleConfig = { enabled: false, cronExpression: '0 3 * * *' };
    scheduleTime: string = '03:00'; // Default text time for input

    ngOnInit() {
        this.loadBackups();
        this.loadSchedule();
    }

    cronToTime(cron: string): string {
        // Basic parser for "min hour * * *" format
        try {
            if (!cron) return '03:00';
            const parts = cron.split(' ');
            if (parts.length < 2) return '03:00';

            let min = parts[0];
            let hour = parts[1];

            // Pad with zeros for time input
            if (min.length === 1) min = '0' + min;
            if (hour.length === 1) hour = '0' + hour;

            return `${hour}:${min}`;
        } catch {
            return '03:00';
        }
    }

    timeToCron(time: string): string {
        // Converts "HH:mm" to "m H * * *"
        if (!time) return '0 3 * * *';
        const parts = time.split(':');
        const hour = parseInt(parts[0]);
        const min = parseInt(parts[1]);
        return `${min} ${hour} * * *`;
    }

    loadBackups() {
        this.loading = true;
        this.backupService.getBackups().subscribe({
            next: (data) => {
                this.backups = data;
                this.loading = false;
            },
            error: (err) => {
                this.toaster.error('Yedekler yüklenirken hata oluştu');
                this.loading = false;
            }
        });
    }

    loadSchedule() {
        this.configLoading = true;
        this.backupService.getSchedule().subscribe({
            next: (config) => {
                this.scheduleConfig = config;
                this.scheduleTime = this.cronToTime(config.cronExpression);
                this.configLoading = false;
            },
            error: () => this.configLoading = false
        });
    }

    saveSchedule() {
        this.configLoading = true;
        // Convert UI time to Cron
        this.scheduleConfig.cronExpression = this.timeToCron(this.scheduleTime);

        this.backupService.configureSchedule(this.scheduleConfig).subscribe({
            next: () => {
                this.toaster.success('Yedekleme ayarları kaydedildi');
                this.configLoading = false;
            },
            error: (err) => {
                this.toaster.error('Ayarlar kaydedilemedi: ' + (err.error?.message || err.message));
                this.configLoading = false;
            }
        });
    }

    createBackup() {
        this.creating = true;
        this.backupService.createBackup().subscribe({
            next: (res) => {
                this.toaster.success('Yedek başarıyla oluşturuldu');
                this.loadBackups();
                this.creating = false;
            },
            error: (err) => {
                this.toaster.error('Yedek oluşturulamadı: ' + (err.error?.message || err.message));
                this.creating = false;
            }
        });
    }

    async deleteBackup(fileName: string) {
        const confirmed = await this.toaster.confirm(
            'Bu yedeği silmek istediğinize emin misiniz? Bu işlem geri alınamaz.',
            'Yedeği Sil'
        );

        if (!confirmed) return;

        this.loading = true; // Show loading while deleting
        this.backupService.deleteBackup(fileName).subscribe({
            next: () => {
                this.toaster.success('Yedek başarıyla silindi');
                this.loadBackups();
            },
            error: (err) => {
                this.toaster.error('Silme başarısız: ' + (err.error?.message || err.message));
                this.loading = false;
            }
        });
    }

    downloadBackup(fileName: string) {
        const url = this.backupService.downloadUrl(fileName);
        const token = localStorage.getItem('token');

        this.loading = true;

        fetch(url, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        })
            .then(response => {
                if (!response.ok) throw new Error('Download failed');
                return response.blob();
            })
            .then(blob => {
                const downloadUrl = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = downloadUrl;
                link.download = fileName;
                link.click();
                window.URL.revokeObjectURL(downloadUrl);
                this.loading = false;
            })
            .catch(() => {
                this.toaster.error('İndirme başarısız');
                this.loading = false;
            });
    }
}
