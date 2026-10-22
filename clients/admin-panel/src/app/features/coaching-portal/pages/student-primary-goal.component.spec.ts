import { Component, Input } from '@angular/core';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
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
  const goals={createStudentGoal:vi.fn(()=>of({goalId:'new'})),getStudentGoals:vi.fn(()=>fail?throwError(()=>new Error('offline')):of({items:[{id:'lgs',title:'Okulum',category:'ExamPreparation'},{id:'yks',title:'Program',category:'ExamPreparation'}],pageNumber:1,pageSize:100,totalCount:2}))};
  TestBed.configureTestingModule({imports:[StudentPrimaryGoalComponent],providers:[provideRouter([]),{provide:AuthService,useValue:{userProfile:signal({id:'student'})}},{provide:CoachingStudyPlanningService,useValue:api},{provide:CoachingPortalService,useValue:goals},{provide:CoachingLearningSetupService,useValue:{get:()=>of({context:{purpose:'Lgs'}})}}]});
  TestBed.overrideComponent(StudentPrimaryGoalComponent,{remove:{imports:[StudentGoalTargetComponent]},add:{imports:[Picker]}});
  const fixture=TestBed.createComponent(StudentPrimaryGoalComponent);fixture.detectChanges();await fixture.whenStable();return {fixture,component:fixture.componentInstance,api,goals};
 }
 it('opens a dedicated page and hides incompatible old goals without deleting them',async()=>{const {fixture,component,api}=await setup();expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Hedefim');expect(component.choices().map(x=>x.id)).toEqual(['lgs']);expect(component.current()?.id).toBe('lgs');expect(api.selectPrimaryGoal).not.toHaveBeenCalled();});
 it('persists the choice with its expected version',async()=>{const {component,api}=await setup();component.select('lgs');expect(api.selectPrimaryGoal).toHaveBeenCalledWith('lgs',3);});
 it('shows a retry on loading failure without enabling target creation',async()=>{const {component}=await setup(true);expect(component.ready()).toBe(false);expect(component.error()).toContain('yüklenemedi');});
 it('creates one goal and saves its selection without replacing old choices',async()=>{
  const {component,api,goals}=await setup();component.create();expect(goals.createStudentGoal).toHaveBeenCalledTimes(1);expect(api.selectPrimaryGoal).toHaveBeenCalledWith('new',3);expect(component.current()?.id).toBe('new');expect(component.choices().map(x=>x.id)).toEqual(['lgs','new']);
 });
 it('preserves the current goal and unlocks the form after a selection conflict',async()=>{
  const {component,api}=await setup();api.selectPrimaryGoal.mockReturnValueOnce(throwError(()=>({error:{message:'Yeniden yükle'}})));component.select('lgs');expect(component.current()?.id).toBe('lgs');expect(component.error()).toContain('Yeniden yükle');expect(component.saving()).toBe(false);
 });
 it('does not create more goals or leave while a write is pending',async()=>{
  const {component,api,goals}=await setup();const pending=new Subject<any>();api.selectPrimaryGoal.mockReturnValueOnce(pending);component.select('lgs');expect(component.canLeavePage()).toBe(false);component.create();component.select('lgs');component.load();expect(goals.createStudentGoal).not.toHaveBeenCalled();expect(api.selectPrimaryGoal).toHaveBeenCalledTimes(1);pending.next({goalId:'lgs',version:4});pending.complete();expect(component.canLeavePage()).toBe(true);
 });
 it('keeps existing choices when a new goal request fails',async()=>{
  const {component,goals}=await setup();goals.createStudentGoal.mockReturnValueOnce(throwError(()=>new Error('offline')));component.create();expect(component.choices()).toHaveLength(1);expect(component.current()?.id).toBe('lgs');expect(component.creating()).toBe(false);expect(component.error()).toContain('oluşturulamadı');
 });
 it('reloads the saved choice after an error and applies a target update locally',async()=>{
  const {component,api}=await setup();component.targetUpdated({goalId:'lgs',version:1,canEdit:true,targetSchoolId:'new-school',targetUniversityProgramId:null});expect(component.current()?.target.targetSchoolId).toBe('new-school');component.load();expect(api.getPrimaryGoal).toHaveBeenCalledTimes(2);
 });

 it('opens the choice area immediately when no main goal has been selected',async()=>{
  const {component,api,fixture}=await setup();api.getPrimaryGoal.mockReturnValueOnce(of({goalId:null,version:null}) as any);component.load();fixture.detectChanges();expect(fixture.nativeElement.querySelector('details').open).toBe(true);expect(fixture.nativeElement.querySelector('summary').textContent).toContain('Ana hedef seç');
 });

});
