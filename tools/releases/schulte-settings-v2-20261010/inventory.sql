-- API remains stopped between snapshots. Exclude only the twenty patched exercise rows.
BEGIN READ ONLY;
SELECT format('SELECT %L || ''|'' || count(*) || ''|'' || md5(COALESCE(string_agg(row_to_json(t)::text, ''|'' ORDER BY row_to_json(t)::text), '''')) FROM %I.%I t %s;',
 tablename,schemaname,tablename,
 CASE WHEN tablename='exercises' THEN 'WHERE id NOT IN(''add57a25-c884-5851-b27f-8fe0fe413f6f'',''3ea61ae4-3637-56d4-a6a6-a3d8057f406b'',''c1fc2991-2b6e-5ae4-ad6d-aa62e0ef0abd'',''e43c9878-8c70-51c0-b9fd-7526747fa5d9'',''670c82a7-66cc-5f2a-8feb-ae3002f02c2a'',''aaabc650-2bfa-5c38-a8f8-5c4728aa33ef'',''1da1b433-db34-5d05-af76-8a7a63a8b4db'',''84334437-52d8-5352-802d-76045fad0dc5'',''40feec30-398e-51a9-bd86-68679a80399b'',''b04edcad-d063-5928-a646-40301db65385'',''b10650e6-3bd1-5228-86ed-6eeb60de5a97'',''61248859-b87b-5849-b175-7956c01a9b1a'',''fc18cf9d-81a7-5645-b533-f5e0ae20d5bc'',''515cf27a-b306-5a37-86e8-4768512e076f'',''34d9e9dc-4515-56c2-b348-16c79d7a7445'',''622ef34e-d579-57dd-aac4-af82e7c192ca'',''71e3cf3f-5e10-5995-a3e1-363a5a87b899'',''061da412-99ff-515f-be25-eb5518a0ae0a'',''9b8454f0-ee0f-51a6-aea4-24a1edef4b82'',''b0a7e63c-edc5-573e-8416-df18584a9b0d'')' ELSE '' END)
FROM pg_tables WHERE schemaname='speed_reading' ORDER BY tablename
\gexec
COMMIT;
