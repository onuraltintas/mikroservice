BEGIN READ ONLY;
SELECT jsonb_build_object(
 'templates',(SELECT jsonb_agg(jsonb_build_object('id',p.id,'ageId',p."TargetAgeGroupConfigurationId",'level',p."InitialDifficultyLevel",'pattern',p."WeeklyPatternJson",'description',p."Description",'version',p.version,'updatedAt',p.updated_at,'updatedBy',p.updated_by))
   FROM speed_reading.program_templates p WHERE p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment"),
 'exercises',(SELECT jsonb_agg(jsonb_build_object('id',e.id,'type',t.name,'ageId',e.target_age_group_id,'level',e.difficulty_level,'mode',e.configuration_json #>> '{engineConfig,comprehensionMode}'))
   FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
   WHERE e.is_active AND NOT e.is_deleted AND NOT e.is_assessment_only AND t.is_active AND NOT t.is_deleted),
 'progress',coalesce((SELECT jsonb_agg(jsonb_build_object('id',p.id,'templateId',p."ProgramTemplateId",'schedule',p."ScheduleJson"::jsonb,
   'currentWeek',p."CurrentWeek",'currentDay',p."CurrentDay",'daysCompleted',p."DaysCompleted",'exercisesCompleted',p."ExercisesCompleted",
   'version',p.version,'updatedAt',p.updated_at,'updatedBy',p.updated_by,
   'logsFingerprint',(SELECT md5(coalesce(string_agg(md5(to_jsonb(l)::text),'' ORDER BY l.id),'')) FROM speed_reading.daily_exercise_logs l WHERE l."StudentProgramProgressId"=p.id),
   'sessionsFingerprint',(SELECT md5(coalesce(string_agg(md5(to_jsonb(s)::text),'' ORDER BY s.id),'')) FROM speed_reading.exercise_sessions s WHERE s.student_id=p."UserId" AND s.assessment_attempt_id IS NULL AND s.start_time>=p."AssignedDate"),
   'protectedDays',coalesce((SELECT jsonb_agg(day) FROM generate_series(1,28) day
     WHERE day<(p."CurrentWeek"-1)*7+p."CurrentDay"
       OR EXISTS(SELECT 1 FROM speed_reading.daily_exercise_logs l WHERE l."StudentProgramProgressId"=p.id AND (l."WeekNumber"-1)*7+l."DayNumber"=day)
       OR (day=(p."CurrentWeek"-1)*7+p."CurrentDay" AND EXISTS(SELECT 1 FROM speed_reading.exercise_sessions s WHERE s.student_id=p."UserId" AND s.assessment_attempt_id IS NULL AND s.start_time>=p."AssignedDate"))),'[]'::jsonb)))
   FROM speed_reading.student_program_progress p JOIN speed_reading.program_templates t ON t.id=p."ProgramTemplateId"
   WHERE p."IsActive" AND p."CompletedDate" IS NULL AND t."IsActive" AND NOT t."IsDeleted" AND NOT t."IsAssessment"),'[]'::jsonb));
COMMIT;
