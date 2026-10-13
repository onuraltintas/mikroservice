import { Component } from '@angular/core';
import { CoachingFinanceComponent } from '../../coaching-portal/pages/coaching-finance.component';

@Component({ selector: 'app-coaching-admin-finance', standalone: true, imports: [CoachingFinanceComponent],
  template: `<h1 class="mb-4 text-2xl font-bold">Koçluk ücret ve tahsilat denetimi</h1><app-coaching-finance mode="admin" [cashView]="true" />` })
export class CoachingAdminFinanceComponent { }
