\set ON_ERROR_STOP on
WITH patterns AS (
    SELECT p.id, p."Name" AS name, p."TargetAgeGroupConfigurationId" AS age,
        day_number, x.value->>'Type' AS type,
        (x.value->>'Count')::int AS needed,
        LEAST(p."MaxDifficultyLevel", GREATEST(0,
            COALESCE((x.value->>'Difficulty')::int, p."InitialDifficultyLevel"))) AS difficulty
    FROM speed_reading.program_templates p
    CROSS JOIN LATERAL generate_series(1,p."TotalDays") day_number
    CROSS JOIN LATERAL jsonb_array_elements(
        COALESCE(p."WeeklyPatternJson" -> ('week' || ((day_number-1)/7+1))
            -> ('day' || ((day_number-1)%7+1)), '[]'::jsonb)) x
    WHERE p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment"
), slots AS (
    SELECT p.*, t.id AS type_id,
        (SELECT count(*) FROM speed_reading.exercises e
          WHERE e.exercise_type_id=t.id AND e.is_active AND NOT e.is_deleted
            AND (e.target_age_group_id IS NULL OR e.target_age_group_id=p.age)
            AND e.difficulty_level<=p.difficulty) AS available
    FROM patterns p LEFT JOIN speed_reading.exercise_types t
      ON t.name=p.type AND t.is_active AND NOT t.is_deleted
)
SELECT name, count(DISTINCT day_number) AS checked_days,
    count(*) FILTER (WHERE available<needed OR type_id IS NULL) AS insufficient_slots,
    sum(needed) AS scheduled_exercises
FROM slots GROUP BY id,name ORDER BY name;
WITH missing_days AS (
    SELECT p."Name", day_number
    FROM speed_reading.program_templates p
    CROSS JOIN LATERAL generate_series(1,p."TotalDays") day_number
    WHERE p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment"
      AND jsonb_array_length(COALESCE(p."WeeklyPatternJson"
        -> ('week' || ((day_number-1)/7+1)) -> ('day' || ((day_number-1)%7+1)), '[]'))=0
)
SELECT * FROM missing_days;
