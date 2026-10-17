import {CommonModule} from '@angular/common';
import {Component,DestroyRef,EventEmitter,OnInit,Output,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {finalize} from 'rxjs';
import {CoachingTeacherAssignmentsService,CoachingAssignmentFollowUpItem} from './coaching-teacher-assignments.service';
@Component({selector:'staff-coaching-assignment-follow-up',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./coaching-teacher-assignment-follow-up.component.html',styleUrl:'./coaching-teacher-assignment-follow-up.component.scss'})
export class CoachingTeacherAssignmentFollowUpComponent implements OnInit {
 private readonly service=inject(CoachingTeacherAssignmentsService);private readonly destroyRef=inject(DestroyRef);private requestVersion=0;
 @Output() readonly openAssignment=new EventEmitter<string>();@Output() readonly close=new EventEmitter<void>();
 readonly items=signal<CoachingAssignmentFollowUpItem[]>([]);readonly loading=signal(false);readonly error=signal('');
 readonly mode=signal<'overdue'|'pending'>('overdue');readonly page=signal(1);readonly total=signal(0);readonly pendingCount=signal(0);readonly overdueCount=signal(0);
 search='';private appliedSearch='';
 ngOnInit(){this.load();}
 load(){const request=++this.requestVersion;this.loading.set(true);this.error.set('');
  this.service.getFollowUp(this.mode(),this.page(),25,this.appliedSearch).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>{if(request===this.requestVersion)this.loading.set(false);})).subscribe({
   next:p=>{if(request!==this.requestVersion)return;this.items.set(p.items);this.total.set(p.totalCount);this.pendingCount.set(p.pendingCount);this.overdueCount.set(p.overdueCount);},
   error:()=>{if(request!==this.requestVersion)return;this.items.set([]);this.total.set(0);this.pendingCount.set(0);this.overdueCount.set(0);this.error.set('Ödev takibi yüklenemedi. Lütfen tekrar deneyin.');}
  });
 }
 setMode(mode:'overdue'|'pending'){if(mode===this.mode())return;this.mode.set(mode);this.page.set(1);this.load();}
 applySearch(){this.appliedSearch=this.search.trim();this.page.set(1);this.load();}
 previousPage(){if(this.page()>1&&!this.loading()){this.page.update(p=>p-1);this.load();}}
 nextPage(){if(this.page()*25<this.total()&&!this.loading()){this.page.update(p=>p+1);this.load();}}
 delay(minutes:number){if(minutes<1)return '1 dk’dan az';const days=Math.floor(minutes/1440),hours=Math.floor(minutes%1440/60),remaining=minutes%60;return [days?`${days} gün`:'',hours?`${hours} sa`:'',remaining?`${remaining} dk`:''].filter(Boolean).join(' ');}
}
