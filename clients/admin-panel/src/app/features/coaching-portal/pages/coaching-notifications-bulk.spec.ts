import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CoachingNotificationsComponent } from './coaching-notifications.component';

describe('notification bulk actions',()=>{
 function setup(){
  const service={notifications:signal<any[]>([{id:'a',isRead:false},{id:'b',isRead:false}]),unreadCount:signal(2),markAsRead:vi.fn(()=>of(undefined)),deleteNotification:vi.fn((id:string)=>id==='b'?throwError(()=>new Error('offline')):of(undefined))};
  const toaster={confirm:vi.fn(async()=>true),error:vi.fn(),success:vi.fn()};
  TestBed.configureTestingModule({imports:[CoachingNotificationsComponent],providers:[provideRouter([]),{provide:NotificationService,useValue:service},{provide:ToasterService,useValue:toaster}]});
  const fixture=TestBed.createComponent(CoachingNotificationsComponent);return {component:fixture.componentInstance,service,toaster};
 }
 it('deletes only the selected records and retains failed selections',async()=>{
  const {component,service}=setup();component.toggleSelection('a');component.toggleSelection('b');await component.bulkAction('delete');
  expect(service.deleteNotification).toHaveBeenCalledTimes(2);expect(component.selectedIds().has('a')).toBe(false);expect(component.selectedIds().has('b')).toBe(true);expect(component.isUpdating()).toBe(false);
 });
 it('does not delete when the central confirmation is cancelled',async()=>{
  const {component,service,toaster}=setup();toaster.confirm.mockResolvedValue(false);component.toggleSelection('a');await component.bulkAction('delete');expect(service.deleteNotification).not.toHaveBeenCalled();expect(component.selectedIds().has('a')).toBe(true);
 });
 it('marks only selected unread records as read',async()=>{
  const {component,service}=setup();component.toggleSelection('a');await component.bulkAction('read');expect(service.markAsRead).toHaveBeenCalledWith('a');expect(service.markAsRead).toHaveBeenCalledTimes(1);
 });
});
