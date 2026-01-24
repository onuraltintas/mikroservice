import { Component, EventEmitter, Output } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule, MatDividerModule, RouterLink],
  templateUrl: './toolbar.html',
  styleUrl: './toolbar.scss'
})
export class ToolbarComponent {
  @Output() menuToggle = new EventEmitter<void>();
}
