import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TargetAdmissionSearchComponent } from './target-admission-search.component';

describe('2026 verified target filters',()=>{
  async function setup(){
    TestBed.configureTestingModule({imports:[TargetAdmissionSearchComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});
    const fixture=TestBed.createComponent(TargetAdmissionSearchComponent);fixture.componentRef.setInput('kind','program');fixture.detectChanges(); await fixture.whenStable();
    return {fixture,component:fixture.componentInstance,http:TestBed.inject(HttpTestingController)};
  }
  it('shows the five MEBI filters and searches by their values',async ()=>{
    const {component,fixture,http}=await setup();
    http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({success:true,data:{programType:['Associate','Bachelor'],scoreType:['EA','SAY','SOZ','TYT','YDT']}});
    fixture.detectChanges(); await fixture.whenStable();
    for(const label of ['Program türü','Üniversite','Fakülte','Program','Puan türü']) expect(fixture.nativeElement.textContent).toContain(label);
    component.filters['programType']='Bachelor';component.filters['university']='Ankara';
    component.filters['faculty']='Engineering';component.filters['programName']='Computer';component.filters['scoreType']='YDT';component.search();
    const request=http.expectOne(r=>r.url.endsWith('/admissions'));
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('programType')).toBe('Bachelor');expect(request.request.params.get('university')).toBe('Ankara');
    expect(request.request.params.get('faculty')).toBe('Engineering');expect(request.request.params.get('programName')).toBe('Computer');
    expect(request.request.params.get('scoreType')).toBe('YDT');
    request.flush({success:true,data:{items:[],totalCount:0,pageNumber:1,pageSize:20}});
    expect(component.searched()).toBe(true);http.verify();
  });
  it('emits only the canonical target of a returned verified row',async ()=>{
    const {component,http}=await setup();http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({success:true,data:{scoreType:['YDT']}});
    const chosen=vi.fn();component.selected.subscribe(chosen);await component.choose('unseen');expect(chosen).not.toHaveBeenCalled();
    component.search();http.expectOne(r=>r.url.endsWith('/admissions')).flush({success:true,data:{items:[{id:'snapshot',targetId:'canonical',name:'Program',institution:'University',year:2026}],totalCount:1,pageNumber:1,pageSize:20}});
    await component.choose('snapshot');expect(chosen).toHaveBeenCalledWith('canonical');http.verify();
  });
  it('cancels old scope requests when switching from program to school',async ()=>{
    const {component,fixture,http}=await setup();http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({success:true,data:{scoreType:['YDT']}});
    component.search();const pending=http.expectOne(r=>r.url.endsWith('/admissions'));
    fixture.componentRef.setInput('kind','school');fixture.detectChanges(); await fixture.whenStable();expect(pending.cancelled).toBe(true);expect(component.items()).toEqual([]);
    http.expectOne(r=>r.url.endsWith('/admissions/filters') && r.params.get('examCode')==='LGS').flush({success:true,data:{city:['ANKARA']}});http.verify();
  });
  it('loads LGS language filters, clears the old district and never labels unknown scores as 2026',async()=>{
    const {component,fixture,http}=await setup();
    http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({data:{}});
    fixture.componentRef.setInput('kind','school');fixture.detectChanges(); await fixture.whenStable();
    http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({data:{city:['ANKARA','SAMSUN'],district:['BAFRA'],language:['İngilizce','Almanca'],field:[]}});
    component.filters['district']='BAFRA';component.filters['city']='ANKARA';component.filterChanged('city');
    const facet=http.expectOne(r=>r.url.endsWith('/admissions/filters'));
    expect(facet.request.params.has('district')).toBe(false);
    facet.flush({data:{city:['ANKARA','SAMSUN'],district:['ÇANKAYA'],language:['İngilizce','Almanca'],field:[]}});
    component.filters['language']='Almanca';component.search();
    const request=http.expectOne(r=>r.url.endsWith('/admissions'));
    expect(request.request.params.get('language')).toBe('Almanca');
    request.flush({data:{items:[{id:'new',targetId:'preserved',institution:'Fen Lisesi',city:'ANKARA',district:'ÇANKAYA',language:'Almanca',minimumScore:450,year:null,stage:null,field:null,quota:null}],totalCount:1,pageNumber:1}});
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('MEBİ kataloğu');
    expect(fixture.nativeElement.textContent).not.toContain('2026 resmî kataloğu');
    await fixture.whenStable();fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('select[name="field"]').disabled).toBe(true);
    const selected=vi.fn();component.selected.subscribe(selected);await component.choose('new');expect(selected).toHaveBeenCalledWith('preserved');
    http.verify();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
