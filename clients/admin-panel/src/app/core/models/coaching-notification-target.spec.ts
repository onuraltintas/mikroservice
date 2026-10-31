import { coachingNotificationLabel, coachingNotificationTarget } from '../../../../shared/coaching-notification-target';

describe('Coaching notification references',()=>{
  const id='a4cea155-5485-4db6-9f52-1ec4df8080c3';
  it('routes weekly notifications with structured and role-specific references',()=>{
    const reflection={type:'WeeklyReflectionSaved',relatedEntityId:`weekly:${id}:2026-10-12`};
    expect(coachingNotificationTarget(reflection,'teacher')).toMatchObject({kind:'weekly',studentId:id,weekStart:'2026-10-12',decisionId:null});
    expect(coachingNotificationTarget(reflection,'student')).toBeNull();
    for(const type of ['WeeklyDecisionCreated','WeeklyDecisionUpdated']) {
      const reference={type,relatedEntityId:`weekly:${id}:2026-10-12:${id}`};
      expect(coachingNotificationTarget(reference,'student')).toMatchObject({kind:'weekly',weekStart:'2026-10-12',decisionId:id});
      expect(coachingNotificationTarget(reference,'teacher')).toBeNull();
    }
  });
  for(const reference of [`weekly:${id}:2026-10-13`, `weekly:${id}:2026-02-30`, `weekly:${id}:invalid`, `weekly:bad:2026-10-12`, `weekly:${id}:2026-10-12:extra`, `weekly:${id}:2026-10-12:${id}:extra`]) {
    it(`rejects malformed weekly destination ${reference}`,()=>{
      for(const type of ['WeeklyReflectionSaved','WeeklyDecisionCreated','WeeklyDecisionUpdated'])
        expect(coachingNotificationTarget({type,relatedEntityId:reference},type==='WeeklyReflectionSaved'?'teacher':'student')).toBeNull();
    });
  }
  for (const type of ['StudyPlanPublished','StudyPlanUpdated','AssignmentCreated','AssignmentUpdated','AssignmentGraded']) {
    it(`links ${type} to its stored student entity`,()=>{
      expect(coachingNotificationTarget({type,relatedEntityId:id},'student')).toEqual({
        kind:type.startsWith('StudyPlan')?'plan':'assignment',id,label:type.startsWith('StudyPlan')?'Planı aç':'Ödevi aç'
      });
      expect(coachingNotificationLabel(type)).not.toBe(type);
    });
  }
  it('routes submissions only into the teacher review and does not turn unrelated products into coaching links',()=>{
    expect(coachingNotificationTarget({type:'AssignmentSubmitted',relatedEntityId:id},'teacher')?.id).toBe(id);
    expect(coachingNotificationTarget({type:'AssignmentSubmitted',relatedEntityId:id},'student')).toBeNull();
    expect(coachingNotificationTarget({type:'CourseManagement.AssignmentSubmitted',relatedEntityId:id},'teacher')).toBeNull();
    expect(coachingNotificationTarget({type:1,relatedEntityId:id},'student')).toBeNull();
  });
  for(const relatedEntityId of [null,undefined,'','legacy-id','https://evil.test','../assignments','00000000-0000-0000-0000-000000000000']) {
    it(`does not invent a link for missing or unsafe entity reference ${relatedEntityId}`,()=>{
      expect(coachingNotificationTarget({type:'StudyPlanUpdated',relatedEntityId},'student')).toBeNull();
    });
  }
});

it('opens the actual plan for daily and overdue study summaries',()=>{
  const id='a4cea155-5485-4db6-9f52-1ec4df8080c3';
  for(const type of ['StudyPlanReminder','StudyPlanOverdueReminder'])expect(coachingNotificationTarget({type,relatedEntityId:id},'student')).toMatchObject({kind:'plan',id});
});

// V1/V4 (01.11.2026): parent-link notices take a student to Profil ve ayarlar › Profilim, where the parent link lives.
it('opens the parent link settings from a parent-link notice for a student',()=>{
  const id='a4cea155-5485-4db6-9f52-1ec4df8080c3';
  for(const type of ['ParentLinkRequested','ParentLinkApproved','ParentLinkRevoked']) {
    expect(coachingNotificationTarget({type,relatedEntityId:id},'student')).toEqual({kind:'profile',id,label:'Veli bağlantısını aç'});
    expect(coachingNotificationTarget({type,relatedEntityId:id},'teacher')).toBeNull();
    expect(coachingNotificationLabel(type)).not.toBe(type);
  }
});
