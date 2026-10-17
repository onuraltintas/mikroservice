import {TestBed} from '@angular/core/testing';
import {of,throwError} from 'rxjs';
import {StudentStudyPlansComponent} from './student-study-plans.component';
import {CoachingStudyPlanningService,StudyPlan} from '../../../core/services/coaching-study-planning.service';
describe('restore archived study plan',()=>{
 const archived:StudyPlan={id:'old',version:11,title:'Yıllık',status:'Archived',tasks:[{id:'done',plannedDate:'2027-01-01',title:'Matematik',plannedMinutes:30,topicId:null,isPinned:false,isCompleted:true,actualMinutes:17,completedAt:'2027-01-01T18:00:00Z'}]};
 async function setup(){const api={list:vi.fn(()=>of({items:[{id:'test',version:2,title:'Test',status:'Active'}],totalCount:1,pageNumber:1,pageSize:20})),get:vi.fn(()=>of(archived)),restore:vi.fn(()=>of({...archived,status:'Active'}))};TestBed.configureTestingModule({imports:[StudentStudyPlansComponent],providers:[{provide:CoachingStudyPlanningService,useValue:api}]});const f=TestBed.createComponent(StudentStudyPlansComponent),c=f.componentInstance;f.detectChanges();c.open('old');f.detectChanges();await f.whenStable();return{f,c,api};}
 it('requires confirmation and sends both current versions without copying completed work',async()=>{
  const {f,c,api}=await setup();expect(f.nativeElement.textContent).toContain('Planı yeniden aktif et');c.restore();expect(api.restore).not.toHaveBeenCalled();
  c.restoreConfirmed=true;c.restore();expect(api.restore).toHaveBeenCalledWith('old',11,{id:'test',version:2,title:'Test',status:'Active'});expect(c.selected()?.tasks[0].actualMinutes).toBe(17);expect(c.selected()?.status).toBe('Active');
 });
 it('keeps archived work on conflict and never offers restore in read-only mode',async()=>{
  const {f,c,api}=await setup();api.restore.mockReturnValue(throwError(()=>({status:409,error:{message:'Aktif plan değişti'}})) as never);c.restoreConfirmed=true;c.restore();expect(c.selected()?.status).toBe('Archived');expect(c.error()).toContain('Aktif plan değişti');
  f.componentRef.setInput('readOnly',true);f.detectChanges();expect(f.nativeElement.textContent).not.toContain('Planı yeniden aktif et');
 });
});
