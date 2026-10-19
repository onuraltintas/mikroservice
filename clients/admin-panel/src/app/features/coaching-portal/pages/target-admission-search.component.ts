import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, finalize, takeUntil } from 'rxjs';
import { environment } from '../../../../environments/environment';

interface AdmissionItem {
  id:string; targetId:string; name:string; institution:string; externalCode:string;
  minimumScore:number|null; rank:number|null; percentile:number|null; year:number; stage:string; sourceUrl:string;
  city:string|null; language:string|null; scholarship:string|null; quota:number|null;
}
@Component({selector:'app-target-admission-search',standalone:true,imports:[CommonModule,FormsModule],
  template:`
  <section aria-label="Doğrulanmış hedef kataloğu" class="mt-4 rounded-lg border p-3">
    <h4 class="font-semibold">2026 ve sonrası · Resmî hedef kataloğu</h4>
    <p class="my-2 text-xs">Yalnız kaynağı ve eşleşmesi doğrulanmış kayıtlar listelenir. Geçmiş yerleştirme sonucu, gelecek yıl için kabul garantisi değildir.</p>
    <form (ngSubmit)="search()">
      <fieldset [disabled]="busy() || disabled" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <label>Veri yılı<input aria-label="Katalog veri yılı" type="number" name="year" [(ngModel)]="year" (change)="yearChanged()" min="2026" max="2100" required class="w-full rounded border p-2"></label>
        <label>Okul / program ara<input name="search" [(ngModel)]="filters['search']" maxlength="300" class="w-full rounded border p-2"></label>
        <label *ngFor="let field of fields">{{field.label}}
          <select [name]="field.key" [(ngModel)]="filters[field.key]" class="w-full rounded border p-2">
            <option value="">Tümü</option><option *ngFor="let value of facets()[field.key] || []" [value]="value">{{display(value)}}</option>
          </select>
        </label>
        <label>En düşük taban puan<input type="number" name="minimumScore" [(ngModel)]="filters['minimumScore']" min="0" step="0.00001" class="w-full rounded border p-2"></label>
        <label>En yüksek taban puan<input type="number" name="maximumScore" [(ngModel)]="filters['maximumScore']" min="0" step="0.00001" class="w-full rounded border p-2"></label>
        <label *ngIf="kind==='program'">En yüksek başarı sırası<input type="number" name="maximumRank" [(ngModel)]="filters['maximumRank']" min="1" step="1" class="w-full rounded border p-2"></label>
        <label *ngIf="kind==='school'">En yüksek yüzdelik dilim<input type="number" name="maximumPercentile" [(ngModel)]="filters['maximumPercentile']" min="0" max="100" step="0.0001" class="w-full rounded border p-2"></label>
        <label>Sıralama<select name="sort" [(ngModel)]="filters['sort']" class="w-full rounded border p-2"><option value="name">Ad</option><option value="score-desc">Taban puan: yüksekten düşüğe</option><option value="rank">Başarı sırası</option><option value="percentile">Yüzdelik dilim</option></select></label>
        <button type="submit" class="rounded bg-indigo-600 p-2 text-white">Resmî katalogda ara</button>
      </fieldset>
    </form>
    <p *ngIf="error()" role="alert" class="mt-2 text-red-600">{{error()}}</p>
    <p *ngIf="busy()" role="status">Katalog yükleniyor…</p>
    <p *ngIf="searched() && !busy()" role="status" class="my-2">{{total()}} doğrulanmış kayıt</p>
    <p *ngIf="searched() && !total()" class="my-2 text-sm">Seçtiğiniz yıl ve filtrelerde doğrulanmış veri bulunamadı. Yılı belirsiz eski puanlar bu sonuçlara karıştırılmaz.</p>
    <ul class="space-y-2"><li *ngFor="let item of items()" class="rounded border p-3">
      <strong>{{item.institution}} — {{item.name}}</strong>
      <p class="text-sm">{{item.externalCode}} · {{item.year}} · {{item.stage==='Main' ? 'Ana yerleştirme' : item.stage}}</p>
      <p class="text-sm">Taban puan: {{item.minimumScore ?? 'Oluşmadı / yayımlanmadı'}} · Başarı sırası: {{item.rank ?? 'Yayımlanmadı'}} · Yüzdelik: {{item.percentile ?? 'Yayımlanmadı'}}</p>
      <p class="text-sm">{{item.city}} {{item.language}} {{item.scholarship}} · Kontenjan: {{item.quota ?? 'Bilinmiyor'}}</p>
      <a [href]="item.sourceUrl" target="_blank" rel="noopener noreferrer" class="underline">Resmî kaynak</a>
      <button type="button" (click)="choose(item.id)" [disabled]="disabled || busy()" class="ml-3 rounded border px-3 py-2">Bu hedefi bağla</button>
    </li></ul>
    <div *ngIf="total()>20" class="mt-3 flex gap-3"><button type="button" (click)="search(page-1)" [disabled]="page<=1 || busy()">Önceki</button><span>Sayfa {{page}}</span><button type="button" (click)="search(page+1)" [disabled]="page*20>=total() || busy()">Sonraki</button></div>
  </section>`})
