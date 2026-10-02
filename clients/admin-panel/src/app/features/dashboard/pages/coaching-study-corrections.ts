import { Component, DestroyRef, Input, Output, EventEmitter, OnChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { StudyPlan } from '../../../core/services/coaching-study-planning.service';
import { environment } from '../../../../environments/environment';

interface Goal { id:string;version:number;title:string;description:string|null;targetDate:string|null;targetScore:number|null;currentProgress:number; }
interface Audit { id:string;occurredAt:string;actorUserId:string;resourceType:string;changedFieldsJson:string; }
@Component({selector:'app-coaching-study-corrections',standalone:true,imports:[FormsModule],template:`
  @if(permitted()) {
    <section class="space-y-3 rounded-xl border p-4" aria-labelledby="correction-heading">
      <h4 id="correction-heading" class="font-semibold">Gerekçeli düzeltme ve işlem geçmişi</h4>
      <p class="text-sm">Tamamlanmış görevler ve öğrenci beyanı değiştirilemez. Her işlem eski/yeni değerlerle kaydedilir.</p>
      @if(busy()) { <p role="status">İşlem sürüyor…</p> }
      @if(error()) { <p role="alert" class="text-red-700">{{error()}}</p> }
      <fieldset [disabled]="busy()" class="space-y-3">
        <label class="block">İşlem gerekçesi<textarea [(ngModel)]="reason" maxlength="200" class="block w-full rounded border p-2 dark:bg-gray-800"></textarea></label>
        @if(plan && plan.status !== 'Archived') {
          <label class="block">Düzeltilmiş plan başlığı<input [(ngModel)]="title" maxlength="200" class="block w-full rounded border p-2 dark:bg-gray-800" /></label>
          <button type="button" (click)="savePlan(false)" [disabled]="reason.trim().length < 5" class="rounded border px-3 py-2">Plan başlığını düzelt</button>
          <button type="button" (click)="savePlan(true)" [disabled]="reason.trim().length < 5" class="ml-2 rounded border px-3 py-2">Planı arşivle</button>
          <label class="block">Düzeltilecek bekleyen görev<select [(ngModel)]="taskId" (ngModelChange)="selectTask()" class="block rounded border p-2 dark:bg-gray-800"><option value="">Görev seçin</option>@for(task of plan.tasks;track task.id) {@if(!task.isCompleted) {<option [value]="task.id">{{task.title}}</option>}}</select></label>
          @if(taskId) {
            <label class="block">Görev başlığı<input [(ngModel)]="taskTitle" maxlength="200" class="block rounded border p-2 dark:bg-gray-800" /></label>
            <label class="block">Planlanan tarih<input type="date" [(ngModel)]="taskDate" class="block rounded border p-2 dark:bg-gray-800" /></label>
            <label class="block">Planlanan dakika<input type="number" [(ngModel)]="taskMinutes" min="1" max="1440" class="block rounded border p-2 dark:bg-gray-800" /></label>
            <button type="button" (click)="saveTask()" [disabled]="reason.trim().length < 5" class="rounded border px-3 py-2">Görevi düzelt</button>
          }
        }
        <button type="button" (click)="loadGoals(1)" class="rounded border px-3 py-2">Düzeltilebilir hedefleri yükle</button>
        @if(goals();as result) {
          <label class="block">Düzeltilecek hedef<select [(ngModel)]="goalId" (ngModelChange)="selectGoal()" class="block rounded border p-2 dark:bg-gray-800"><option value="">Hedef seçin</option>@for(goal of result.items;track goal.id) {<option [value]="goal.id">{{goal.title}} — kaydedilen ilerleme %{{goal.currentProgress}}</option>}</select></label>
          <button type="button" (click)="loadGoals(goalPage-1)" [disabled]="goalPage<=1">Önceki hedefler</button><button type="button" (click)="loadGoals(goalPage+1)" [disabled]="goalPage*25>=result.totalCount">Sonraki hedefler</button>
          @if(goalId) {
            <label class="block">Hedef açıklaması<textarea [(ngModel)]="goalDescription" maxlength="500" class="block w-full rounded border p-2 dark:bg-gray-800"></textarea></label>
            <label class="block">Hedef tarihi<input type="date" [(ngModel)]="goalDate" class="block rounded border p-2 dark:bg-gray-800" /></label>
            <label class="block">Hedef puanı<input type="number" [(ngModel)]="goalScore" min="0" max="999.99" step="0.01" class="block rounded border p-2 dark:bg-gray-800" /></label>
            <button type="button" (click)="saveGoal()" [disabled]="reason.trim().length<5" class="rounded border px-3 py-2">Hedefi düzelt</button>
          }
        }
        <button type="button" (click)="loadHistory(1)" class="rounded border px-3 py-2">İşlem geçmişini yükle</button>
      </fieldset>
      @if(history();as result) {
        <ul class="space-y-2 text-sm">@for(row of result.items;track row.id) {<li class="rounded border p-2"><p>{{row.occurredAt}} — Yönetici: {{row.actorUserId}} — {{row.resourceType}}</p><pre class="whitespace-pre-wrap break-all">{{row.changedFieldsJson}}</pre></li>} @empty {<li>Düzeltme kaydı yok.</li>}</ul>
        <button type="button" (click)="loadHistory(historyPage-1)" [disabled]="busy()||historyPage<=1">Önceki işlemler</button><button type="button" (click)="loadHistory(historyPage+1)" [disabled]="busy()||historyPage*25>=result.totalCount">Sonraki işlemler</button>
      }
    </section>
  }`})
export class CoachingStudyCorrectionsComponent implements OnChanges {
  @Input({required:true}) studentId!:string; @Input() plan:StudyPlan|null=null; @Output() saved=new EventEmitter<void>();
  private readonly http=inject(HttpClient);private readonly auth=inject(AuthService);private readonly toaster=inject(ToasterService);private readonly destroy=inject(DestroyRef);
  private requests:Subscription[]=[];
  readonly busy=signal(false);readonly error=signal('');readonly goals=signal<{items:Goal[];totalCount:number}|null>(null);readonly history=signal<{items:Audit[];totalCount:number}|null>(null);
  title='';reason='';taskId='';taskTitle='';taskDate='';taskMinutes=30;goalId='';goalDescription='';goalDate='';goalScore:number|null=null;goalPage=1;historyPage=1;
  permitted(){return !!this.auth.userProfile()?.roles.includes('SystemAdmin')&&this.auth.hasPermission('Permissions.Coaching.Manage');}
  private url(){return `${environment.apiUrl}/coaching-admin/students/${encodeURIComponent(this.studentId)}/study/corrections`;}
  ngOnChanges(){this.requests.forEach(x=>x.unsubscribe());this.requests=[];this.busy.set(false);this.error.set('');this.title=this.plan?.title??'';this.reason='';this.taskId='';this.goalId='';this.goals.set(null);this.history.set(null);}
  selectTask(){const task=this.plan?.tasks.find(x=>x.id===this.taskId&&!x.isCompleted);this.taskTitle=task?.title??'';this.taskDate=task?.plannedDate??'';this.taskMinutes=task?.plannedMinutes??30;}
  selectGoal(){const goal=this.goals()?.items.find(x=>x.id===this.goalId);this.goalDescription=goal?.description??'';this.goalDate=goal?.targetDate?.slice(0,10)??'';this.goalScore=goal?.targetScore??null;}
  async savePlan(archive:boolean){if(!this.plan||this.plan.status==='Archived'||!this.title.trim())return;await this.write(`/plans/${this.plan.id}`,{expectedVersion:this.plan.version,title:this.title.trim(),archive,reason:this.reason.trim()});}
  async saveTask(){const task=this.plan?.tasks.find(x=>x.id===this.taskId&&!x.isCompleted);if(!task||!this.taskDate||!this.taskTitle.trim()||this.taskMinutes<1||this.taskMinutes>1440)return;await this.write(`/plans/${this.plan!.id}/tasks/${this.taskId}`,{expectedPlanVersion:this.plan!.version,title:this.taskTitle.trim(),plannedDate:this.taskDate,plannedMinutes:this.taskMinutes,reason:this.reason.trim()});}
  async saveGoal(){const goal=this.goals()?.items.find(x=>x.id===this.goalId);if(!goal)return;await this.write(`/goals/${goal.id}`,{expectedVersion:goal.version,description:this.goalDescription.trim()||null,targetDate:this.goalDate||null,targetScore:this.goalScore,reason:this.reason.trim()});}
  private async write(path:string,body:object){
    if(!this.permitted()||this.busy()||this.reason.trim().length<5||this.reason.trim().length>200)return;
    this.busy.set(true);this.error.set('');const student=this.studentId;const plan=this.plan;
    try {const confirmed=await this.toaster.confirm('Düzeltme gerekçesi ve eski/yeni değerler kaydedilecek. Devam edilsin mi?',{confirmText:'Onayla'});
      if(!confirmed||this.destroy.destroyed||student!==this.studentId||plan!==this.plan){this.busy.set(false);return;}
      this.requests.push(this.http.put(this.url()+path,body).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{this.busy.set(false);this.reason='';this.goals.set(null);this.history.set(null);this.toaster.success('Düzeltme kaydedildi. Güncel kayıt yeniden yüklenecek.');this.saved.emit();},error:err=>this.fail(err)}));
    } catch {this.fail({});}
  }
  loadGoals(page:number){this.read<{items:Goal[];totalCount:number}>('goals',page,value=>{this.goals.set(value);this.goalPage=page;this.goalId='';});}
  loadHistory(page:number){this.read<{items:Audit[];totalCount:number}>('history',page,value=>{this.history.set(value);this.historyPage=page;});}
  private read<T>(kind:string,page:number,next:(value:T)=>void){if(!this.permitted()||this.busy()||page<1)return;this.busy.set(true);this.error.set('');this.requests.push(this.http.get<{data:T}>(`${this.url()}/${kind}?page=${page}`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:response=>{this.busy.set(false);next(response.data);},error:err=>this.fail(err)}));}
  private fail(err:{error?:{message?:string}}){this.busy.set(false);this.error.set(err.error?.message??'İşlem tamamlanamadı. Güncel kaydı açıp yeniden deneyin.');}
}
