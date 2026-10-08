import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ExerciseLevelDialogComponent } from './exercise-level-dialog.component';

describe('Exercise level picker age groups', () => {
    it('renders API age groups and filters the records selected by the administrator', async () => {
        const exercises = ['Çocuk', 'Genç', 'Yetişkin', 'Genç yetişkin'].map((name, index) => ({
            id: `exercise-${index}`, title: name, difficultyLevel: 1, exerciseTypeId: 'type',
            targetAgeGroupConfigurationId: `10000000-0000-0000-0000-00000000000${index + 1}`,
            configurationJson: '{}'
        }));
        await TestBed.configureTestingModule({
            imports: [ExerciseLevelDialogComponent],
            providers: [
                { provide: MAT_DIALOG_DATA, useValue: { title: 'Schulte', exercises } },
                { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } }
            ]
        }).compileComponents();
        const fixture = TestBed.createComponent(ExerciseLevelDialogComponent);
        fixture.detectChanges();
        const element: HTMLElement = fixture.nativeElement;
        expect(Array.from(element.querySelectorAll('.age-badge')).map(badge => badge.textContent?.trim()))
            .toEqual(['Çocuk', 'Genç', 'Yetişkin', 'Genç yetişkin']);
        const filter = element.querySelector('select')!;
        filter.value = exercises[0].targetAgeGroupConfigurationId;
        filter.dispatchEvent(new Event('change'));
        fixture.detectChanges();
        expect(element.querySelectorAll('.level-item').length).toBe(1);
        expect(element.querySelector('.age-badge')?.textContent?.trim()).toBe('Çocuk');
        element.querySelector<HTMLElement>('.level-item')!.click();
        expect(TestBed.inject(MatDialogRef).close).toHaveBeenCalledWith(exercises[0]);
    });
});
