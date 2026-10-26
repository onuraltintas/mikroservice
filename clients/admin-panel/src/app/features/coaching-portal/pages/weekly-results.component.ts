import {CommonModule} from '@angular/common';
import {Component,Input,Output,EventEmitter} from '@angular/core';
import {WeeklyResultSnapshot} from '../../../core/services/coaching-weekly-review.service';

@Component({selector:'app-weekly-results',standalone:true,imports:[CommonModule],template:`
  <p>{{snapshot.summary.completedTasks}} / {{snapshot.summary.scheduledTasks}} çalışma tamamlandı · {{snapshot.summary.actualMinutes ?? 'Belirtilmedi'}} dk gerçekleşti</p>
  <p>Plan sayıları planlanan haftayı, gerçekleşme miktarları fiili çalışma günlerini gösterir.</p>
  @if(snapshot.summary.legacyDateActualMinutes!=null){<p>{{snapshot.summary.legacyDateActualMinutes}} dk eski kayıt, sabitlenmiş eski plan tarihiyle gösteriliyor; fiili tarihi bilinmiyor.</p>}
  <small>Sonuçların alındığı zaman: {{snapshot.capturedAt | date:'dd.MM.yyyy HH:mm':'+0300'}}</small>
  @if(!snapshot.decisions.length){<p>Bu değerlendirmeye taşınan önceki karar yok.</p>}
  @for(row of snapshot.decisions;track row.decisionId){<article>
    <h4>{{row.title}}</h4><p>Karar haftası: {{row.sourceWeekStart | date:'dd.MM.yyyy'}} · {{status(row.status)}} · Son tarih: {{row.dueOn | date:'dd.MM.yyyy'}}</p>
    @if(row.work?.study;as work){<p>Bağlı çalışma: {{work.title}} · {{status(work.status)}} · {{work.actualMinutes ?? 'Belirtilmedi'}} dk · {{work.actualQuestions ?? 'Belirtilmedi'}} soru</p>}
    @if(row.work?.assignment;as work){<p>Bağlı ödev: {{work.title}} · {{status(work.status)}} · Puan: {{work.score ?? 'Belirtilmedi'}}</p>@if(work.feedback){<p>Öğretmen geri bildirimi: {{work.feedback}}</p>}}
    @for(work of row.relatedStudy;track work.taskId){<p>Bu haftaki ilgili çalışma: {{work.title}} · {{work.isCompleted ? 'Tamamlandı' : work.actualMinutes != null ? 'Kısmi çalışma' : 'Bekliyor'}} · {{work.actualMinutes ?? 'Belirtilmedi'}} dk · {{work.actualQuestions ?? 'Belirtilmedi'}} soru</p>}
    @if(canFollowUp){<button type="button" (click)="followUp.emit(row.decisionId)">Bu kararın devamını oluştur</button>}
    @if(row.effect){<p><strong>Öğrencinin karar sonucu:</strong> {{status(row.effect)}} · {{row.effectNote}}</p>}
  </article>}
  <details><summary>Haftanın çalışma sonuçları ({{snapshot.studies.length}} kayıt)</summary>
    @for(work of snapshot.studies;track work.taskId){<p>Plan tarihi: {{work.plannedDate | date:'dd.MM.yyyy'}} · {{work.title}} · {{work.isCompleted ? 'Tamamlandı' : work.actualMinutes != null ? 'Kısmi çalışma' : 'Bekliyor'}} · {{work.actualMinutes ?? 'Belirtilmedi'}} / {{work.plannedMinutes}} dk · {{work.actualQuestions ?? 'Belirtilmedi'}} soru</p>@for(day of work.workDays ?? [];track day.workedOn){<p>Çalışma günü: {{day.workedOn | date:'dd.MM.yyyy'}} · {{day.minutes}} dk @if(day.isLegacyDate){ · Eski plan tarihine dayalı kayıt }</p>}}
  </details>
`,styles:[`:host{display:block}article{padding:12px 0;border-bottom:1px solid var(--ui-border)}small{color:var(--ui-text-muted)}p{white-space:pre-wrap;overflow-wrap:anywhere}`]})
export class WeeklyResultsComponent {
  @Input() canFollowUp=false;
  @Output() followUp=new EventEmitter<string>();
  @Input({required:true}) snapshot!:WeeklyResultSnapshot;
  status(value:string){return ({Planned:'Planlandı',InProgress:'Devam ediyor',Completed:'Tamamlandı',Cancelled:'İptal',Pending:'Bekliyor',Archived:'Plan arşivlendi',Assigned:'Atandı',Submitted:'Teslim edildi',Graded:'Değerlendirildi',Unavailable:'Erişilemiyor',Helped:'İşe yaradı',PartlyHelped:'Kısmen işe yaradı',DidNotHelp:'İşe yaramadı',NotTried:'Uygulanmadı'} as Record<string,string>)[value]??value;}
}
