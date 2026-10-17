import {CommonModule} from '@angular/common';
import {Component,DestroyRef,HostListener,computed,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {Router,RouterLink} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {finalize} from 'rxjs';
import {CoachingWeeklyOutcomePlanService,WeeklyOutcomeOptions,WeeklyOutcomePreview,WeeklyOutcomeRequest} from '../../../core/services/coaching-weekly-outcome-plan.service';
import {clock} from './weekly-calendar';

@Component({selector:'app-student-weekly-outcome-plan',standalone:true,imports:[CommonModule,FormsModule,RouterLink],templateUrl:'./student-weekly-outcome-plan.component.html',styleUrl:'./student-weekly-outcome-plan.component.scss'})
export class StudentWeeklyOutcomePlanComponent {
  private readonly service=inject(CoachingWeeklyOutcomePlanService);private readonly destroy=inject(DestroyRef);private readonly router=inject(Router);
  readonly options=signal<WeeklyOutcomeOptions|null>(null);readonly preview=signal<WeeklyOutcomePreview|null>(null);readonly loading=signal(false);readonly generating=signal(false);readonly saving=signal(false);readonly stale=signal(false);readonly error=signal('');
  readonly lessonFilter=signal('');readonly page=signal(1);readonly pageSize=25;readonly clock=clock;
  readonly filteredTasks=computed(()=>this.preview()?.schedule.tasks.filter(x=>!this.lessonFilter()||x.lessonId===this.lessonFilter())??[]);
  readonly visibleTasks=computed(()=>this.filteredTasks().slice((this.page()-1)*this.pageSize,this.page()*this.pageSize));
  readonly remaining=computed(()=>this.preview()?.schedule.lessons.reduce((sum,x)=>sum+x.remainingMinutes,0)??0);
  readonly totalMinutes=computed(()=>(this.preview()?.schedule.tasks.reduce((sum,x)=>sum+x.minutes,0)??0)+(this.preview()?.protectedTasks?.reduce((sum,x)=>sum+x.minutes,0)??0));
  startDate='';endDate='';reuseFreedSlots=false;title='Haftalık derslerimden çalışma planım';confirmed=false;private defaultEnd=true;
  constructor(){this.load();}
  busy(){return this.loading()||this.generating()||this.saving();}
  load(){if(this.busy())return;this.loading.set(true);this.error.set('');this.service.options().pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.loading.set(false))).subscribe({next:r=>{
    if(!this.options()){this.startDate=r.startDate;this.endDate=r.endDate;}this.options.set(r);this.changed();this.stale.set(false);
  },error:e=>this.fail(e,'Haftalık taslak yüklenemedi. Önce takvimini kaydet.')} );}
  changed(){this.preview.set(null);this.confirmed=false;this.page.set(1);this.lessonFilter.set('');this.error.set('');}
  startChanged(){if(this.defaultEnd&&/^\d{4}-\d{2}-\d{2}$/.test(this.startDate)){const [y,m,d]=this.startDate.split('-').map(Number);this.endDate=`${m<6||m===6&&d===1?y:y+1}-06-01`;}this.changed();}
  endChanged(){this.defaultEnd=false;this.changed();}
  private request():WeeklyOutcomeRequest{return{startDate:this.startDate,endDate:this.endDate,expectedWeeklyDraftVersion:this.options()!.weeklyDraftVersion,reuseFreedSlots:this.reuseFreedSlots};}
  generatePreview(){
    if(this.busy()||!this.options())return;if(!this.startDate||!this.endDate||this.endDate<this.startDate){this.error.set('Başlangıç ve bitiş tarihlerini kontrol et.');return;}
    this.generating.set(true);this.error.set('');this.confirmed=false;this.preview.set(null);this.stale.set(false);
    this.service.preview(this.request()).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.generating.set(false))).subscribe({next:r=>{this.preview.set(r);this.page.set(1);this.lessonFilter.set('');},error:e=>this.fail(e,'Önizleme hazırlanamadı.')});
  }
  extendDeadline(){const end=this.preview()?.schedule.recommendedEndDate;if(!end||end<=this.endDate||this.busy())return;this.endDate=end;this.defaultEnd=false;this.generatePreview();}
  useFreedSlots(){if(this.busy())return;this.reuseFreedSlots=true;this.generatePreview();}
  isCurrent(){const p=this.preview();return !!p&&!!this.options()&&p.request.startDate===this.startDate&&p.request.endDate===this.endDate&&p.request.expectedWeeklyDraftVersion===this.options()!.weeklyDraftVersion&&p.request.reuseFreedSlots===this.reuseFreedSlots;}
  save(){
    const p=this.preview();if(this.busy()||!p?.canCreate||!this.confirmed||this.stale()||!this.isCurrent())return;
    if(!this.title.trim()||this.title.trim().length>200){this.error.set('Plan için en fazla 200 karakterlik bir başlık gir.');return;}
    this.saving.set(true);this.error.set('');this.service.create({title:this.title.trim(),preview:p.request,fingerprint:p.fingerprint}).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.saving.set(false)))
      .subscribe({next:r=>void this.router.navigate(['/coaching-portal/study-plans'],{queryParams:{plan:r.id}}),error:e=>this.fail(e,'Plan taslağı kaydedilemedi. Yeniden deneyebilirsin.')});
  }
  private fail(e:{status?:number;error?:{message?:string}},fallback:string){if(e.status===409){this.stale.set(true);this.confirmed=false;}this.error.set(e.error?.message??fallback);}
  filter(value:string){this.lessonFilter.set(value);this.page.set(1);}
  canLeavePage(){return !this.saving();}
  @HostListener('window:beforeunload',['$event']) beforeUnload(e:BeforeUnloadEvent){if(this.saving()){e.preventDefault();e.returnValue='';}}
}