export class TargetAdmissionSearchComponent implements OnChanges {
  @Input() kind:'school'|'program'='program';
  @Input() disabled=false;
  @Output() readonly selected=new EventEmitter<string>();
  private readonly http=inject(HttpClient);
  private readonly destroyRef=inject(DestroyRef);
  private readonly scopeChanged=new Subject<void>();
  private readonly root=`${environment.apiUrl}/coaching/study-planning/targets/admissions`;
  readonly facets=signal<Record<string,string[]>>({});readonly items=signal<AdmissionItem[]>([]);
  readonly busy=signal(false);readonly searched=signal(false);readonly error=signal('');readonly total=signal(0);
  year=2026;page=1;filters:Record<string,string|number>={sort:'name'};
  readonly fields=[{key:'city',label:'İl'},{key:'district',label:'İlçe'},{key:'institution',label:'Okul / üniversite'},
    {key:'institutionType',label:'Kurum türü'},{key:'scoreType',label:'Puan türü'},{key:'degree',label:'Öğrenim düzeyi'},
    {key:'language',label:'Eğitim dili'},{key:'scholarship',label:'Burs / ücret'},{key:'educationType',label:'Öğretim biçimi'},{key:'field',label:'Fakülte / alan'}];
  display(value:string){return ({Associate:'Ön lisans',Bachelor:'Lisans',SOZ:'Sözel',DIL:'Dil'} as Record<string,string>)[value]??value;}
  ngOnChanges(changes:import('@angular/core').SimpleChanges){
    if(!changes['kind'])return;
    this.scopeChanged.next();this.items.set([]);this.facets.set({});this.total.set(0);this.searched.set(false);this.error.set('');this.filters={sort:'name'};this.loadFacets();
  }
  private loadFacets(){
    this.http.get<{data:Record<string,string[]>}>(`${this.root}/filters`,{params:{examCode:this.kind==='school'?'LGS':'YKS',year:this.year}})
      .pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef)).subscribe({next:r=>this.facets.set(r.data),error:()=>this.error.set('Katalog filtreleri yüklenemedi.')});
  }
  yearChanged(){this.scopeChanged.next();this.filters={sort:'name'};this.items.set([]);this.facets.set({});this.searched.set(false);this.total.set(0);if(this.year>=2026&&this.year<=2100)this.loadFacets();}
  search(page=1){
    if(this.busy()||this.disabled)return;
    if(!Number.isInteger(this.year)||this.year<2026||this.year>2100){this.error.set('2026 veya sonrası bir veri yılı seçin.');return;}
    this.busy.set(true);this.items.set([]);this.error.set('');this.searched.set(false);
    let params=new HttpParams().set('examCode',this.kind==='school'?'LGS':'YKS').set('year',this.year).set('pageNumber',page);
    for(const [key,value] of Object.entries(this.filters))if(value!=='' && value!==null && value!==undefined)params=params.set(key,value);
    this.http.get<{data:{items:AdmissionItem[];totalCount:number;pageNumber:number}}>(this.root,{params})
      .pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef),finalize(()=>this.busy.set(false)))
      .subscribe({next:r=>{this.items.set(r.data.items);this.total.set(r.data.totalCount);this.page=r.data.pageNumber;this.searched.set(true);},error:()=>this.error.set('Arama başarısız. Filtreleri kontrol edin.')});
  }
  choose(id:string){const item=this.items().find(x=>x.id===id);if(item&&!this.disabled&&!this.busy())this.selected.emit(item.targetId);}
}
