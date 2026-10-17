import {CommonModule} from '@angular/common';
import {Component,ElementRef,EventEmitter,Injector,Input,OnChanges,Output,SimpleChanges,ViewChild,afterNextRender,computed,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {StudyPlan} from '../../../core/services/coaching-study-planning.service';
import {CalendarView,calendarDays,clock} from './weekly-calendar';
import {PlanCalendarBlock,calendarBlocks,calendarDate,calendarFocusDate,calendarToday,dateKey} from './study-plan-calendar';

@Component({selector:'app-study-plan-calendar',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./study-plan-calendar.component.html',styleUrl:'./study-plan-calendar.component.scss'})
export class StudyPlanCalendarComponent implements OnChanges {
  @Input({required:true}) plan!:StudyPlan;
  @Input() disabled=false;
  @Output() taskSelected=new EventEmitter<string[]>();
  private readonly injector=inject(Injector);private readonly host=inject<ElementRef<HTMLElement>>(ElementRef);private currentId:string|null=null;private scrollerElement?:HTMLElement;private focusPage=false;
  private blocks:PlanCalendarBlock[]=[];private byDate=new Map<string,PlanCalendarBlock[]>();
  readonly view=signal<CalendarView>(typeof window!=='undefined'&&window.innerWidth<680?'day':'week');readonly anchor=signal(new Date());
  readonly days=computed(()=>calendarDays(this.anchor(),this.view()));readonly clock=clock;readonly dateKey=dateKey;
  readonly views=[{id:'day' as const,label:'Gün'},{id:'week' as const,label:'Hafta'},{id:'month' as const,label:'Ay'}];
  readonly hours=Array.from({length:24},(_,i)=>i);readonly weekLabels=['Pzt','Sal','Çar','Per','Cum','Cmt','Paz'];
  get zone(){return this.plan.weeklySource?.timeZoneId??'Europe/Istanbul';}
  @ViewChild('scroller') set scroller(value:ElementRef<HTMLElement>|undefined){this.scrollerElement=value?.nativeElement;if(value)this.focusHours();}
  ngOnChanges(changes:SimpleChanges){
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
  select(block:PlanCalendarBlock){this.taskSelected.emit(block.tasks.map(t=>t.id));}
  showDay(){this.taskSelected.emit(this.rows(this.anchor()).flatMap(b=>b.tasks.map(t=>t.id)));}
  private focusHours(){afterNextRender(()=>{
    const starts=this.days().flatMap(d=>this.timed(d).map(b=>b.startMinute!));
    if(this.scrollerElement)this.scrollerElement.scrollTop=starts.length?Math.max(0,Math.min(...starts)-30):480;
    if(this.focusPage){this.focusPage=false;this.host.nativeElement.scrollIntoView?.({block:'start'});}
  },{injector:this.injector});}
}
