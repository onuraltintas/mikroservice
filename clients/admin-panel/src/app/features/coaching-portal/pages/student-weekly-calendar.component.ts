import {CommonModule} from '@angular/common';
import {Component,DestroyRef,ElementRef,HostListener,OnInit,ViewChild,computed,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {CdkTrapFocus} from '@angular/cdk/a11y';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {CoachingWeeklyDraftService,WeeklyLesson} from '../../../core/services/coaching-weekly-draft.service';
import {CalendarView,WeeklySlot,Weekday,calendarDays,clock,slotError,weekday,weekDays} from './weekly-calendar';

interface Gesture {id:number;slot:WeeklySlot;mode:'move'|'top'|'bottom';startX:number;startY:number;offset:number;moved:boolean}
@Component({selector:'app-student-weekly-calendar',standalone:true,imports:[CommonModule,FormsModule,RouterLink,CdkTrapFocus],
  templateUrl:'./student-weekly-calendar.component.html',styleUrl:'./student-weekly-calendar.component.scss'})
export class StudentWeeklyCalendarComponent implements OnInit {
  private readonly service=inject(CoachingWeeklyDraftService);private readonly destroy=inject(DestroyRef);
  readonly loading=signal(true);readonly saving=signal(false);readonly ready=signal(false);readonly error=signal('');readonly message=signal('');readonly conflict=signal(false);
  readonly lessons=signal<WeeklyLesson[]>([]);readonly slots=signal<WeeklySlot[]>([]);readonly view=signal<CalendarView>('week');readonly anchor=signal(new Date());
  readonly preview=signal<WeeklySlot|null>(null);readonly days=computed(()=>calendarDays(this.anchor(),this.view()));
  readonly weeklyMinutes=computed(()=>this.slots().reduce((sum,s)=>sum+s.durationMinutes,0));
  readonly displayed=computed(()=>this.preview()?this.slots().map(s=>s.id===this.preview()!.id?this.preview()!:s):this.slots());
  readonly weekDays=weekDays;readonly clock=clock;readonly weekday=weekday;readonly minutes=Array.from({length:48},(_,i)=>i*30);readonly views=[{id:'day' as const,label:'Gün'},{id:'week' as const,label:'Hafta'},{id:'month' as const,label:'Ay'}];
  timeZoneId='Europe/Istanbul';version:number|null=null;private initial='';private gesture:Gesture|null=null;private suppressClick=false;
  menu:{x:number;y:number;date:Date;minute:number}|null=null;
  form:{id:string;lessonId:string;day:Weekday;start:string;duration:number}|null=null;formError='';
  private initializedScroller?:HTMLElement;
  @ViewChild('scroller') set scroller(value:ElementRef<HTMLElement>|undefined){
    if(value&&value.nativeElement!==this.initializedScroller){this.initializedScroller=value.nativeElement;value.nativeElement.scrollTop=8*72;}
  }
  ngOnInit(){if(typeof window!=='undefined'&&window.innerWidth<680)this.view.set('day');this.load();}
  private snapshot(){return JSON.stringify({timeZoneId:this.timeZoneId,slots:this.slots()});}
  dirty(){return this.ready()&&this.snapshot()!==this.initial;}
  busy(){return this.loading()||this.saving();}
  load(discard=false){
    if(this.busy()&&this.ready()||this.saving())return;
    if(this.dirty()&&(!discard||!window.confirm('Kaydedilmemiş haftalık düzeni bırakıp kaydedileni yüklemek istiyor musun?')))return;
    this.loading.set(true);this.error.set('');this.menu=null;this.form=null;
    this.service.get().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{
      this.lessons.set(r.lessons);this.slots.set(r.draft?.slots??[]);this.version=r.draft?.version??null;this.timeZoneId=r.draft?.timeZoneId??'Europe/Istanbul';
      this.initial=this.snapshot();this.ready.set(true);this.conflict.set(false);this.loading.set(false);this.today();
    },error:e=>{this.loading.set(false);this.fail(e,'Haftalık taslak yüklenemedi. Yeniden deneyebilirsin.');}});
  }
  save(){
    if(!this.ready()||this.busy()||!this.dirty()||this.conflict()||this.gesture||this.form)return;
    const invalid=this.slots().map(s=>slotError(s,this.slots())).find(Boolean);
    if(invalid){this.error.set(invalid);return;}
    this.saving.set(true);this.error.set('');this.message.set('');this.menu=null;
    this.service.save(this.version,this.timeZoneId,this.slots()).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{
      this.slots.set(r.slots);this.version=r.version;this.timeZoneId=r.timeZoneId;this.initial=this.snapshot();this.saving.set(false);this.message.set('Haftalık taslak kaydedildi. Mevcut planın korunuyor.');
    },error:e=>{this.saving.set(false);this.fail(e,'Taslak kaydedilemedi. Değişikliklerin bu ekranda duruyor; yeniden deneyebilirsin.');}});
  }
  private fail(e:{status?:number;error?:{message?:string}},fallback:string){if(e.status===409)this.conflict.set(true);this.error.set(e.error?.message??fallback);}
  changeView(view:CalendarView){this.escape();this.view.set(view);}
  navigate(step:number){
    const a=this.anchor();let next:Date;
    if(this.view()==='month'){const last=new Date(a.getFullYear(),a.getMonth()+step+1,0).getDate();next=new Date(a.getFullYear(),a.getMonth()+step,Math.min(a.getDate(),last));}
    else next=new Date(a.getFullYear(),a.getMonth(),a.getDate()+step*(this.view()==='week'?7:1));
    this.escape();this.anchor.set(next);
  }
  today(){this.escape();try{const parts=new Intl.DateTimeFormat('en-CA',{timeZone:this.timeZoneId,year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date());const get=(type:string)=>Number(parts.find(p=>p.type===type)?.value);this.anchor.set(new Date(get('year'),get('month')-1,get('day')));}catch{this.error.set('Geçerli bir saat dilimi gir; örneğin Europe/Istanbul.');}}
  title(){const a=this.anchor();return this.view()==='month'?a.toLocaleDateString('tr-TR',{month:'long',year:'numeric'}):this.view()==='day'?a.toLocaleDateString('tr-TR',{day:'numeric',month:'long',year:'numeric',weekday:'long'}):`${this.days()[0].toLocaleDateString('tr-TR',{day:'numeric',month:'short'})} – ${this.days()[6].toLocaleDateString('tr-TR',{day:'numeric',month:'short',year:'numeric'})}`;}
  dayTitle(d:Date){return d.toLocaleDateString('tr-TR',{weekday:'long',day:'numeric',month:'short'});}
  rows(d:Date){return this.displayed().filter(s=>s.day===weekday(d)).sort((a,b)=>a.startMinute-b.startMinute);}
  gridColumns(){return `48px repeat(${this.days().length},minmax(${this.view()==='week'?120:220}px,1fr))`;}
  openMenu(e:MouseEvent,d:Date,minute:number){
    e.preventDefault();if(this.busy()||!this.ready())return;
    this.escape();this.menu={x:Math.max(8,Math.min(e.clientX,window.innerWidth-258)),y:Math.max(8,Math.min(e.clientY,window.innerHeight-170)),date:d,minute};
  }
  add(d=this.anchor(),minute=1080){
    if(this.busy()||!this.ready())return;this.menu=null;this.formError='';
    this.form={id:crypto.randomUUID(),lessonId:this.lessons()[0]?.id??'',day:weekday(d),start:clock(minute),duration:minute===1410?30:60};
  }
  edit(slot:WeeklySlot){if(this.suppressClick){this.suppressClick=false;return;}if(this.busy())return;this.menu=null;this.formError='';this.form={id:slot.id,lessonId:slot.lessonId,day:slot.day,start:clock(slot.startMinute),duration:slot.durationMinutes};}
  apply(){
    if(!this.form||this.busy())return;const f=this.form;const lesson=this.lessons().find(l=>l.id===f.lessonId);
    if(!lesson){this.formError='Çalışma düzenine uygun aktif bir ders seç.';return;}
    if(!/^\d{2}:\d{2}$/.test(f.start)){this.formError='Geçerli bir başlangıç saati seç.';return;}
    const [hour,minute]=f.start.split(':').map(Number);
    if(hour>23||minute>59){this.formError='Geçerli bir başlangıç saati seç.';return;}
    const candidate:WeeklySlot={id:f.id,lessonId:f.lessonId,lessonName:lesson.name,examCode:lesson.examCode,day:f.day,startMinute:hour*60+minute,durationMinutes:Number(f.duration)};
    this.formError=slotError(candidate,this.slots());if(this.formError)return;
    this.slots.update(rows=>[...rows.filter(s=>s.id!==candidate.id),candidate]);this.form=null;this.message.set('');this.error.set('');
  }
  remove(){if(!this.form||this.busy())return;const id=this.form.id;this.slots.update(rows=>rows.filter(s=>s.id!==id));this.form=null;this.message.set('');}
  hasSlot(){return !!this.form&&this.slots().some(s=>s.id===this.form!.id);}
  available(slot:WeeklySlot){return this.lessons().some(l=>l.id===slot.lessonId);}
  color(slot:WeeklySlot){return [...slot.lessonId].reduce((sum,c)=>sum+c.charCodeAt(0),0)%5;}
  begin(e:PointerEvent,slot:WeeklySlot,mode:Gesture['mode']){
    if(this.busy()||e.button!==0||!e.isPrimary)return;e.stopPropagation();this.menu=null;this.suppressClick=false;
    const card=(e.target as HTMLElement).closest<HTMLElement>('.slot-card')!;
    this.gesture={id:e.pointerId,slot:{...slot},mode,startX:e.clientX,startY:e.clientY,offset:e.clientY-card.getBoundingClientRect().top,moved:false};
  }
  @HostListener('document:pointermove',['$event']) move(e:PointerEvent){
    const g=this.gesture;if(!g||g.id!==e.pointerId)return;
    if(!g.moved&&Math.hypot(e.clientX-g.startX,e.clientY-g.startY)<5)return;g.moved=true;e.preventDefault();let candidate={...g.slot};
    if(g.mode==='move'){
      const track=document.elementFromPoint(e.clientX,e.clientY)?.closest<HTMLElement>('.day-track');
      if(!track){this.preview.set(null);return;}
      candidate.day=track.dataset['day'] as Weekday;
      candidate.startMinute=Math.round((e.clientY-track.getBoundingClientRect().top-g.offset)/36)*30;
    }else{
      const delta=Math.round((e.clientY-g.startY)/36)*30;
      candidate.durationMinutes=Math.max(30,Math.min(60,g.slot.durationMinutes+(g.mode==='top'?-delta:delta)));
      if(g.mode==='top')candidate.startMinute=g.slot.startMinute+g.slot.durationMinutes-candidate.durationMinutes;
    }
    this.preview.set(candidate);
  }
  @HostListener('document:pointerup',['$event']) end(e:PointerEvent){
    const g=this.gesture;if(!g||e.pointerId!==g.id)return;
    const candidate=this.preview();this.gesture=null;this.preview.set(null);if(!g.moved)return;this.suppressClick=true;
    if(!candidate){this.error.set('Kartı takvimde uygun bir saate bırak.');return;}
    const error=slotError(candidate,this.slots());if(error){this.error.set(error);return;}
    this.slots.update(rows=>rows.map(s=>s.id===candidate.id?candidate:s));this.error.set('');this.message.set('');
  }
  @HostListener('document:pointercancel') cancelGesture(){this.gesture=null;this.preview.set(null);}
  @HostListener('document:keydown.escape') escape(){this.cancelGesture();this.menu=null;this.form=null;}
  previewError(){return this.preview()?slotError(this.preview()!,this.slots()):'';}
  monthDay(d:Date){this.escape();this.anchor.set(d);this.view.set('day');}
  canLeavePage(){return !this.saving()&&(!this.dirty()&&!this.form||window.confirm('Kaydedilmemiş haftalık düzeni bırakarak ayrılmak istiyor musun?'));}
  @HostListener('window:beforeunload',['$event']) beforeUnload(e:BeforeUnloadEvent){if(this.dirty()||this.saving()){e.preventDefault();e.returnValue='';}}
}
