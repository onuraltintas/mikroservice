import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { weeklyPlanKindLabel } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { CommonModule } from '@angular/common';
import { Component, ChangeDetectorRef, DestroyRef, ElementRef, Injector, Input, OnChanges, OnInit, ViewChild, afterNextRender, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subject, finalize, firstValueFrom, takeUntil, switchMap } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan, StudyPlanPage, StudyPlanStatus, StudyTask, StudyTaskInput } from '../../../core/services/coaching-study-planning.service';
import {clock} from './weekly-calendar';
import {CoachingLearningSetupService,LearningLesson} from '../../../core/services/coaching-learning-setup.service';
import {lessonColors,lessonKey} from './study-plan-colors';
import {computed} from '@angular/core';
import {CalendarAction,StudyPlanCalendarComponent} from './study-plan-calendar.component';
import {CalendarMove,calendarTaskOverdue} from './study-plan-calendar';
import {effect} from '@angular/core';
import {PlanTaskRowComponent} from './plan-task-row.component';
import {CsBottomSheetComponent,CsSegmentedTabsComponent,CsTabOption} from '../ui';

const WEEKDAYS=['Pzt','Sal','Çar','Per','Cum','Cmt','Paz'];
const isoDay=(date:Date)=>date.toISOString().slice(0,10);
const shiftDays=(iso:string,days:number)=>{const d=new Date(`${iso}T12:00:00Z`);d.setUTCDate(d.getUTCDate()+days);return isoDay(d);};

