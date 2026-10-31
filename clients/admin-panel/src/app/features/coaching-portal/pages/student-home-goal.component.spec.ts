import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { StudentHomeGoalComponent } from './student-home-goal.component';
describe('home goal summary',()=>{
 async function setup(school:boolean,fail=false){
  const api={getPrimaryGoal:()=>of({goalId:'g'}),getGoalTarget:vi.fn(()=>fail?throwError(()=>new Error('offline')):of({targetSchoolId:school?'s':null,targetUniversityProgramId:school?null:'p',catalogTarget:{name:school?'Okulum':'Program'},netTargets:[]}))};
  TestBed.configureTestingModule({imports:[StudentHomeGoalComponent],providers:[provideRouter([]),{provide:CoachingStudyPlanningService,useValue:api},{provide:CoachingLearningSetupService,useValue:{get:()=>of({context:{purpose:'Lgs'}})}}]});
  const fixture=TestBed.createComponent(StudentHomeGoalComponent);fixture.detectChanges();await fixture.whenStable();return fixture;
 }
 it('shows the main goal without fetching unrelated student records',async()=>{const f=await setup(true);expect(f.nativeElement.textContent).toContain('Okulum');expect(f.nativeElement.querySelector('a').getAttribute('href')).toBe('/coaching-portal/growth/goals');});
 it('does not show an old university goal in an LGS context',async()=>{const f=await setup(false);expect(f.componentInstance.target()).toBeNull();expect(f.nativeElement.textContent).not.toContain('Program');});
 it('offers retry without disguising a loading error as an empty target',async()=>{const f=await setup(true,true);expect(f.componentInstance.error()).toBe(true);expect(f.nativeElement.textContent).toContain('Yeniden dene');});
});
