BEGIN READ ONLY;
SELECT jsonb_build_object('activeNormalPrograms',count(*)) FROM speed_reading.program_templates WHERE "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment";
SELECT jsonb_build_object('templateId',p.id,'level',p."InitialDifficultyLevel",'ageId',p."TargetAgeGroupConfigurationId",
  'vocabularySlots',count(*) FILTER(WHERE slot->>'type'='Vocabulary'),
  'focusFirstDays',count(*) FILTER(WHERE day.value->0->>'type'='Focus' AND position=1))
FROM speed_reading.program_templates p
CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") week
CROSS JOIN LATERAL jsonb_each(CASE WHEN week.key LIKE 'week%' THEN week.value ELSE '{}'::jsonb END) day
CROSS JOIN LATERAL jsonb_array_elements(day.value) WITH ORDINALITY entries(slot,position)
WHERE p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment"
GROUP BY p.id ORDER BY p."TargetAgeGroupConfigurationId",p."InitialDifficultyLevel";
SELECT jsonb_build_object('templateId',p."ProgramTemplateId",'week',p."CurrentWeek",'day',p."CurrentDay",'completedDays',p."DaysCompleted",'completedExercises',p."ExercisesCompleted",
  'logs',(SELECT count(*) FROM speed_reading.daily_exercise_logs l WHERE l."StudentProgramProgressId"=p.id),
  'normalSessionsSinceAssignment',(SELECT count(*) FROM speed_reading.exercise_sessions s WHERE s.student_id=p."UserId" AND s.assessment_attempt_id IS NULL AND s.start_time>=p."AssignedDate"))
FROM speed_reading.student_program_progress p WHERE p."IsActive" AND p."CompletedDate" IS NULL;
COMMIT;
