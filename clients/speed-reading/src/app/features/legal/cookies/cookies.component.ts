import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
    selector: 'app-cookies',
    standalone: true,
    imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, NavbarComponent, FooterComponent],
    templateUrl: './cookies.component.html',
    styleUrls: ['./cookies.component.scss']
})
export class CookiesComponent {
    currentDate = new Date();
    content: string = '';
    title = 'Çerez Politikası';
    loading = false;
    error = false;
    noDocument = true;

}
