import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { TeachersListComponent } from './teachers-list.component';
import { TeachersService } from '../../../core/services/teachers.service';
import { ToasterService } from '../../../core/services/toaster.service';

describe('TeachersListComponent', () => {
  let getTeachersPage: jasmine.Spy;

  beforeEach(() => {
    getTeachersPage = jasmine.createSpy('getTeachersPage').and.returnValue(of({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25, totalPages: 0,
      hasPreviousPage: false, hasNextPage: false
    }));
    TestBed.configureTestingModule({
      imports: [TeachersListComponent],
      providers: [
        { provide: TeachersService, useValue: { getTeachersPage } },
        { provide: ToasterService, useValue: { error: jasmine.createSpy('error'), success: jasmine.createSpy('success'), confirm: jasmine.createSpy('confirm') } },
        { provide: MatDialog, useValue: { open: jasmine.createSpy('open') } }
      ]
    });
  });

  it('requests a paged server-side teacher roster and stores pagination metadata', () => {
    getTeachersPage.and.returnValue(of({
      items: [{ id: 'teacher-1', firstName: 'Ada', lastName: 'Yılmaz', email: 'ada@example.test', isActive: true }],
      totalCount: 61, pageNumber: 2, pageSize: 25, totalPages: 3,
      hasPreviousPage: true, hasNextPage: true
    }));
    const component = TestBed.createComponent(TeachersListComponent).componentInstance;

    component.loadTeachers(1);

    expect(getTeachersPage).toHaveBeenCalledWith(2, 25, undefined, undefined, undefined);
    expect(component.dataSource.data).toHaveSize(1);
    expect(component.totalCount).toBe(61);
    expect(component.pageIndex).toBe(1);
    expect(component.pageSize).toBe(25);
  });

  it('resets to page one when status filters change and requests the selected server page', () => {
    const component = TestBed.createComponent(TeachersListComponent).componentInstance;
    component.pageIndex = 4;

    component.onStatusChange(false);
    component.onPageChange({ pageIndex: 2, pageSize: 50, length: 150 });

    expect(getTeachersPage.calls.allArgs()).toEqual([
      [1, 25, undefined, undefined, false],
      [3, 50, undefined, undefined, false]
    ]);
  });

  it('debounces server-side name and email search and resets the page', fakeAsync(() => {
    const component = TestBed.createComponent(TeachersListComponent).componentInstance;
    component.pageIndex = 3;
    component.onSearchChange({ target: { value: '  ada@example.test  ' } } as unknown as Event);

    tick(299);
    expect(getTeachersPage).not.toHaveBeenCalled();
    tick(1);

    expect(getTeachersPage).toHaveBeenCalledOnceWith(1, 25, 'ada@example.test', undefined, undefined);
  }));
});
