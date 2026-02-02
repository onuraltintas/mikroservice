import { Component, OnInit, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';
import { ConfigurationService } from '../../../core/services/settings/configuration.service';
import { catchError, of } from 'rxjs';

@Component({
    selector: 'app-register',
    standalone: true,
    imports: [CommonModule, RouterLink, MatIconModule],
    templateUrl: './register.component.html',
    styleUrl: './register.component.scss'
})
export class RegisterComponent implements OnInit {
    private configService = inject(ConfigurationService);

    isRegistrationAllowed = signal(true); // Default assumed true until checked
    isLoading = signal(true);

    ngOnInit() {
        this.configService.getPublicConfigurationValue('auth.allowregistration')
            .pipe(catchError(() => of('true'))) // Fallback to true if config fails to minimize disruption
            .subscribe(val => {
                const cleanVal = val?.replace(/"/g, '').trim().toLowerCase();
                this.isRegistrationAllowed.set(cleanVal === 'true');
                this.isLoading.set(false);
            });
    }
}
