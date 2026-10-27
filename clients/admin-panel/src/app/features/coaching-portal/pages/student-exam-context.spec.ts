import { CoachingInstitutionExamRanksService } from '../../../core/services/coaching-institution-exam-ranks.service';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudentExamsService } from '../../../core/services/coaching-student-exams.service';
import { StudentExamResultsComponent } from './student-exam-results.component';
describe('profile based exam entry',()=>{
 const profiles=[{code:'LGS',name:'LGS',wrongAnswerDivisor:3,sections:[{code:'MATH',name:'Matematik',questionCount:20}]},{code:'TYT',name:'TYT',wrongAnswerDivisor:4,sections:[]},{code:'AYT_SAY',name:'AYT Sayısal',wrongAnswerDivisor:4,sections:[]}];
 async function setup(purpose='Lgs'){
  TestBed.configureTestingModule({imports:[StudentExamResultsComponent],providers:[{provide:CoachingInstitutionExamRanksService,useValue:{mine:()=>of([])}},{provide:CoachingStudentExamsService,useValue:{list:()=>of({items:[],totalCount:0,pageNumber:1,pageSize:20}),profiles:()=>of(profiles)}},{provide:CoachingStudyPlanningService,useValue:{}},{provide:CoachingLearningSetupService,useValue:{get:()=>of({context:{purpose,primaryScoreType:'SAY'}})}}]});
  const f=TestBed.createComponent(StudentExamResultsComponent);f.detectChanges();await f.whenStable();return f.componentInstance;
 }
 it('selects LGS and prepares empty answer rows without marking the form dirty',async()=>{const c=await setup();expect(c.practiceExam?.examCode).toBe('LGS');expect(c.practiceExam?.sections[0].empty).toBe(20);expect(c.dirty).toBe(false);});
 it('offers TYT and the selected AYT field for a YKS student',async()=>{const c=await setup('Yks');expect(c.availableProfiles().map(x=>x.code)).toEqual(['TYT','AYT_SAY']);});
 it('keeps all source profiles accessible when editing an old result',async()=>{const c=await setup();c.selected={id:'old',practiceExam:{examCode:'TYT',sections:[]}} as any;expect(c.availableProfiles().map(x=>x.code)).toContain('TYT');});
});
