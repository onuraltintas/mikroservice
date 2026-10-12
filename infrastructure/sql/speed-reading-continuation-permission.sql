-- Student-scoped approval is not global program catalog management.
BEGIN;
DO $$ BEGIN
 IF (SELECT count(*) FROM identity."Roles" WHERE "Name" IN ('Teacher','InstitutionOwner','InstitutionAdmin') AND NOT "IsDeleted") <> 3 THEN
  RAISE EXCEPTION 'Expected three active staff roles';
 END IF;
 IF EXISTS (SELECT 1 FROM identity."Permissions" WHERE "Key"='Permissions.SpeedReading.StudentProgramApprove' AND "IsDeleted") THEN
  RAISE EXCEPTION 'Approval permission is explicitly disabled';
 END IF;
END $$;
INSERT INTO identity."Permissions"("Id","Key","Description","Group","IsSystem","IsDeleted","CreatedAt")
VALUES(gen_random_uuid(),'Permissions.SpeedReading.StudentProgramApprove','Approve a pending program choice for an assigned student','SpeedReading',true,false,now())
ON CONFLICT("Key") DO NOTHING;
INSERT INTO identity."RolePermissions"("Id","RoleId","Permission","CreatedAt")
SELECT gen_random_uuid(),r."Id",'Permissions.SpeedReading.StudentProgramApprove',now()
FROM identity."Roles" r WHERE r."Name" IN ('Teacher','InstitutionOwner','InstitutionAdmin') AND NOT r."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM identity."RolePermissions" p WHERE p."RoleId"=r."Id" AND p."Permission"='Permissions.SpeedReading.StudentProgramApprove');
COMMIT;
