using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuardCoachingSessionOverlap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Serialize scheduling writes, including attendance changes, until their transaction commits.
            // Existing legacy overlaps remain readable; only new or changed reservations are validated.
            migrationBuilder.Sql("""
                CREATE FUNCTION coaching.lock_session_scheduling() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtextextended('coaching-session-scheduling', 0));
                    RETURN NULL;
                END;
                $$;
                CREATE TRIGGER lock_session_scheduling BEFORE INSERT OR UPDATE OR DELETE
                    ON coaching.coaching_sessions FOR EACH STATEMENT EXECUTE FUNCTION coaching.lock_session_scheduling();
                CREATE TRIGGER lock_session_scheduling BEFORE INSERT OR UPDATE OR DELETE
                    ON coaching.session_attendances FOR EACH STATEMENT EXECUTE FUNCTION coaching.lock_session_scheduling();
                CREATE FUNCTION coaching.guard_session_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE participants uuid[];
                BEGIN
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    IF NEW.status <> 'Scheduled' THEN RETURN NEW; END IF;
                    IF TG_OP = 'UPDATE' AND NEW.scheduled_date = OLD.scheduled_date
                        AND NEW.duration_minutes = OLD.duration_minutes AND NEW.teacher_id = OLD.teacher_id
                        AND NEW.status = OLD.status THEN RETURN NEW; END IF;
                    SELECT array_agg(person_id) INTO participants FROM (
                        SELECT NEW.teacher_id AS person_id UNION
                        SELECT a.student_id FROM coaching.session_attendances a WHERE a.session_id = NEW.id
                    ) people;
                    IF EXISTS (
                        SELECT 1 FROM coaching.coaching_sessions s
                        WHERE s.id <> NEW.id AND s.status = 'Scheduled'
                            AND s.scheduled_date < NEW.scheduled_date + make_interval(mins => NEW.duration_minutes)
                            AND s.scheduled_date + make_interval(mins => s.duration_minutes) > NEW.scheduled_date
                            AND (s.teacher_id = ANY(participants) OR EXISTS (
                                SELECT 1 FROM coaching.session_attendances a
                                WHERE a.session_id = s.id AND a.student_id = ANY(participants)))
                    ) THEN
                        RAISE EXCEPTION 'A coaching participant has an overlapping session'
                            USING ERRCODE = '23P01', CONSTRAINT = 'coaching_session_overlap';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER guard_session_overlap BEFORE INSERT OR UPDATE OR DELETE
                    ON coaching.coaching_sessions FOR EACH ROW EXECUTE FUNCTION coaching.guard_session_overlap();

                CREATE FUNCTION coaching.guard_attendance_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE target coaching.coaching_sessions%ROWTYPE;
                BEGIN
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    IF TG_OP = 'UPDATE' AND NEW.session_id = OLD.session_id AND NEW.student_id = OLD.student_id THEN RETURN NEW; END IF;
                    SELECT * INTO target FROM coaching.coaching_sessions WHERE id = NEW.session_id;
                    IF target.status <> 'Scheduled' THEN RETURN NEW; END IF;
                    IF EXISTS (
                        SELECT 1 FROM coaching.coaching_sessions s
                        WHERE s.id <> NEW.session_id AND s.status = 'Scheduled'
                            AND s.scheduled_date < target.scheduled_date + make_interval(mins => target.duration_minutes)
                            AND s.scheduled_date + make_interval(mins => s.duration_minutes) > target.scheduled_date
                            AND (s.teacher_id = NEW.student_id OR EXISTS (
                                SELECT 1 FROM coaching.session_attendances a
                                WHERE a.session_id = s.id AND a.id <> NEW.id AND a.student_id = NEW.student_id))
                    ) THEN
                        RAISE EXCEPTION 'A coaching participant has an overlapping session'
                            USING ERRCODE = '23P01', CONSTRAINT = 'coaching_session_overlap';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER guard_attendance_overlap BEFORE INSERT OR UPDATE OR DELETE
                    ON coaching.session_attendances FOR EACH ROW EXECUTE FUNCTION coaching.guard_attendance_overlap();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER lock_session_scheduling ON coaching.session_attendances;
                DROP TRIGGER lock_session_scheduling ON coaching.coaching_sessions;
                DROP TRIGGER guard_attendance_overlap ON coaching.session_attendances;
                DROP TRIGGER guard_session_overlap ON coaching.coaching_sessions;
                DROP FUNCTION coaching.guard_attendance_overlap();
                DROP FUNCTION coaching.guard_session_overlap();
                DROP FUNCTION coaching.lock_session_scheduling();
                """);
        }
    }
}
