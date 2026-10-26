import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CoachingTeacherAssignmentFollowUpComponent} from './coaching-teacher-assignment-follow-up.component';
describe('teacher assignment follow up',()=>{
 function setup(){TestBed.configureTestingModule({imports:[CoachingTeacherAssignmentFollowUpComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});const f=TestBed.createComponent(CoachingTeacherAssignmentFollowUpComponent);f.detectChanges();return {f,c:f.componentInstance,http:TestBed.inject(HttpTestingController)};}
 const page={items:[{assignmentId:'a',studentId:'s',studentName:'Ayşe Test',title:'Matematik tekrar',subject:'Matematik',dueDate:'2027-01-04T12:00:00Z',status:'Assigned',isOverdue:true,overdueMinutes:1440}],totalCount:26,pageNumber:1,pageSize:25,pendingCount:30,overdueCount:26};
 it('shows server totals and waiting rows, filters from page one and opens the selected assignment',()=>{
  const {f,c,http}=setup();http.expectOne(r=>r.url.endsWith('/assignment-follow-up')&&r.params.get('mode')==='overdue').flush(page);f.detectChanges();expect(f.nativeElement.textContent).toContain('Ayşe Test');expect(f.nativeElement.textContent).toContain('Matematik tekrar');expect(f.nativeElement.textContent).toContain('30');
  const open=vi.spyOn(c.openAssignment,'emit');(f.nativeElement.querySelector('tbody button') as HTMLButtonElement).click();expect(open).toHaveBeenCalledWith('a');c.nextPage();http.expectOne(r=>r.params.get('pageNumber')==='2').flush({...page,pageNumber:2});c.setMode('pending');http.expectOne(r=>r.params.get('mode')==='pending'&&r.params.get('pageNumber')==='1').flush({...page,items:[],totalCount:0});
  http.verify();
 });
 it('does not show stale rows on request failure and supports retry',async ()=>{const {f,c,http}=setup();http.expectOne(r=>r.url.endsWith('/assignment-follow-up')).flush(page);(await c.load());http.expectOne(r=>r.url.endsWith('/assignment-follow-up')).flush({},{status:500,statusText:'Error'});f.detectChanges();expect(f.nativeElement.querySelector('[role="alert"]')).not.toBeNull();expect(f.nativeElement.textContent).not.toContain('Ayşe Test');(await c.load());http.expectOne(r=>r.url.endsWith('/assignment-follow-up')).flush({...page,items:[],totalCount:0});f.detectChanges();expect(f.nativeElement.textContent).toContain('Kayıt bulunamadı');http.verify();});
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
