import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import confetti from 'canvas-confetti';

export interface CelebrationData {
    title: string;
    message: string;
    type: 'week' | 'level' | 'program';
    xpEarned?: number;
}

@Component({
    selector: 'app-celebration-modal',
    standalone: true,
    imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
    templateUrl: './celebration-modal.component.html',
    styleUrls: ['./celebration-modal.component.scss']
})
export class CelebrationModalComponent implements OnInit {
    constructor(
        public dialogRef: MatDialogRef<CelebrationModalComponent>,
        @Inject(MAT_DIALOG_DATA) public data: CelebrationData
    ) { }

    ngOnInit() {
        this.launchConfetti();
    }

    launchConfetti() {
        const duration = 3000;
        const end = Date.now() + duration;

        (function frame() {
            confetti({
                particleCount: 7,
                angle: 60,
                spread: 55,
                origin: { x: 0 },
                colors: ['#FFD700', '#FFA500', '#FF4500'] // Gold, Orange, Red
            });
            confetti({
                particleCount: 7,
                angle: 120,
                spread: 55,
                origin: { x: 1 },
                colors: ['#00BFFF', '#1E90FF', '#4169E1'] // Blue shades
            });

            if (Date.now() < end) {
                requestAnimationFrame(frame);
            }
        }());
    }

    close() {
        this.dialogRef.close();
    }
}
