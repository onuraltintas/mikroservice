BEGIN READ ONLY;
SELECT coalesce(jsonb_agg(jsonb_build_object('id',t.id,'name',t.name,'description',t.description,'version',t.version,
  'updatedAt',t.updated_at,'updatedBy',t.updated_by) ORDER BY t.name),'[]'::jsonb)
FROM speed_reading.exercise_types t WHERE t.is_active AND NOT t.is_deleted;
COMMIT;
