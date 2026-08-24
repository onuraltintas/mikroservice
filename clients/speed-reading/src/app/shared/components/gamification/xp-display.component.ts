import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { trigger, transition, style, animate } from '@angular/animations';

@Component({
  selector: 'app-xp-display',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './xp-display.component.html',
  styleUrls: ['./xp-display.component.scss'],
  animations: [
    trigger('floatUp', [
      transition(':enter', [
        style({ transform: 'translateY(0)', opacity: 1 }),
        animate('2s ease-out', style({ transform: 'translateY(-50px)', opacity: 0 }))
      ])
    ])
  ]
})
export class XPDisplayComponent {
  @Input() currentXP: number = 0;
  @Input() gainedXP: number = 0;
  @Input() showGainAnimation: boolean = false;
}
