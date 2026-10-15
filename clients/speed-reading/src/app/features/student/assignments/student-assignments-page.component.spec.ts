import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { Subject } from 'rxjs';
import { Router } from '@angular/router';
import { AssignmentDto, AssignmentService } from '../../../core/services/assignment.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { StudentAssignmentsPageComponent } from './student-assignments-page.component';

describe('StudentAssignmentsPageComponent', () => {
  function create(): StudentAssignmentsPageComponent {
    TestBed.configureTestingModule({
      imports: [StudentAssignmentsPageComponent],
      providers: [
        { provide: AssignmentService, useValue: {} },
        { provide: Router, useValue: {} },
        { provide: ToasterService, useValue: {} }
      ]
    });
    return TestBed.createComponent(StudentAssignmentsPageComponent).componentInstance;
  }

  it('averages only completed assignments with a recorded score', () => {
    const component = create();
    component.assignments = [
      { isCompleted: true, score: 80 },
      { isCompleted: true, score: 100 },
      { isCompleted: true },
      { isCompleted: false, score: 0 }
    ] as AssignmentDto[];

    component.calculateStats();

    expect(component.stats.averageScore).toBe(90);
  });

  it('does not present missing scores as zero achievement', () => {
    const component = create();
    component.assignments = [{ isCompleted: true }] as AssignmentDto[];

    component.calculateStats();

    expect(component.stats.averageScore).toBeNull();
  });

  for (const completed of [false, true]) {
    it(`renders an asynchronous ${completed ? 'completed' : 'pending'} assignment without a click`, async () => {
      const response = new Subject<AssignmentDto[]>();
      TestBed.configureTestingModule({
        imports: [StudentAssignmentsPageComponent],
        providers: [
          provideZonelessChangeDetection(),
          { provide: AssignmentService, useValue: { getMyAssignments: () => response } },
          { provide: Router, useValue: {} },
          { provide: ToasterService, useValue: {} },
        ],
      });
      const fixture = TestBed.createComponent(StudentAssignmentsPageComponent);
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('Ödevler yükleniyor');

      response.next([{
        id: 'assignment-1', studentAssignmentId: 'student-assignment-1', title: 'Canlı ödev',
        description: '', exerciseTitle: 'Takip', exerciseId: 'exercise-1',
        dueDate: '2026-10-16T20:59:59Z', isCompleted: completed,
      }]);
      await fixture.whenStable();

      expect(fixture.nativeElement.textContent).not.toContain('Ödevler yükleniyor');
      expect(fixture.nativeElement.textContent).toContain('Canlı ödev');
      expect(fixture.nativeElement.querySelector('[role="tab"][aria-selected="true"]').textContent)
        .toContain(completed ? 'Tamamlananlar' : 'Bekleyenler');
    });
  }

  it('clears the loading view after an asynchronous failure without a click', async () => {
    const response = new Subject<AssignmentDto[]>();
    const toaster = jasmine.createSpyObj('ToasterService', ['error']);
    spyOn(console, 'error');
    TestBed.configureTestingModule({
      imports: [StudentAssignmentsPageComponent],
      providers: [
        provideZonelessChangeDetection(),
        { provide: AssignmentService, useValue: { getMyAssignments: () => response } },
        { provide: Router, useValue: {} },
        { provide: ToasterService, useValue: toaster },
      ],
    });
    const fixture = TestBed.createComponent(StudentAssignmentsPageComponent);
    fixture.detectChanges();
    response.error(new Error('Offline'));
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).not.toContain('Ödevler yükleniyor');
    expect(toaster.error).toHaveBeenCalled();
  });
});
