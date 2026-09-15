BEGIN;

DELETE FROM speed_reading.cms_content_blocks
WHERE NOT "IsDeleted"
  AND "Group" = 'AboutPage'
  AND (
    (id = 'c78ab930-4f8b-47dd-b5ed-c48e67f91ce1'::uuid AND "Key" = 'about_hero_subtitle' AND "Value" = 'Hızlı okuma ve anlama çalışmalarınızı ölçüm, düzenli pratik ve kişisel planla sürdürün.')
    OR
    (id = '1c4f9731-2c6a-4549-96a5-4c03c93738bf'::uuid AND "Key" = 'about_story_content' AND "Value" = '<p>Master Hızlı Okuma, başlangıç ölçümü, düzenli çalışmalar ve kişiselleştirilmiş planlarla okuma hızı, anlama ve odaklanma gelişiminizi izlemenize yardımcı olur.</p>')
  );

DELETE FROM speed_reading.admin_audit_records
WHERE "Id" = 'b6a54047-e174-4a06-904a-3dc197c7532a'::uuid;

COMMIT;
