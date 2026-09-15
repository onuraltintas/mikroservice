BEGIN;

-- The current site shows the application fallback because these CMS blocks do not
-- yet exist. Accept either no rows or the exact rows created by this pack.
DO $$
DECLARE
    existing_rows integer;
    matching_rows integer;
BEGIN
    SELECT COUNT(*) INTO existing_rows
    FROM speed_reading.cms_content_blocks
    WHERE NOT "IsDeleted"
      AND "Group" = 'AboutPage'
      AND "Key" IN ('about_hero_subtitle', 'about_story_content');

    IF existing_rows = 0 THEN
        RETURN;
    END IF;

    SELECT COUNT(*) INTO matching_rows
    FROM speed_reading.cms_content_blocks
    WHERE NOT "IsDeleted"
      AND "Group" = 'AboutPage'
      AND (
        ("Key" = 'about_hero_subtitle' AND "Value" = 'Hızlı okuma ve anlama çalışmalarınızı ölçüm, düzenli pratik ve kişisel planla sürdürün.')
        OR
        ("Key" = 'about_story_content' AND "Value" = '<p>Master Hızlı Okuma, başlangıç ölçümü, düzenli çalışmalar ve kişiselleştirilmiş planlarla okuma hızı, anlama ve odaklanma gelişiminizi izlemenize yardımcı olur.</p>')
      );

    IF existing_rows <> 2 OR matching_rows <> 2 THEN
        RAISE EXCEPTION 'cms-marketing-language-repair-v1 found an editor-managed AboutPage block that differs from this pack';
    END IF;
END $$;

INSERT INTO speed_reading.cms_content_blocks (id, "Key", "Group", "Label", "Type", "Value", "CreatedAt", "CreatedBy", "IsDeleted")
VALUES
    ('c78ab930-4f8b-47dd-b5ed-c48e67f91ce1'::uuid, 'about_hero_subtitle', 'AboutPage', 'Hakkımızda — giriş metni', 1, 'Hızlı okuma ve anlama çalışmalarınızı ölçüm, düzenli pratik ve kişisel planla sürdürün.', NOW(), '00000000-0000-0000-0000-000000000000'::uuid, FALSE),
    ('1c4f9731-2c6a-4549-96a5-4c03c93738bf'::uuid, 'about_story_content', 'AboutPage', 'Hakkımızda — hikâye metni', 1, '<p>Master Hızlı Okuma, başlangıç ölçümü, düzenli çalışmalar ve kişiselleştirilmiş planlarla okuma hızı, anlama ve odaklanma gelişiminizi izlemenize yardımcı olur.</p>', NOW(), '00000000-0000-0000-0000-000000000000'::uuid, FALSE)
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    created_rows integer;
BEGIN
    SELECT COUNT(*) INTO created_rows
    FROM speed_reading.cms_content_blocks
    WHERE NOT "IsDeleted" AND "Group" = 'AboutPage' AND (
        (id = 'c78ab930-4f8b-47dd-b5ed-c48e67f91ce1'::uuid AND "Key" = 'about_hero_subtitle' AND "Value" = 'Hızlı okuma ve anlama çalışmalarınızı ölçüm, düzenli pratik ve kişisel planla sürdürün.')
        OR
        (id = '1c4f9731-2c6a-4549-96a5-4c03c93738bf'::uuid AND "Key" = 'about_story_content' AND "Value" = '<p>Master Hızlı Okuma, başlangıç ölçümü, düzenli çalışmalar ve kişiselleştirilmiş planlarla okuma hızı, anlama ve odaklanma gelişiminizi izlemenize yardımcı olur.</p>')
    );

    IF created_rows <> 2 THEN
        RAISE EXCEPTION 'cms-marketing-language-repair-v1 did not create both CMS blocks';
    END IF;
END $$;

INSERT INTO speed_reading.admin_audit_records ("Id", "OccurredAt", "ServiceName", "ActorUserId", "ActorRoles", "HttpMethod", "Path", "StatusCode", "CorrelationId", "Action", "ResourceType", "ResourceId", "ChangedFieldsJson")
VALUES ('b6a54047-e174-4a06-904a-3dc197c7532a'::uuid, NOW(), 'speed-reading-service', 'system-cms-maintenance', 'System', 'POST', '/maintenance/cms-marketing-language-repair-v1', 200, 'cms-marketing-language-repair-v1', 'Create', 'cms-content-block', 'AboutPage', '{"createdKeys":["about_hero_subtitle","about_story_content"]}')
ON CONFLICT ("Id") DO NOTHING;

COMMIT;
