import { Component, Input } from '@angular/core';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentPrimaryGoalComponent } from './student-primary-goal.component';
import { StudentGoalTargetComponent } from './student-goal-target.component';
@Component({selector:'app-student-goal-target',standalone:true,template:''})
class Picker { @Input() goalId='';@Input() context:any;@Input() autoOpen=false; }

describe('StudentPrimaryGoalComponent',()=>{
 async function setup(fail=false){
  const api={getPrimaryGoal:vi.fn(()=>of({goalId:'lgs',version:3})),getGoalTarget:vi.fn((id:string)=>of({goalId:id,version:0,canEdit:true,targetSchoolId:id==='lgs'?'school':null,targetUniversityProgramId:id==='yks'?'program':null})),selectPrimaryGoal:vi.fn(()=>of({goalId:'lgs',version:4}))};
  const goals={getStudentGoals:vi.fn(()=>fail?throwError(()=>new Error('offline')):of({items:[{id:'lgs',title:'Okulum',category:'ExamPreparation'},{id:'yks',title:'Program',category:'ExamPreparation'}],pageNumber:1,pageSize:100,totalCount:2}))};
  TestBed.configureTestingModule({imports:[StudentPrimaryGoalComponent],providers:[provideRouter([]),{provide:AuthService,useValue:{userProfile:signal({id:'student'})}},{provide:CoachingStudyPlanningService,useValue:api},{provide:CoachingPortalService,useValue:goals},{provide:CoachingLearningSetupService,useValue:{get:()=>of({context:{purpose:'Lgs'}})}}]});
  TestBed.overrideComponent(StudentPrimaryGoalComponent,{remove:{imports:[StudentGoalTargetComponent]},add:{imports:[Picker]}});
  const fixture=TestBed.createComponent(StudentPrimaryGoalComponent);fixture.detectChanges();await fixture.whenStable();return {fixture,component:fixture.componentInstance,api};
 }
 it('opens a dedicated page and hides incompatible old goals without deleting them',async()=>{const {fixture,component,api}=await setup();expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Hedefim');expect(component.choices().map(x=>x.id)).toEqual(['lgs']);expect(component.current()?.id).toBe('lgs');expect(api.selectPrimaryGoal).not.toHaveBeenCalled();});
 it('persists the choice with its expected version',async()=>{const {component,api}=await setup();component.select('lgs');expect(api.selectPrimaryGoal).toHaveBeenCalledWith('lgs',3);});
 it('shows a retry on loading failure without enabling target creation',async()=>{const {component}=await setup(true);expect(component.ready()).toBe(false);expect(component.error()).toContain('yüklenemedi');});
});
