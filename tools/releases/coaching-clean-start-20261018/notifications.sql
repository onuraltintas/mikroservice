\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='10s';
DELETE FROM public."Notifications" WHERE "Type" IN('StudyPlanPublished','StudyPlanUpdated','StudyPlanReminder')
 AND "RelatedEntityId"=ANY(string_to_array(:'plan_ids',','));
COMMIT;