@Component({
  selector: 'app-student-study-plans',
  standalone: true,
  imports: [CommonModule, FormsModule, StudyPlanCalendarComponent, PlanTaskRowComponent, CsBottomSheetComponent, CsSegmentedTabsComponent],
  templateUrl: './student-study-plans.component.html',
  styleUrl: './student-study-plans.component.scss'
})
export class StudentStudyPlansComponent implements OnInit, OnChanges {
  private readonly toaster = coachingToaster();
  readonly clock=clock;taskPage=1;
  readonly planKindLabel=weeklyPlanKindLabel;
  // T4 (31.10.2026): the student page shows a week strip or a list of compact rows; details open in a bottom sheet.
  readonly studentView=signal<'week'|'list'>('week');
  readonly studentViews:CsTabOption[]=[{value:'week',label:'Takvim'},{value:'list',label:'Liste'}];
  readonly listFilters:{value:'all'|'today'|'pending'|'overdue'|'completed';label:string}[]=[{value:'all',label:'Tümü'},{value:'today',label:'Bugün'},{value:'pending',label:'Bekleyen'},{value:'overdue',label:'Geciken'},{value:'completed',label:'Tamamlanan'}];
  readonly boardOpen=signal(false);
  readonly selectedDay=signal<string|null>(null);
  readonly sheetTaskId=signal<string|null>(null);
  readonly lastCompletedId=signal<string|null>(null);
  get studentMode(){return !this.staffStudentId;}
  readonly sheetTask=computed(()=>{const id=this.sheetTaskId();return id?this.selected()?.tasks.find(t=>t.id===id)??null:null;});
  readonly weekDays=computed(()=>{
    const plan=this.selected(),day=this.selectedDay()??this.planToday();if(!plan)return[];
    const start=shiftDays(day,-((new Date(`${day}T12:00:00Z`).getUTCDay()+6)%7)),today=this.planToday();
    return Array.from({length:7},(_,i)=>{const date=shiftDays(start,i),tasks=plan.tasks.filter(t=>t.plannedDate===date);
      return{date,label:WEEKDAYS[i],number:Number(date.slice(8)),count:tasks.length,open:tasks.filter(t=>!t.isCompleted).length,late:tasks.some(t=>this.isOverdue(t)),isToday:date===today,colors:[...new Set(tasks.map(t=>this.taskColor(t)))].slice(0,3)};});
  });
  readonly dayTasks=computed(()=>{const day=this.selectedDay();return(this.selected()?.tasks??[]).filter(t=>t.plannedDate===day).sort((a,b)=>(a.plannedStartMinute??1440)-(b.plannedStartMinute??1440));});
  dayGroups(){const groups=new Map<string,StudyTask[]>();
    for(const task of [...this.filteredTasks()].sort((a,b)=>a.plannedDate.localeCompare(b.plannedDate)||(a.plannedStartMinute??1440)-(b.plannedStartMinute??1440))){const list=groups.get(task.plannedDate)??[];list.push(task);groups.set(task.plannedDate,list);}
    return[...groups].map(([date,tasks])=>({date,tasks}));}
  dayTitle(date:string){const today=this.planToday();if(date===today)return'Bugün';if(date===shiftDays(today,1))return'Yarın';if(date===shiftDays(today,-1))return'Dün';
    return new Intl.DateTimeFormat('tr-TR',{weekday:'long',day:'numeric',month:'long',timeZone:'UTC'}).format(new Date(`${date}T12:00:00Z`));}
  planProgress(){const tasks=this.selected()?.tasks??[];return{done:tasks.filter(t=>t.isCompleted).length,total:tasks.length};}
  selectDay(date:string){this.selectedDay.set(date);}
  shiftWeek(weeks:number){this.selectedDay.set(shiftDays(this.selectedDay()??this.planToday(),weeks*7));}
  setListFilter(value:'all'|'today'|'pending'|'overdue'|'completed'){this.taskFilter=value;this.taskPage=1;}
  readonly listDays=signal(14);
  get remainingLabel(){return this.staffStudentId?'Yalnız kalan kapsamı kaydet':'Yalnız kalan işi kaydet';}
  openEditFlow(){void this.router?.navigate(['/coaching-portal/plan/weekly']);}
  openTask(id:string){this.sheetTaskId.set(id);}
  closeTask(){this.sheetTaskId.set(null);this.completionError.set(null);}
  canQuickComplete(task:StudyTask){return this.selected()?.status==='Active'&&!this.readOnly&&!task.isCompleted;}
  quickComplete(task:StudyTask){this.lastCompletedId.set(task.id);this.complete(task,this.actualMinutes[task.id]??task.plannedMinutes,this.actualQuestions[task.id]??null);}
  undoComplete(task:StudyTask){
    const current=this.selected()?.tasks.find(t=>t.id===task.id)??task;
    this.startCorrection(current);const form=this.correction;if(!form)return;
    Object.assign(form,{minutes:0,questions:null,completed:false,remainingMinutes:null,remainingQuestions:null,reason:'Yanlışlıkla tamamlandı olarak işaretledim.'});
    if(this.continuationScopeRequired(current)){this.openTask(current.id);return;}
    this.lastCompletedId.set(null);this.saveCorrection();
  }
  private readonly openSheetOnError=effect(()=>{const failure=this.completionError();if(failure&&!this.staffStudentId)this.sheetTaskId.set(failure.taskId);});
  private pickStudentDay(plan:StudyPlan,focus:string|null){
    const today=this.planToday(),dates=plan.tasks.map(t=>t.plannedDate).sort();
    const target=focus?plan.tasks.find(t=>t.id===focus)?.plannedDate:undefined;
    this.selectedDay.set(target??(dates.includes(today)||!dates.length?today:plan.tasks.filter(t=>!t.isCompleted&&t.plannedDate>=today).map(t=>t.plannedDate).sort()[0]??dates.at(-1)!));
    if(focus&&target)this.sheetTaskId.set(focus);
  }
  readonly planView=signal<'calendar'|'list'>('list');readonly selectedTaskIds=signal<string[]>([]);
  private readonly injector=inject(Injector);
  private readonly changeDetector=inject(ChangeDetectorRef);
  @ViewChild('calendarTaskDetail') private calendarTaskDetail?:ElementRef<HTMLElement>;
  selectCalendarTasks(ids:string[]){this.selectedTaskIds.set(ids);afterNextRender(()=>this.calendarTaskDetail?.nativeElement.focus(),{injector:this.injector});}
  visibleTasks(){const plan=this.selected();if(this.planView()==='calendar')return plan?.tasks.filter(t=>this.selectedTaskIds().includes(t.id))??[];const tasks=this.filteredTasks();return plan?.weeklySource?tasks.slice((this.taskPage-1)*50,this.taskPage*50):tasks;}
  readonly taskColors=computed(()=>lessonColors(this.selected()?.tasks??[]));
  taskColor(task:StudyTask){return this.taskColors().get(lessonKey(task));}
  @ViewChild('taskDialog') private taskDialogElement?:ElementRef<HTMLElement>;
  calendarForm:{kind:CalendarAction['kind'];taskId:string|null;taskIds:string[];date:string;time:string;title:string;minutes:number;lessonId:string|null;actualMinutes:number;actualQuestions:number|null;workedOn?:string}|null=null;
  calendarLessons:LearningLesson[]=[];calendarLessonsLoading=false;
  async calendarCommand(action:CalendarAction){
    const plan=this.selected();if(!plan||this.readOnly||this.busy()||plan.status==='Archived'||action.kind==='complete'&&(!!this.staffStudentId||plan.status!=='Active'))return;
    const tasks=plan.tasks.filter(t=>action.taskIds.includes(t.id)&&!t.isCompleted),task=tasks[0];
    if(action.kind!=='add'&&!task)return;
    this.calendarForm={kind:action.kind,taskId:task?.id??null,taskIds:tasks.map(t=>t.id),date:action.date,time:clock(action.startMinute),title:'',minutes:30,lessonId:null,actualMinutes:30,actualQuestions:null};
    this.error.set(null);if(task)this.chooseCalendarTask(task.id);
    afterNextRender(()=>this.taskDialogElement?.nativeElement.querySelector<HTMLElement>('input,select,button')?.focus(),{injector:this.injector});
    if(action.kind==='add'){
      this.calendarLessons=[];this.calendarLessonsLoading=true;
      try{
        const setup=this.injector.get(CoachingLearningSetupService),student=this.staffStudentId;
        const catalog=await firstValueFrom((student?setup.catalogForStudent(student):setup.catalog()).pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef)),{defaultValue:null});
        if(this.calendarForm?.kind==='add'&&catalog){this.calendarLessons=catalog.lessons;this.calendarForm.lessonId=catalog.lessons[0]?.id??null;}
      }catch(error){this.showError(error,'Dersler yüklenemedi. Tekrar dene.');}
      finally{this.calendarLessonsLoading=false;this.changeDetector.markForCheck();}
    }
  }
  chooseCalendarTask(id:string){
    const task=this.selected()?.tasks.find(t=>t.id===id),form=this.calendarForm;if(!task||!form)return;
    Object.assign(form,{taskId:id,date:task.plannedDate,time:clock(task.plannedStartMinute??540),title:task.title,minutes:task.plannedMinutes,lessonId:task.weeklyLessonId??null,actualMinutes:this.actualMinutes[id]??task.plannedMinutes,actualQuestions:this.actualQuestions[id]??null});
  }
  calendarTaskOptions(){return this.selected()?.tasks.filter(t=>this.calendarForm?.taskIds.includes(t.id))??[];}
  calendarDurationLocked(){const task=this.selected()?.tasks.find(t=>t.id===this.calendarForm?.taskId);return !!task?.outcomes?.length;}
  closeCalendarForm(){if(this.busy())return;this.calendarForm=null;}
  calendarDialogKey(event:KeyboardEvent){
    if(event.key==='Escape'){event.preventDefault();this.closeCalendarForm();return;}
    if(event.key!=='Tab')return;
    const items=[...this.taskDialogElement!.nativeElement.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),[tabindex="0"]')];
    const first=items[0],last=items.at(-1);
    if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus();}
    if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus();}
  }
  async saveCalendarForm(){
    const form=this.calendarForm,plan=this.selected();if(!form||!plan||this.readOnly||this.busy()||plan.status==='Archived')return;
    const task=plan.tasks.find(t=>t.id===form.taskId);
    if(form.kind==='complete'){
      if(!task||this.staffStudentId)return;
      if(form.workedOn)this.workDates[task.id]=form.workedOn;
      this.complete(task,form.actualMinutes,form.actualQuestions);
      return;
    }
    if(form.kind==='delete'){
      if(!task||task.isCompleted)return;
      const scope=this.staffStudentId;
      if(!await this.toaster.confirm(`“${task.title}” çalışması silinsin mi? Bu çalışmanın planlanan kazanım payları da kaldırılır. Tamamlanan çalışmalar korunur.`))return;
      if(this.staffStudentId!==scope||this.selected()?.id!==plan.id||this.busy())return;
      this.mutate(this.service.deleteCalendarTask(plan.id,task.id,plan.version,...this.studentArgument),'Bekleyen çalışma silindi.');return;
    }
    if(!/^([01]\d|2[0-3]):[0-5]\d$/.test(form.time)||!form.date||!form.title.trim()||form.title.trim().length>200||!Number.isInteger(form.minutes)||form.minutes<1||form.minutes>1440||form.kind==='add'&&!form.lessonId){this.error.set('Ders, başlık, tarih ve süreyi kontrol et.');return;}
    const [h,m]=form.time.split(':').map(Number),start=h*60+m;
    if(start+form.minutes>1440||plan.tasks.some(t=>t.id!==form.taskId&&t.plannedDate===form.date&&t.plannedStartMinute!=null&&t.plannedStartMinute<start+form.minutes&&t.plannedStartMinute+t.plannedMinutes>start)){this.error.set('Çalışma günün dışına taşıyor veya başka bir çalışmayla çakışıyor.');return;}
    this.mutate(this.service.saveCalendarTask(plan.id,form.taskId,{expectedVersion:plan.version,title:form.title.trim(),plannedDate:form.date,startMinute:start,plannedMinutes:form.minutes,lessonId:form.lessonId},...this.studentArgument),'Çalışma takvime kaydedildi.');
  }
  @Input() autoOpenActive=false;
  @Input() initialPlanId:string|null=null;
  @Input() initialTaskId:string|null=null;
  @Input() taskFilter:'all'|'today'|'pending'|'completed'|'overdue'='all';
  filteredTasks(){const today=this.planToday();return this.selected()?.tasks.filter(t=>this.taskFilter==='all'||this.taskFilter==='today'&&t.plannedDate===today||this.taskFilter==='completed'&&t.isCompleted||this.taskFilter==='pending'&&!t.isCompleted||this.taskFilter==='overdue'&&this.isOverdue(t))??[];}
  /** Today's date in the plan's time zone. */
  planToday(){const zone=this.selected()?.weeklySource?.timeZoneId||'Europe/Istanbul';try{return new Intl.DateTimeFormat('en-CA',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());}catch{return this.today;}}
  private openedOnToday=false;
  openWizard(){void this.router?.navigate(['/coaching-portal/plan/wizard']);}
  isOverdue(task:StudyTask){return this.selected()?.status==='Active'&&calendarTaskOverdue(task,this.selected()?.weeklySource?.timeZoneId);}
  changeTaskFilter(){this.taskPage=1;this.planView.set('list');this.selectedTaskIds.set([]);}
  taskTitles:Record<string,string>={};
  correctTitle(task:StudyTask){const p=this.selected();if(!this.staffStudentId||this.readOnly||this.busy()||p?.status!=='Active'||task.isCompleted)return;
    const title=this.taskTitles[task.id]?.trim();if(!title||title.length>200){this.error.set('Çalışma başlığı 1–200 karakter olmalıdır.');return;}
    this.mutate(this.service.correctTitle(p.id,task.id,p.version,title,this.staffStudentId),'Çalışma başlığı düzeltildi. Kayıtlar korundu.');}
  @Input() staffStudentId: string | null = null;
  @Input() readOnly = false;
  private readonly scopeChanged = new Subject<void>();
  private initialized = false;
  private get studentArgument(): [string] | [] { return this.staffStudentId ? [this.staffStudentId] : []; }
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly router = inject(Router, { optional: true });
  private requestedPlanId = this.route?.snapshot.queryParamMap.get('plan') ?? null;
  private readonly requestedTaskPlanId = this.requestedPlanId;
  private readonly requestedTaskId = this.route?.snapshot.queryParamMap.get('task') ?? null;
  readonly page = signal<StudyPlanPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly selected = signal<StudyPlan | null>(null);
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly completionError = signal<{ taskId: string; field: 'minutes' | 'questions'; message: string } | null>(null);
  readonly success = signal<string | null>(null);
  readonly knownDraftId = signal<string | null>(null);
  editing = false;
  dirty = false;
  filter: StudyPlanStatus | '' = 'Active';
  editTitle = '';
  editTasks: StudyTaskInput[] = [];
  publishConfirmed = false;
  restoreConfirmed=false;
  restore(){const plan=this.selected();if(!plan||plan.status!=='Archived'||!this.restoreConfirmed||this.readOnly||this.busy())return;
    this.mutate(this.service.list(1,'Active',...this.studentArgument).pipe(switchMap(page=>this.service.restore(plan.id,plan.version,page.items[0]??null,...this.studentArgument))),'Plan yeniden etkinleştirildi. Önceki aktif plan ve çalışmalar arşivde korundu.');}
  readonly today=new Intl.DateTimeFormat('en-CA',{timeZone:'Europe/Istanbul',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  workDates:Record<string,string|undefined>={};
  actualMinutes: Record<string, number> = {};
  actualQuestions: Record<string, number | null> = {};
  moveCalendar(move:CalendarMove){const plan=this.selected();if(!plan||this.readOnly||this.busy()||plan.status==='Archived')return;
    this.mutate(this.service.move(plan.id,plan.version,move.taskIds,move.plannedDate,move.startMinute,...this.studentArgument),'Dersin günü ve saati güncellendi. Kazanımlar korundu.');}
  setDraftTime(task:StudyTaskInput,value:string){task.plannedStartMinute=value?Number(value.slice(0,2))*60+Number(value.slice(3)):null;this.dirty=true;}
  targetDates: Record<string, string> = {};

  ngOnInit() { this.initialized = true; if (this.staffStudentId) this.filter = ''; this.loadList(); }
  ngOnChanges() {
    if (!this.initialized) return;
    this.scopeChanged.next();this.calendarForm=null;this.correction=null;
    this.selected.set(null); this.page.set({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
    this.knownDraftId.set(null); this.filter = this.staffStudentId ? '' : 'Active'; this.editing = false; this.dirty = false;
    this.busy.set(false); this.loading.set(false); this.success.set(null); this.error.set(null);
    this.loadList();
  }

  async canLeavePage() {
    if (this.busy()) { this.error.set('İşlem sürüyor. Tamamlanmasını bekle.'); return false; }
    return !(this.dirty || !!this.calendarForm || !!this.correction || this.hasUnsavedTaskInputs()) || (typeof window !== 'undefined' && await this.toaster.confirm('Kaydedilmemiş değişikliklerin var. Değişikliklerden vazgeçerek ayrılmak istiyor musun?'));
  }

  async refreshAfterExternalChange() {
    if (this.busy() || this.dirty || this.calendarForm || this.correction || this.hasUnsavedTaskInputs()) return;
    const plan = this.selected();
    if (plan) await this.open(plan.id);
    else this.loadList();
  }

  loadList(pageNumber = 1) {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.service.list(pageNumber, this.filter || undefined, ...this.studentArgument).pipe(
      takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))
    ).subscribe({ next: async page => {
      this.page.set(page);
      if(this.staffStudentId&&this.initialPlanId&&!this.selected()&&!this.editing){await this.open(this.initialPlanId);}
      else if (!this.staffStudentId && this.requestedPlanId) {
        const id = this.requestedPlanId; this.requestedPlanId = null; await this.open(id);
      } else if ((!this.staffStudentId||this.autoOpenActive) && !this.selected() && !this.editing && (!this.filter || this.filter === 'Active')) {
        const active = page.items.find(plan => plan.status === 'Active');
        if (active) await this.open(active.id);
        else if (!this.filter) this.service.list(1, 'Active',...this.studentArgument).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef))
          .subscribe({ next: async plans => { if (plans.items.length && !this.selected() && !this.editing) await this.open(plans.items[0].id); },
            error: error => this.showError(error, 'Aktif plan yüklenemedi. Tekrar dene.') });
      }
      if (pageNumber === 1 && (!this.filter || this.filter === 'Draft')) {
        this.knownDraftId.set(page.items.find(plan => plan.status === 'Draft')?.id ?? null);
      }
    }, error: error => this.showError(error, 'Planlar yüklenemedi. Tekrar dene.') });
  }

  async open(id: string) {
    if (!await this.canLeaveEditor()) return;
    this.busy.set(true);
    this.error.set(null);
    this.service.get(id, ...this.studentArgument).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: plan => this.applyPlan(plan), error: error => this.showError(error, 'Plan açılamadı.') });
  }

  async newDraft() {
    if (this.readOnly) return;
    if (!await this.canLeaveEditor()) return;
    let draftId = this.selected()?.status === 'Draft' ? this.selected()!.id : this.knownDraftId();
    if (!draftId && !this.staffStudentId) {
      this.busy.set(true);
      try {
        const drafts = await firstValueFrom(this.service.list(1, 'Draft', ...this.studentArgument).pipe(
          takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))
        ), { defaultValue: null });
        if (!drafts || this.destroyRef.destroyed) return;
        draftId = drafts.items.find(plan => plan.status === 'Draft')?.id ?? null;
        this.knownDraftId.set(draftId);
      } catch (error) { this.showError(error, 'Taslak kontrol edilemedi. Tekrar dene.'); return; }
    }
    if (draftId) { await this.open(draftId); return; }
    this.selected.set(null);
    this.editing = true;
    this.editTitle = '';
    this.editTasks = [];
    this.correction=null;
    this.dirty = false;
    this.publishConfirmed = false;
    this.error.set(null);
    this.success.set(null);
    this.addTask();
  }

  addTask() {
    if (this.busy() || this.editTasks.length >= 500) return;
    const today = new Date();
    const plannedDate = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    this.editTasks = [...this.editTasks, { plannedDate, title: '', plannedMinutes: 30, topicId: null, isPinned: false }];
    this.dirty = true;
  }

  removeTask(index: number) {
    if (this.busy()) return;
    this.editTasks = this.editTasks.filter((_, i) => i !== index);
    this.dirty = true;
  }

  discardChanges() {
    if (this.busy()) return;
    const plan = this.selected();
    if (plan) this.applyPlan(plan);
    else { this.editing = false; this.editTasks = []; this.dirty = false; }
    this.error.set(null);
  }

  saveDraft() {
    if (this.readOnly) return;
    const selected = this.selected();
    if (this.busy() || !this.editing || (selected && selected.status !== 'Draft')) return;
    if (!this.editTitle.trim() || this.editTitle.length > 200 || this.editTasks.some(task =>
      !task.title.trim() || task.title.length > 200 || !task.plannedDate || !Number.isInteger(task.plannedMinutes)
      || task.plannedMinutes < 1 || task.plannedMinutes > 1440 || (task.sourceName?.trim().length ?? 0) > 300
      || (task.plannedStartMinute!=null&&(!Number.isInteger(task.plannedStartMinute)||task.plannedStartMinute<0||task.plannedStartMinute+task.plannedMinutes>1440))
      || (task.plannedQuestions != null && (!Number.isInteger(task.plannedQuestions) || task.plannedQuestions < 0 || task.plannedQuestions > 100000)))) {
      this.error.set('Başlık, tarih, süre, kaynak ve soru sayılarını kontrol et. Süre 1–1440 dakika, soru sayısı 0–100000 olmalıdır.');
      return;
    }
    const input = { title: this.editTitle.trim(), tasks: this.editTasks.map(task => ({ ...task, title: task.title.trim(), sourceName: task.sourceName?.trim() || null })) };
    this.mutate(selected ? this.service.replace(selected.id, selected.version, input, ...this.studentArgument) : this.service.create(input, ...this.studentArgument), 'Taslak kaydedildi. Henüz yayımlanmadı.');
  }

  publish() {
    if (this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Draft' || !plan.tasks.length || !this.publishConfirmed || this.dirty || this.busy()) return;
    this.mutate(this.service.publish(plan.id, plan.version, ...this.studentArgument), 'Plan yayımlandı. Önceki planın geçmişi korundu.');
  }
  async archiveDraft() {
    if (this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Draft' || this.busy() || this.dirty || typeof window === 'undefined') return;
    if (!await this.toaster.confirm('Taslak arşivlensin mi? Çalışmalar geçmişte korunacak; yeni bir taslak hazırlayabileceksin.')) return;
    this.mutate(this.service.archive(plan.id, plan.version, ...this.studentArgument), 'Taslak arşivlendi. Geçmiş korundu; yeni taslak hazırlayabilirsin.');
  }

  remainingMinutes:Record<string,number|null>={};
  remainingQuestions:Record<string,number|null>={};
  correction: {taskId:string;minutes:number;questions:number|null;completed:boolean;reason:string;remainingMinutes:number|null;remainingQuestions:number|null;workedOn:string;reassignFromDate:string;continuationMinutes?:number|null;continuationQuestions?:number|null}|null=null;
  saveRemainingScope(task:StudyTask){
    const plan=this.selected(),minutes=this.remainingMinutes[task.id],questions=this.remainingQuestions[task.id]??null;
    if(!plan||plan.status==='Draft'||this.readOnly||task.isCompleted||this.busy())return;
    if(!Number.isInteger(minutes)||minutes!<1||minutes!>1440||questions!=null&&(!Number.isInteger(questions)||questions<0||questions>100000)){this.error.set('Kalan süreyi ve soru sayısını kontrol et.');return;}
    this.mutate(this.service.setRemainingScope(plan.id,task.id,plan.version,minutes!,questions,this.staffStudentId),'Kalan kapsam kaydedildi. Gerçekleşme toplamı korundu.');
  }
  partial(task:StudyTask) {
    const plan=this.selected(); if(!plan||plan.status!=='Active'||this.staffStudentId||this.readOnly||task.isCompleted||this.busy())return;
    const minutes=this.actualMinutes[task.id],questions=this.actualQuestions[task.id]??null;
    if(!Number.isInteger(minutes)||minutes<1||minutes>1440||questions!=null&&(!Number.isInteger(questions)||questions<0||questions>100000)) {this.error.set('Süreyi ve soru sayısını kontrol et.');return;}
    const remainingMinutes=this.remainingMinutes[task.id],remainingQuestions=this.remainingQuestions[task.id]??null;
    if(!Number.isInteger(remainingMinutes)||remainingMinutes!<1||remainingMinutes!>1440||remainingQuestions!=null&&(!Number.isInteger(remainingQuestions)||remainingQuestions<0||remainingQuestions>100000)){this.error.set('Kısmi çalışma için kalan süreyi 1–1440 dakika olarak belirt. Bu süre yeni plana devredilecek iş miktarıdır.');return;}
    this.mutate(this.service.recordRealization(plan.id,task.id,{expectedVersion:plan.version,actualMinutes:minutes,actualQuestions:questions,isCompleted:false,remainingMinutes:remainingMinutes!,remainingQuestions,...(this.workDates[task.id]?{workedOn:this.workDates[task.id]}:{})}),'Kısmi çalışma toplamı kaydedildi.');
  }
  startCorrection(task:StudyTask) {
    if(this.staffStudentId||this.readOnly||this.busy()||task.actualMinutes==null&&!task.realizationHistory?.length)return;
    this.correction={taskId:task.id,minutes:task.actualMinutes??0,questions:task.actualQuestions??null,completed:task.isCompleted&&task.actualMinutes!=null,reason:'',remainingMinutes:task.remainingMinutes??null,remainingQuestions:task.remainingQuestions??null,workedOn:task.workDays?.at(-1)?.workedOn??this.today,reassignFromDate:'',continuationMinutes:null,continuationQuestions:null};
  }
  continuationScopeRequired(task:StudyTask):boolean {
    const form=this.correction,active=task.continuation;
    if(!form||form.taskId!==task.id||!active||active.hasOwnCompletion||!active.requiresExplicitScope||form.completed)return false;
    const remaining=form.minutes===0?null:form.remainingMinutes??task.remainingMinutes??null;
    const questions=form.minutes===0?null:form.remainingQuestions??task.remainingQuestions??null;
    return active.isCompleted||task.isCompleted!==form.completed||(task.remainingMinutes??null)!==remaining||(task.remainingQuestions??null)!==questions;
  }
  saveCorrection() {
    const plan=this.selected(),form=this.correction;
    if(!plan||!form||this.staffStudentId||this.readOnly||this.busy())return;
    if(!Number.isInteger(form.minutes)||form.minutes<0||form.minutes>1440||form.minutes===0&&(form.completed||(form.questions??0)>0)||form.questions!=null&&(!Number.isInteger(form.questions)||form.questions<0||form.questions>100000)||form.reason.trim().length<5||form.reason.trim().length>1000){this.error.set('Süreyi, soru sayısını ve en az 5 karakterlik gerekçeyi kontrol et.');return;}
    if(!form.completed&&form.minutes>0&&form.remainingMinutes!=null&&(!Number.isInteger(form.remainingMinutes)||form.remainingMinutes<1||form.remainingMinutes>1440)){this.error.set('Kalan süreyi kontrol et.');return;}
    const task=plan.tasks.find(x=>x.id===form.taskId);
    const active=task?.continuation;
    const reconcile=!!task&&this.continuationScopeRequired(task);
    if(reconcile&&(!Number.isInteger(form.continuationMinutes)||form.continuationMinutes!<1||form.continuationMinutes!>1440||form.continuationQuestions!=null&&(!Number.isInteger(form.continuationQuestions)||form.continuationQuestions<0||form.continuationQuestions>100000))){this.error.set('Aktif devamın güncel kalan süresini ve soru sayısını belirt. Sonraki çalışmanın kapsamı geçmiş süreden tahmin edilmez.');return;}
    const scope=!form.completed&&form.minutes>0&&form.remainingMinutes!=null?{remainingMinutes:form.remainingMinutes,remainingQuestions:form.remainingQuestions}:{};
    const continuation=reconcile&&active?{continuationScope:{revisionId:active.revisionId,expectedVersion:active.revisionVersion,remainingMinutes:form.continuationMinutes!,remainingQuestions:form.continuationQuestions??null}}:{};
    this.mutate(this.service.recordRealization(plan.id,form.taskId,{expectedVersion:plan.version,actualMinutes:form.minutes,actualQuestions:form.questions,isCompleted:form.completed,reason:form.reason.trim(),workedOn:form.workedOn,...(form.reassignFromDate?{reassignFromDate:form.reassignFromDate}:{}),...scope,...continuation},true),'Gerçekleşme gerekçesiyle düzeltildi; önceki kayıt korundu ve aktif devam uzlaştırıldı.');
  }

  complete(task: StudyTask, actualMinutes: number, actualQuestions: number | null = null) {
    if (this.staffStudentId || this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Active' || task.isCompleted || this.busy()) return;
    if (!Number.isInteger(actualMinutes) || actualMinutes < 1 || actualMinutes > 1440) {
      this.completionError.set({ taskId: task.id, field: 'minutes', message: 'Gerçek çalışma süresini 1–1440 dakika arasında gir.' });
      return;
    }
    if (actualQuestions != null && (!Number.isInteger(actualQuestions) || actualQuestions < 0 || actualQuestions > 100000)) {
      this.completionError.set({ taskId: task.id, field: 'questions', message: 'Gerçek soru sayısını 0–100000 arasında tam sayı olarak gir.' }); return;
    }
    this.completionError.set(null);
    this.mutate(this.service.complete(plan.id, task.id, plan.version, actualMinutes, actualQuestions,...(this.workDates[task.id]?[this.workDates[task.id]]:[])), 'Çalışma tamamlandı.');
  }

  reschedule(task: StudyTask, plannedDate: string) {
    if (this.readOnly) return;
    const plan = this.selected();
    if (!plan || plan.status !== 'Active' || task.isCompleted || this.busy()) return;
    if (!plannedDate) { this.error.set('Yeni çalışma tarihini seç.'); return; }
    this.mutate(this.service.reschedule(plan.id, task.id, plan.version, plannedDate, ...this.studentArgument), 'Çalışma tarihi güncellendi.');
  }

  statusLabel(status: StudyPlanStatus) { return { Draft: 'Taslak', Active: 'Aktif', Archived: 'Arşiv' }[status]; }

  private applyPlan(plan: StudyPlan, preserveInputs = false) {
    const previous = this.selected();
    if(!preserveInputs||previous?.id!==plan.id)this.workDates={};
    if(previous?.id!==plan.id||!!previous?.weeklySource!==!!plan.weeklySource){this.planView.set(this.taskFilter!=='all'?'list':plan.weeklySource?'calendar':'list');this.selectedTaskIds.set([]);}
    // Roadmap O1: the student's active plan opens on today's work when there is any.
    if(!this.openedOnToday&&!this.staffStudentId&&!this.readOnly&&!this.requestedTaskId&&previous?.id!==plan.id&&plan.status==='Active'&&this.taskFilter==='all'){
      this.openedOnToday=true;const zone=plan.weeklySource?.timeZoneId||'Europe/Istanbul';let today=this.today;
      try{today=new Intl.DateTimeFormat('en-CA',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());}catch{/* keep Istanbul */}
      if(plan.tasks.some(t=>t.plannedDate===today)){this.taskFilter='today';this.planView.set('list');this.selectedTaskIds.set([]);}
    }
    const keepInputs = preserveInputs && previous?.id === plan.id && plan.status === 'Active';
    const minutes = this.actualMinutes; const questions = this.actualQuestions; const dates = this.targetDates;
    const titles=this.taskTitles;
    this.calendarForm=null;this.selected.set(plan);
    const focusTask = this.initialPlanId === plan.id ? this.initialTaskId : this.requestedTaskPlanId === plan.id ? this.requestedTaskId : null;
    if (previous?.id !== plan.id && focusTask && plan.tasks.some(task => task.id === focusTask)) {
      this.planView.set('calendar');
      this.selectCalendarTasks([focusTask]);
    }
    if (previous?.id !== plan.id && !this.staffStudentId) this.pickStudentDay(plan, focusTask);
    this.editing = plan.status === 'Draft' && !this.readOnly && !plan.weeklySource;
    this.taskPage=1;
    this.editTitle = plan.title;
    this.editTasks = plan.tasks.filter(task=>!task.predecessorTaskId).map(({ plannedDate, title, plannedMinutes, topicId, isPinned, sourceName, plannedQuestions,plannedStartMinute }) =>
      ({ plannedDate, title, plannedMinutes, topicId, isPinned, sourceName: sourceName ?? null, plannedQuestions: plannedQuestions ?? null,plannedStartMinute:plannedStartMinute??null }));
    this.actualMinutes = Object.fromEntries(plan.tasks.map(task => {
      const old = previous?.tasks.find(item => item.id === task.id);
      const changed = old && minutes[task.id] !== (old.actualMinutes ?? old.plannedMinutes);
      return [task.id, keepInputs && !task.isCompleted && Object.hasOwn(minutes, task.id) && changed ? minutes[task.id] : task.actualMinutes ?? task.plannedMinutes];
    }));
    this.actualQuestions = Object.fromEntries(plan.tasks.map(task => [task.id, keepInputs && !task.isCompleted && Object.hasOwn(questions, task.id) ? questions[task.id] : task.actualQuestions ?? null]));
    this.remainingMinutes=Object.fromEntries(plan.tasks.map(task=>[task.id,keepInputs&&Object.hasOwn(this.remainingMinutes,task.id)?this.remainingMinutes[task.id]:task.remainingMinutes??null]));
    this.remainingQuestions=Object.fromEntries(plan.tasks.map(task=>[task.id,keepInputs&&Object.hasOwn(this.remainingQuestions,task.id)?this.remainingQuestions[task.id]:task.remainingQuestions??null]));
    this.targetDates = Object.fromEntries(plan.tasks.map(task => [task.id, keepInputs && !task.isCompleted && dates[task.id] && dates[task.id] !== previous?.tasks.find(old => old.id === task.id)?.plannedDate ? dates[task.id] : task.plannedDate]));
    this.taskTitles=Object.fromEntries(plan.tasks.map(t=>[t.id,keepInputs&&!t.isCompleted&&titles[t.id]&&titles[t.id]!==previous?.tasks.find(old=>old.id===t.id)?.title?titles[t.id]:t.title]));
    this.correction=null;
    this.dirty = false;
    this.publishConfirmed = false;
    this.completionError.set(null);
    this.restoreConfirmed=false;
    if (plan.status === 'Draft') this.knownDraftId.set(plan.id);
    else if (this.knownDraftId() === plan.id) this.knownDraftId.set(null);
    if (!this.staffStudentId && this.route && this.router) {
      void this.router.navigate([], { relativeTo: this.route, queryParams: { plan: plan.id }, queryParamsHandling: 'merge', replaceUrl: true });
    }
  }

  private hasUnsavedTaskInputs() {
    const plan = this.selected();
    return !this.readOnly && plan?.status === 'Active' && plan.tasks.some(task => !task.isCompleted && (
      this.actualMinutes[task.id] !== (task.actualMinutes ?? task.plannedMinutes)
      || (this.actualQuestions[task.id] ?? null) !== (task.actualQuestions ?? null)
      || (this.remainingMinutes[task.id]??null)!==(task.remainingMinutes??null) || (this.remainingQuestions[task.id]??null)!==(task.remainingQuestions??null)
      || !!this.workDates[task.id] && this.workDates[task.id]!==this.today || this.targetDates[task.id] !== task.plannedDate || !!this.staffStudentId&&this.taskTitles[task.id]!==task.title));
  }

  private async canLeaveEditor() {
    if (this.busy()) return false;
    if (this.dirty) { this.error.set('Önce taslağını kaydet veya değişikliklerden vazgeç.'); return false; }
    if ((this.calendarForm || this.correction || this.hasUnsavedTaskInputs()) && (typeof window === 'undefined' || !await this.toaster.confirm('Kaydedilmemiş çalışma bilgilerin var. Değişikliklerden vazgeçmek istiyor musun?'))) return false;
    this.success.set(null);
    return true;
  }

  private mutate(request: Observable<StudyPlan>, message: string) {
    this.busy.set(true);
    this.error.set(null);
    this.success.set(null);
    request.pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: plan => { this.calendarForm=null;this.applyPlan(plan, true); this.success.set(message); this.loadList(this.page().pageNumber); },
      error: error => this.showError(error, 'İşlem tamamlanamadı. Tekrar dene.')
    });
  }

  private showError(error: unknown, fallback: string) {
    const response = error as { status?: number; error?: { message?: unknown } };
    const message = response?.error?.message;
    this.error.set(typeof message === 'string' ? message : fallback);
    if (response?.status === 409) this.error.update(value => `${value} Planı yeniden açarak güncel durumu kontrol et.`);
  }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
