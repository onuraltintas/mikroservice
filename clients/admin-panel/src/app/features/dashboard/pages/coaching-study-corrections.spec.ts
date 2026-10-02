import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingStudyCorrectionsComponent } from './coaching-study-corrections';

describe('CoachingStudyCorrectionsComponent',()=>{
  function setup(manage=true){
    TestBed.configureTestingModule({imports:[CoachingStudyCorrectionsComponent],providers:[provideHttpClient(),provideHttpClientTesting(),
      {provide:AuthService,useValue:{userProfile:()=>({roles:['SystemAdmin']}),hasPermission:()=>manage}},
      {provide:ToasterService,useValue:{confirm:async()=>true,success:()=>{}}}]});
    const fixture=TestBed.createComponent(CoachingStudyCorrectionsComponent);
    fixture.componentRef.setInput('studentId','student');
    fixture.componentRef.setInput('plan',{id:'revision',version:3,title:'Plan',status:'Active',tasks:[]});
    fixture.detectChanges();return {fixture,component:fixture.componentInstance,http:TestBed.inject(HttpTestingController)};
  }
  afterEach(()=>TestBed.inject(HttpTestingController).verify());
  it('sends version and mandatory reason with only editable planning fields',async()=>{
    const {component,http}=setup();component.title='Reviewed';component.reason='Verified correction';
    await component.savePlan(false);
    const req=http.expectOne(r=>r.url.endsWith('/corrections/plans/revision'));
    expect(req.request.body).toEqual({expectedVersion:3,title:'Reviewed',archive:false,reason:'Verified correction'});
    req.flush({success:true});
  });
  it('blocks missing reason and missing manage permission',async()=>{
    const {component,http}=setup(false);component.reason='Valid reason';await component.savePlan(false);
    http.expectNone(r=>r.url.includes('/corrections/'));
  });
});
