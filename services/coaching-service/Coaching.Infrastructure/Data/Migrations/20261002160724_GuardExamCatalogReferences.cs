using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuardExamCatalogReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION coaching.guard_exam_catalog_references() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE item jsonb; lesson_id uuid; topic_id uuid;
                BEGIN
                    IF NEW.lesson_answers IS NULL THEN RETURN NEW; END IF;
                    IF jsonb_typeof(NEW.lesson_answers) <> 'array' THEN
                        RAISE EXCEPTION 'Exam lesson answers must be an array' USING ERRCODE = '23514';
                    END IF;
                    FOR item IN SELECT jsonb_array_elements(NEW.lesson_answers) LOOP
                        lesson_id := COALESCE(item->>'LessonId', item->>'lessonId')::uuid;
                        topic_id := COALESCE(item->>'TopicId', item->>'topicId')::uuid;
                        PERFORM 1 FROM coaching.study_catalog_lessons WHERE "Id" = lesson_id FOR KEY SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'Exam references a missing catalog lesson' USING ERRCODE = '23503';
                        END IF;
                        IF topic_id IS NOT NULL THEN
                            PERFORM 1 FROM coaching.study_catalog_topics
                                WHERE "Id" = topic_id AND "LessonId" = lesson_id FOR KEY SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Exam references a missing or mismatched catalog topic' USING ERRCODE = '23503';
                            END IF;
                        END IF;
                    END LOOP;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER guard_exam_catalog_references
                    BEFORE INSERT OR UPDATE OF lesson_answers ON coaching.exam_results
                    FOR EACH ROW EXECUTE FUNCTION coaching.guard_exam_catalog_references();

                CREATE FUNCTION coaching.guard_catalog_exam_history() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM coaching.exam_results AS result
                        CROSS JOIN LATERAL jsonb_array_elements(result.lesson_answers) AS item
                        WHERE COALESCE(item->>'LessonId', item->>'lessonId')::uuid = OLD."Id"
                            OR COALESCE(item->>'TopicId', item->>'topicId')::uuid = OLD."Id") THEN
                        RAISE EXCEPTION 'Catalog entry is referenced by exam history' USING ERRCODE = '23503';
                    END IF;
                    RETURN OLD;
                END $$;
                CREATE TRIGGER guard_catalog_exam_history BEFORE DELETE ON coaching.study_catalog_lessons
                    FOR EACH ROW EXECUTE FUNCTION coaching.guard_catalog_exam_history();
                CREATE TRIGGER guard_catalog_exam_history BEFORE DELETE ON coaching.study_catalog_topics
                    FOR EACH ROW EXECUTE FUNCTION coaching.guard_catalog_exam_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_catalog_exam_history ON coaching.study_catalog_topics;
                DROP TRIGGER guard_catalog_exam_history ON coaching.study_catalog_lessons;
                DROP TRIGGER guard_exam_catalog_references ON coaching.exam_results;
                DROP FUNCTION coaching.guard_catalog_exam_history();
                DROP FUNCTION coaching.guard_exam_catalog_references();
                """);
        }
    }
}
