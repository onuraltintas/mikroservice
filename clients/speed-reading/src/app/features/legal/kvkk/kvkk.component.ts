import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
  selector: 'app-kvkk',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule, NavbarComponent, FooterComponent],
  templateUrl: './kvkk.component.html',
  styleUrls: ['./kvkk.component.scss']
})
export class KvkkComponent {
  currentDate = new Date();
  content: string = '';
  title = 'KVKK Aydınlatma Metni';
  loading = false;
  error = false;
  noDocument = true;
}
