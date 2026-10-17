import {CommonModule} from '@angular/common';
import {Component,DestroyRef,Input,OnInit,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {HttpClient,HttpErrorResponse} from '@angular/common/http';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {finalize} from 'rxjs';
import {environment} from '../../../environments/environment';
interface StudentProfile {version:number;educationStatus:string|number;gradeLevel:number|null;graduationYear?:number|null;schoolName:string|null;schoolCity:string|null;schoolDistrict:string|null;schoolType:string|null;classBranch:string|null;birthDate:string|null;bio:string|null}
@Component({selector:'staff-coaching-student-profile',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./coaching-teacher-student-profile.component.html',styleUrl:'./coaching-teacher-student-profile.component.scss'})
export class CoachingTeacherStudentProfileComponent implements OnInit {
 @Input({required:true}) studentId!:string;
 private readonly http=inject(HttpClient);private readonly destroyRef=inject(DestroyRef);
 readonly busy=signal(false);readonly error=signal('');readonly success=signal('');
 form:StudentProfile|null=null;editing=false;dirty=false;
 private get url(){return `${environment.apiUrl}/coaching/teachers/me/students/${encodeURIComponent(this.studentId)}/profile`;}
 ngOnInit(){this.load();}
 canLeavePage(){return !this.dirty||window.confirm('Profildeki kaydedilmemiş değişikliklerden vazgeçilsin mi?');}
 load(){if(this.busy()||!this.canLeavePage())return;this.busy.set(true);this.error.set('');this.success.set('');
  this.http.get<StudentProfile>(this.url).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.busy.set(false))).subscribe({next:p=>this.apply(p),error:e=>this.fail(e)});}
 save(){if(!this.form||!this.editing||this.busy())return;const p=this.form;this.busy.set(true);this.error.set('');this.success.set('');
  const body={expectedVersion:p.version,schoolName:p.schoolName,schoolCity:p.schoolCity,schoolDistrict:p.schoolDistrict,schoolType:p.schoolType,classBranch:p.classBranch,birthDate:p.birthDate||null,bio:p.bio};
  this.http.put<StudentProfile>(this.url,body).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.busy.set(false))).subscribe({next:r=>{this.apply(r);this.success.set('Öğrencinin Koçluk profili güncellendi.');},error:e=>this.fail(e)});}
 private apply(p:StudentProfile){this.form={...p};this.dirty=false;this.editing=false;}
 private fail(e:HttpErrorResponse){this.error.set(e.error?.message??(e.status===403?'Bu öğrencinin profiline erişim yetkiniz yok.':'Profil yüklenemedi veya kaydedilemedi. Yeniden deneyin.'));}
}
