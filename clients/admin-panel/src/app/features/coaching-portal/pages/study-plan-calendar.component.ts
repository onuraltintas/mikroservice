import {CommonModule} from '@angular/common';
import {Component,ElementRef,EventEmitter,HostListener,Injector,Input,OnChanges,Output,SimpleChanges,ViewChild,afterNextRender,computed,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {StudyPlan} from '../../../core/services/coaching-study-planning.service';
import {CalendarView,calendarDays,clock} from './weekly-calendar';
import {CalendarMove,PlanCalendarBlock,calendarBlocks,calendarDate,calendarFocusDate,calendarMoveError,calendarToday,dateKey} from './study-plan-calendar';

@Component({selector:'app-study-plan-calendar',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./study-plan-calendar.component.html',styleUrl:'./study-plan-calendar.component.scss'})
export class StudyPlanCalendarComponent implements OnChanges {
  @Input({required:true}) plan!:StudyPlan;
  @Input() disabled=false;
  @Input() editable=false;
  @Output() taskSelected=new EventEmitter<string[]>();
  @Output() tasksMoved=new EventEmitter<CalendarMove>();
  readonly moveError=signal('');readonly preview=signal<(CalendarMove&{minutes:number;lesson:string})|null>(null);
  moveForm:{id:string;date:string;time:string}|null=null;
  private gesture:{pointer:number;block:PlanCalendarBlock;x:number;y:number;offset:number;moved:boolean}|null=null;
  private suppressClick=false;
  private readonly injector=inject(Injector);private readonly host=inject<ElementRef<HTMLElement>>(ElementRef);private currentId:string|null=null;private scrollerElement?:HTMLElement;private focusPage=false;
  private blocks:PlanCalendarBlock[]=[];private byDate=new Map<string,PlanCalendarBlock[]>();
  readonly view=signal<CalendarView>(typeof window!=='undefined'&&window.innerWidth<680?'day':'week');readonly anchor=signal(new Date());
  readonly days=computed(()=>calendarDays(this.anchor(),this.view()));readonly clock=clock;readonly dateKey=dateKey;
  readonly views=[{id:'day' as const,label:'Gün'},{id:'week' as const,label:'Hafta'},{id:'month' as const,label:'Ay'}];
  readonly hours=Array.from({length:24},(_,i)=>i);readonly weekLabels=['Pzt','Sal','Çar','Per','Cum','Cmt','Paz'];
  get zone(){return this.plan.weeklySource?.timeZoneId??'Europe/Istanbul';}
  @ViewChild('scroller') set scroller(value:ElementRef<HTMLElement>|undefined){this.scrollerElement=value?.nativeElement;if(value)this.focusHours();}
  ngOnChanges(changes:SimpleChanges){
    this.cancelMove();
    if(!changes['plan'])return;
    this.blocks=calendarBlocks(this.plan.tasks);this.byDate=new Map();
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
  columns(){return `44px repeat(${this.days().length},minmax(${this.view()==='week'?110:200}px,1fr))`;}
  done(block:PlanCalendarBlock){return block.tasks.filter(t=>t.isCompleted).length;}
  color(block:PlanCalendarBlock){return [...block.lessonKey].reduce((sum,c)=>sum+c.charCodeAt(0),0)%5;}
  range(block:PlanCalendarBlock){return block.startMinute===null?'Saati belirtilmemiş':`${clock(block.startMinute)}–${clock(block.startMinute+block.minutes)}`;}
  label(block:PlanCalendarBlock){return `${block.lesson} · ${this.dayTitle(calendarDate(block.date))} · ${this.range(block)} · ${block.minutes} dk · ${block.tasks.length} çalışma · ${this.done(block)} tamamlandı`;}
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
    const start=track.classList.contains('month-day')?g.block.startMinute!:Math.round((e.clientY-track.getBoundingClientRect().top-g.offset)/30)*30;
    const date=track.dataset['date']!;this.preview.set({taskIds:g.block.tasks.map(t=>t.id),plannedDate:date,startMinute:start,minutes:g.block.minutes,lesson:g.block.lesson});
    this.moveError.set(calendarMoveError(g.block,date,start,this.plan.tasks));
  }
  @HostListener('document:pointerup',['$event']) endMove(e:PointerEvent){
    const g=this.gesture;if(!g||e.pointerId!==g.pointer)return;const p=this.preview();this.cancelMove();
    if(!g.moved)return;this.suppressClick=true;
    if(p)this.requestMove(g.block,p.plannedDate,p.startMinute);else this.moveError.set('Kartı takvimde boş bir saate bırak.');
  }
  @HostListener('document:pointercancel') cancelMove(){this.gesture=null;this.preview.set(null);}
  @HostListener('document:keydown.escape') escapeMove(){this.cancelMove();this.moveForm=null;this.moveError.set('');}
  showDay(){this.taskSelected.emit(this.rows(this.anchor()).flatMap(b=>b.tasks.map(t=>t.id)));}
  private focusHours(){afterNextRender(()=>{
    const anchorStarts=this.timed(this.anchor()).map(b=>b.startMinute!);
    const starts=anchorStarts.length?anchorStarts:this.days().flatMap(d=>this.timed(d).map(b=>b.startMinute!));
    if(this.scrollerElement)this.scrollerElement.scrollTop=starts.length?Math.max(0,Math.min(...starts)-30):480;
    for(const element of this.host.nativeElement.querySelectorAll<HTMLElement>('.time-scroller,.untimed-wrap')){
      const timed=element.classList.contains('time-scroller'),dates=this.days().filter(d=>(timed?this.timed(d):this.untimed(d)).length);
      const day=dates.find(d=>dateKey(d)===dateKey(this.anchor()))??dates[0];
      const column=day?element.querySelector<HTMLElement>(`[data-date="${dateKey(day)}"]`):null;
      if(column)element.scrollLeft=Math.max(0,element.scrollLeft+column.getBoundingClientRect().left-element.getBoundingClientRect().left-44);
    }
    if(this.focusPage){this.focusPage=false;this.host.nativeElement.scrollIntoView?.({block:'start'});}
  },{injector:this.injector});}
}
