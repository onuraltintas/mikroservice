import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CoachingTeacherStudentProfileComponent} from './coaching-teacher-student-profile.component';
describe('teacher student profile',()=>{
 const url='/api/coaching/teachers/me/students/student/profile',profile={version:3,educationStatus:'Student',gradeLevel:8,schoolName:'Eski okul',schoolCity:'Ankara',schoolDistrict:null,schoolType:null,classBranch:'A',birthDate:null,bio:null};
 async function setup(){TestBed.configureTestingModule({imports:[CoachingTeacherStudentProfileComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});const f=TestBed.createComponent(CoachingTeacherStudentProfileComponent);f.componentRef.setInput('studentId','student');f.detectChanges();const http=TestBed.inject(HttpTestingController);http.expectOne(url).flush(profile);f.detectChanges();await f.whenStable();return{f,c:f.componentInstance,http};}
 it('shows full profile and saves only permitted fields with the read version',async()=>{
  const {f,c,http}=await setup();expect(f.nativeElement.textContent).toContain('8');c.editing=true;c.form!.schoolName='Yeni okul';c.dirty=true;c.save();const req=http.expectOne(url);expect(req.request.method).toBe('PUT');expect(req.request.body.expectedVersion).toBe(3);expect(req.request.body.schoolName).toBe('Yeni okul');expect(req.request.body.gradeLevel).toBeUndefined();expect(req.request.body.shareProgressWithTeachers).toBeUndefined();req.flush({...profile,version:4,schoolName:'Yeni okul'});expect(c.dirty).toBe(false);expect(c.form?.version).toBe(4);http.verify();
 });
 it('keeps dirty edits on conflict and guards navigation',async()=>{
  const {c,http}=await setup();c.editing=true;c.form!.schoolName='Korunacak';c.dirty=true;vi.mocked(TestBed.inject(ToasterService).confirm).mockResolvedValue(false);expect(await c.canLeavePage()).toBe(false);c.save();http.expectOne(url).flush({message:'Profil değişti'},{status:409,statusText:'Conflict'});expect(c.form?.schoolName).toBe('Korunacak');expect(c.error()).toContain('Profil değişti');expect(c.dirty).toBe(true);http.verify();
 });
});

beforeEach(() => TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn()}}));
