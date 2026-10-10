START TRANSACTION;
DROP INDEX speed_reading.ux_daily_exercise_logs_progress_slot;

ALTER TABLE speed_reading.daily_exercise_logs ADD slot_order integer;

CREATE UNIQUE INDEX ux_daily_exercise_logs_legacy_slot ON speed_reading.daily_exercise_logs ("StudentProgramProgressId", "WeekNumber", "DayNumber", "ExerciseId") WHERE session_id IS NOT NULL AND slot_order IS NULL;

CREATE UNIQUE INDEX ux_daily_exercise_logs_progress_slot ON speed_reading.daily_exercise_logs ("StudentProgramProgressId", "WeekNumber", "DayNumber", slot_order) WHERE slot_order IS NOT NULL;

INSERT INTO speed_reading.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261010064029_AddDailyTaskSlotOrder', '9.0.2');

COMMIT;

