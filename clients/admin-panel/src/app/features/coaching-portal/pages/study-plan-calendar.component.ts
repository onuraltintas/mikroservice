import {CommonModule} from '@angular/common';
import {Component,ElementRef,EventEmitter,HostListener,Injector,Input,OnChanges,Output,SimpleChanges,ViewChild,afterNextRender,computed,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {lessonColors} from './study-plan-colors';
import {StudyPlan} from '../../../core/services/coaching-study-planning.service';
import {CalendarView,calendarDays,clock} from './weekly-calendar';
import {CalendarMove,PlanCalendarBlock,calendarBlocks,calendarDate,calendarFocusDate,calendarMoveError,calendarTaskOverdue,calendarToday,dateKey} from './study-plan-calendar';

export interface CalendarAction {kind:'add'|'edit'|'delete'|'complete';taskIds:string[];date:string;startMinute:number}

@Component({selector:'app-study-plan-calendar',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./study-plan-calendar.component.html',styleUrl:'./study-plan-calendar.component.scss'})
export class StudyPlanCalendarComponent implements OnChanges {
  @Input({required:true}) plan!:StudyPlan;
  @Input() disabled=false;
  @Input() editable=false;
  @Input() student=true;
  @Output() calendarAction=new EventEmitter<CalendarAction>();
  readonly pixelsPerMinute=2;
  private colors=new Map<string,string>();
  menu:{x:number;y:number;date:string;startMinute:number;block:PlanCalendarBlock|null}|null=null;
  private menuTrigger:HTMLElement|null=null;
  @Output() taskSelected=new EventEmitter<string[]>();
  @Output() tasksMoved=new EventEmitter<CalendarMove>();
  readonly moveError=signal('');readonly preview=signal<(CalendarMove&{minutes:number;lesson:string})|null>(null);
  moveForm:{id:string;date:string;time:string}|null=null;
  private gesture:{pointer:number;block:PlanCalendarBlock;x:number;y:number;offset:number;moved:boolean}|null=null;
  private suppressClick=false;
  private readonly injector=inject(Injector);private readonly host=inject<ElementRef<HTMLElement>>(ElementRef);private currentId:string|null=null;private scrollerElement?:HTMLElement;private focusPage=false;
  private blocks:PlanCalendarBlock[]=[];private byDate=new Map<string,PlanCalendarBlock[]>();
  readonly view=signal<CalendarView>(typeof window!=='undefined'&&window.innerWidth<680?'day':'week');readonly anchor=signal(new Date());
  readonly days=computed(()=>calendarDays(this.anchor(),this.view()));readonly clock=clock;readonly dateKey=dateKey;readonly calendarDateFor=calendarDate;
  readonly views=[{id:'day' as const,label:'Gün'},{id:'week' as const,label:'Hafta'},{id:'month' as const,label:'Ay'}];
  readonly hours=Array.from({length:24},(_,i)=>i);readonly weekLabels=['Pzt','Sal','Çar','Per','Cum','Cmt','Paz'];
  get zone(){return this.plan.weeklySource?.timeZoneId??'Europe/Istanbul';}
  @ViewChild('scroller') set scroller(value:ElementRef<HTMLElement>|undefined){this.scrollerElement=value?.nativeElement;if(value)this.focusHours();}
  ngOnChanges(changes:SimpleChanges){
    this.cancelMove();this.closeMenu();
    if(!changes['plan'])return;
    this.colors=lessonColors(this.plan.tasks);this.blocks=calendarBlocks(this.plan.tasks);this.byDate=new Map();
    for(const block of this.blocks){const list=this.byDate.get(block.date)??[];list.push(block);this.byDate.set(block.date,list);}
    if(this.currentId!==this.plan.id){this.currentId=this.plan.id;this.focusPage=true;this.focusWork();}
  }
  rows(day:Date){return this.byDate.get(dateKey(day))??[];}
  timed(day:Date){return this.rows(day).filter(b=>b.startMinute!==null);}
  untimed(day:Date){return this.rows(day).filter(b=>b.startMinute===null);}
  hasTimed(){return this.days().some(d=>this.timed(d).length);}
  hasUntimed(){return this.days().some(d=>this.untimed(d).length);}
  hasWork(){return this.days().some(d=>this.rows(d).length);}
  hasAny(){return this.blocks.length>0;}
  changeView(value:CalendarView){this.view.set(value);this.focusHours();}
  openDay(day:Date){this.anchor.set(new Date(day));this.changeView('day');}
  goToDate(value:string){if(value){this.anchor.set(calendarDate(value));this.focusHours();}}
  today(){this.goToDate(calendarToday(this.zone));}
  focusWork(){this.goToDate(calendarFocusDate(this.blocks,calendarToday(this.zone)));}
  adjacent(step:number){const dates=[...this.byDate.keys()],key=dateKey(this.anchor());return step>0?dates.find(d=>d>key):dates.filter(d=>d<key).at(-1);}
  occupied(step:number){const value=this.adjacent(step);if(value)this.goToDate(value);}
  navigate(step:number){
    const a=this.anchor(),month=this.view()==='month';
    const next=month?new Date(a.getFullYear(),a.getMonth()+step,Math.min(a.getDate(),new Date(a.getFullYear(),a.getMonth()+step+1,0).getDate())):new Date(a.getFullYear(),a.getMonth(),a.getDate()+step*(this.view()==='week'?7:1));
    this.goToDate(dateKey(next));
  }
  title(){const a=this.anchor();return this.view()==='month'?a.toLocaleDateString('tr-TR',{month:'long',year:'numeric'}):this.view()==='day'?this.dayTitle(a):`${this.days()[0].toLocaleDateString('tr-TR',{day:'numeric',month:'short'})} – ${this.days()[6].toLocaleDateString('tr-TR',{day:'numeric',month:'short',year:'numeric'})}`;}
  dayTitle(day:Date){return day.toLocaleDateString('tr-TR',{day:'numeric',month:'long',year:'numeric',weekday:'long'});}
  shortDay(day:Date){return day.toLocaleDateString('tr-TR',{day:'2-digit',month:'2-digit',weekday:'short'});}
  columns(){return `44px repeat(${this.days().length},minmax(${this.view()==='week'?150:240}px,1fr))`;}
  done(block:PlanCalendarBlock){return block.tasks.filter(t=>t.isCompleted).length;}
  status(block:PlanCalendarBlock){const done=this.done(block);if(done===block.tasks.length)return 'Tamamlandı';const overdue=block.tasks.some(task=>calendarTaskOverdue(task,this.zone));if(done)return `Kısmen tamamlandı · ${done}/${block.tasks.length}${overdue?' · gecikti':''}`;return overdue?'Gecikti · tamamlanmadı':'Bekliyor';}
  statusClass(block:PlanCalendarBlock){const done=this.done(block);return done===block.tasks.length?'ui-status--success':done?'ui-status--info':block.tasks.some(task=>calendarTaskOverdue(task,this.zone))?'ui-status--danger':'ui-status--warning';}
  color(block:PlanCalendarBlock){return this.colors.get(block.lessonKey)??'hsl(160 62% 44%)';}
  writable(){return this.editable&&!this.disabled&&this.plan.status!=='Archived';}
  context(event:MouseEvent,day:Date,block:PlanCalendarBlock|null=null){
    if(this.disabled)return;
    event.preventDefault();event.stopPropagation();this.cancelMove();
    if(!block&&!this.writable())return;
    const target=event.currentTarget as HTMLElement;
    const minute=block?.startMinute??(target.classList.contains('day-track')?Math.floor((event.clientY-target.getBoundingClientRect().top)/this.pixelsPerMinute/30)*30:540);
    this.openMenu(target,event.clientX,event.clientY,dateKey(day),Math.max(0,Math.min(1410,minute)),block);
  }
  blockMenu(event:MouseEvent,block:PlanCalendarBlock){this.context(event,calendarDate(block.date),block);}
  menuButton(event:MouseEvent,block:PlanCalendarBlock){
    event.preventDefault();event.stopPropagation();const el=event.currentTarget as HTMLElement,rect=el.getBoundingClientRect();
    this.openMenu(el,rect.left,rect.bottom,block.date,block.startMinute??540,block);
  }
  keyboardMenu(event:KeyboardEvent,day:Date,block:PlanCalendarBlock|null=null){
    if(event.key!=='ContextMenu'&&!(event.shiftKey&&event.key==='F10'))return;
    event.preventDefault();event.stopPropagation();const el=event.currentTarget as HTMLElement,rect=el.getBoundingClientRect();
    if(!this.disabled&&(block||this.writable()))this.openMenu(el,rect.left,rect.top,dateKey(day),block?.startMinute??540,block);
  }
  private openMenu(trigger:HTMLElement,x:number,y:number,date:string,startMinute:number,block:PlanCalendarBlock|null){
    this.menuTrigger=trigger;this.menu={x:Math.max(8,Math.min(x,window.innerWidth-256)),y:Math.max(8,Math.min(y,window.innerHeight-370)),date,startMinute,block};
    afterNextRender(()=>this.host.nativeElement.querySelector<HTMLElement>('[role="menu"] button:not(:disabled)')?.focus(),{injector:this.injector});
  }
  closeMenu(restore=false){this.menu=null;if(restore)this.menuTrigger?.focus();}
  menuKey(event:KeyboardEvent){
    if(event.key==='Escape'){event.stopPropagation();this.closeMenu(true);return;}
    if(event.key==='Tab'){this.closeMenu();return;}
    if(!['ArrowDown','ArrowUp','Home','End'].includes(event.key))return;
    event.preventDefault();const items=[...this.host.nativeElement.querySelectorAll<HTMLButtonElement>('[role="menu"] button:not(:disabled)')];
    const current=items.indexOf(document.activeElement as HTMLButtonElement),step=event.key==='ArrowUp'?-1:1;
    items[event.key==='Home'?0:event.key==='End'?items.length-1:(current+step+items.length)%items.length]?.focus();
  }
  action(kind:CalendarAction['kind']){
    const m=this.menu;if(!m)return;
    if(!this.writable()||kind!=='add'&&!m.block||kind==='complete'&&(!this.student||this.plan.status!=='Active'))return;
    const ids=m.block?.tasks.filter(t=>!t.isCompleted).map(t=>t.id)??[];
    if(kind!=='add'&&!ids.length)return;
    this.closeMenu();this.calendarAction.emit({kind,taskIds:kind==='add'?[]:ids,date:m.date,startMinute:m.startMinute});
  }
  menuDetails(){const b=this.menu?.block;this.closeMenu();if(b)this.taskSelected.emit(b.tasks.map(t=>t.id));}
  menuMove(){const b=this.menu?.block;this.closeMenu();if(b&&this.canMove(b)){this.moveForm={id:b.id,date:b.date,time:clock(b.startMinute!)};afterNextRender(()=>{const field=this.host.nativeElement.querySelector<HTMLElement>('.move-form select');field?.focus();field?.scrollIntoView?.({block:'nearest'});},{injector:this.injector});}}
  @HostListener('document:click',['$event']) outsideMenu(event:MouseEvent){if(!(event.target as HTMLElement).closest('.calendar-context-menu'))this.closeMenu();}
  range(block:PlanCalendarBlock){return block.startMinute===null?'Saati belirtilmemiş':`${clock(block.startMinute)}–${clock(block.startMinute+block.minutes)}`;}
  label(block:PlanCalendarBlock){return `${block.lesson} · ${this.dayTitle(calendarDate(block.date))} · ${this.range(block)} · ${block.minutes} dk · ${block.tasks.length} çalışma · ${this.status(block)}`;}
  select(block:PlanCalendarBlock){if(this.suppressClick){this.suppressClick=false;return;}this.taskSelected.emit(block.tasks.map(t=>t.id));}
  canMove(block:PlanCalendarBlock){return this.editable&&!this.disabled&&this.plan.status!=='Archived'&&block.startMinute!==null&&!block.tasks.some(t=>t.isCompleted);}
  movable(){return this.blocks.filter(b=>this.canMove(b));}
  openMoveForm(){const b=this.movable()[0];if(b)this.moveForm={id:b.id,date:b.date,time:clock(b.startMinute!)};this.moveError.set('');}
  chooseMoveBlock(id:string){const b=this.blocks.find(b=>b.id===id);if(b&&this.moveForm)this.moveForm={id,date:b.date,time:clock(b.startMinute!)};}
  applyMoveForm(){if(!this.moveForm)return;const b=this.blocks.find(b=>b.id===this.moveForm!.id),time=this.moveForm.time;
    if(!b||!/^([01]\d|2[0-3]):[0-5]\d$/.test(time)){this.moveError.set('Geçerli bir saat seç.');return;}
    const [h,m]=time.split(':').map(Number);this.requestMove(b,this.moveForm.date,h*60+m);
  }
  requestMove(block:PlanCalendarBlock,date:string,start:number){
    if(!this.canMove(block))return;
    this.moveError.set(calendarMoveError(block,date,start,this.plan.tasks));if(this.moveError())return;
    if(block.date===date&&block.startMinute===start){this.moveForm=null;return;}
    this.moveForm=null;this.tasksMoved.emit({taskIds:block.tasks.map(t=>t.id),plannedDate:date,startMinute:start});
  }
  beginMove(e:PointerEvent,block:PlanCalendarBlock){
    this.suppressClick=false;
    if(!this.canMove(block)||e.button!==0||!e.isPrimary)return;
    const card=(e.currentTarget as HTMLElement);
    this.gesture={pointer:e.pointerId,block,x:e.clientX,y:e.clientY,offset:this.view()==='month'?0:e.clientY-card.getBoundingClientRect().top,moved:false};
  }
  @HostListener('document:pointermove',['$event']) movePointer(e:PointerEvent){
    const g=this.gesture;if(!g||e.pointerId!==g.pointer)return;
    if(!g.moved&&Math.hypot(e.clientX-g.x,e.clientY-g.y)<5)return;
    g.moved=true;e.preventDefault();
    const track=document.elementFromPoint(e.clientX,e.clientY)?.closest<HTMLElement>('.day-track,.month-day');
    if(!track||!this.host.nativeElement.contains(track)){this.preview.set(null);return;}
    const start=track.classList.contains('month-day')?g.block.startMinute!:Math.round((e.clientY-track.getBoundingClientRect().top-g.offset)/this.pixelsPerMinute/30)*30;
    const date=track.dataset['date']!;this.preview.set({taskIds:g.block.tasks.map(t=>t.id),plannedDate:date,startMinute:start,minutes:g.block.minutes,lesson:g.block.lesson});
    this.moveError.set(calendarMoveError(g.block,date,start,this.plan.tasks));
  }
  @HostListener('document:pointerup',['$event']) endMove(e:PointerEvent){
    const g=this.gesture;if(!g||e.pointerId!==g.pointer)return;const p=this.preview();this.cancelMove();
    if(!g.moved)return;this.suppressClick=true;
    if(p)this.requestMove(g.block,p.plannedDate,p.startMinute);else this.moveError.set('Kartı takvimde boş bir saate bırak.');
  }
  @HostListener('document:pointercancel') cancelMove(){this.gesture=null;this.preview.set(null);}
  @HostListener('document:keydown.escape') escapeMove(){this.cancelMove();this.closeMenu(true);this.moveForm=null;this.moveError.set('');}
  showDay(){this.taskSelected.emit(this.rows(this.anchor()).flatMap(b=>b.tasks.map(t=>t.id)));}
  private focusHours(){afterNextRender(()=>{
    const anchorStarts=this.timed(this.anchor()).map(b=>b.startMinute!);
    const starts=anchorStarts.length?anchorStarts:this.days().flatMap(d=>this.timed(d).map(b=>b.startMinute!));
    if(this.scrollerElement)this.scrollerElement.scrollTop=starts.length?Math.max(0,Math.min(...starts)-30)*this.pixelsPerMinute:480*this.pixelsPerMinute;
    for(const element of this.host.nativeElement.querySelectorAll<HTMLElement>('.time-scroller,.untimed-wrap')){
      const timed=element.classList.contains('time-scroller'),dates=this.days().filter(d=>(timed?this.timed(d):this.untimed(d)).length);
      const day=dates.find(d=>dateKey(d)===dateKey(this.anchor()))??dates[0];
      const column=day?element.querySelector<HTMLElement>(`[data-date="${dateKey(day)}"]`):null;
      if(column)element.scrollLeft=Math.max(0,element.scrollLeft+column.getBoundingClientRect().left-element.getBoundingClientRect().left-44);
    }
    if(this.focusPage){this.focusPage=false;this.host.nativeElement.scrollIntoView?.({block:'start'});}
  },{injector:this.injector});}
}
