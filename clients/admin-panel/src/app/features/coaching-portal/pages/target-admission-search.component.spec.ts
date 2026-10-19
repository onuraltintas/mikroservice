import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TargetAdmissionSearchComponent } from './target-admission-search.component';

describe('2026 verified target filters',()=>{
  function setup(){
    TestBed.configureTestingModule({imports:[TargetAdmissionSearchComponent],providers:[provideHttpClient(),provideHttpClientTesting()]});
    const fixture=TestBed.createComponent(TargetAdmissionSearchComponent);fixture.componentRef.setInput('kind','program');fixture.detectChanges();
    return {fixture,component:fixture.componentInstance,http:TestBed.inject(HttpTestingController)};
  }
  it('loads source-backed facets and sends year and filters without guessing rank',()=>{
    const {component,http}=setup();http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({success:true,data:{city:['ANKARA'],institutionType:['DEVLET']}});
    component.filters['city']='ANKARA';component.filters['institutionType']='DEVLET';component.search();
    const request=http.expectOne(r=>r.url.endsWith('/admissions'));expect(request.request.params.get('year')).toBe('2026');expect(request.request.params.get('city')).toBe('ANKARA');
    request.flush({success:true,data:{items:[],totalCount:0,pageNumber:1,pageSize:20}});expect(component.searched()).toBe(true);http.verify();
  });
  it('emits only the canonical target of a returned verified row',()=>{
    const {component,http}=setup();http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({success:true,data:{}});
    const chosen=vi.fn();component.selected.subscribe(chosen);component.choose('unseen');expect(chosen).not.toHaveBeenCalled();
    component.search();http.expectOne(r=>r.url.endsWith('/admissions')).flush({success:true,data:{items:[{id:'snapshot',targetId:'canonical',name:'Program',institution:'University',year:2026}],totalCount:1,pageNumber:1,pageSize:20}});
    component.choose('snapshot');expect(chosen).toHaveBeenCalledWith('canonical');http.verify();
  });
  it('cancels old scope requests when switching from program to school',()=>{
    const {component,fixture,http}=setup();http.expectOne(r=>r.url.endsWith('/admissions/filters')).flush({success:true,data:{}});
    component.search();const pending=http.expectOne(r=>r.url.endsWith('/admissions'));
    fixture.componentRef.setInput('kind','school');fixture.detectChanges();expect(pending.cancelled).toBe(true);expect(component.items()).toEqual([]);
    http.expectOne(r=>r.url.endsWith('/admissions/filters') && r.params.get('examCode')==='LGS').flush({success:true,data:{}});http.verify();
  });
});
