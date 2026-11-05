using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <summary>
    /// User decision (01.11.2026, "sınırı yükselt"): the work of one topic or one outcome duration version may total up to
    /// 4320 minutes (72 h) instead of 1440, so broad topics (e.g. ÖABT İlköğretim Matematik "Analiz") keep their real share
    /// instead of being split into sub-topics. Only the two per-topic totals change:
    /// - outcome_duration_estimates.ck_outcome_minutes (Learning + Practice + Check)
    /// - study_catalog_topic_outcome_shares.ck_topic_outcome_share_minutes
    /// Everything that happens on one day keeps 1440 (study tasks, remaining scope, task-outcome allocations, timed slots).
    /// Down restores 1440 and refuses, changing nothing, while a duration version, a topic share or a topic's minutes need
    /// more than 1440: the previous release cannot store or plan them.
    /// </summary>
    public partial class TopicMinutesCap4320 : Migration
    {
        private const string Outcome = "\"LearningMinutes\" >= 0 AND \"PracticeMinutes\" >= 0 AND \"CheckMinutes\" >= 0 AND (\"LearningMinutes\"::bigint + \"PracticeMinutes\" + \"CheckMinutes\") BETWEEN 1 AND {0} AND \"Number\" > 0";
        private const string Share = "\"LearningMinutes\" >= 0 AND \"PracticeMinutes\" >= 0 AND \"CheckMinutes\" >= 0 AND (\"LearningMinutes\"::bigint + \"PracticeMinutes\" + \"CheckMinutes\") BETWEEN 1 AND {0}";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Replace(migrationBuilder, 4320);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE estimates bigint; shares bigint; topics bigint;
                BEGIN
                  SELECT count(*) INTO estimates FROM coaching.outcome_duration_estimates
                    WHERE "LearningMinutes"::bigint + "PracticeMinutes" + "CheckMinutes" > 1440;
                  SELECT count(*) INTO shares FROM coaching.study_catalog_topic_outcome_shares
                    WHERE "LearningMinutes"::bigint + "PracticeMinutes" + "CheckMinutes" > 1440;
                  SELECT count(*) INTO topics FROM coaching.study_catalog_topics WHERE "EstimatedMinutes" > 1440;
                  IF estimates + shares + topics > 0 THEN
                    RAISE EXCEPTION 'TopicMinutesCap4320 down refused: % duration version(s), % topic share(s) and % topic(s) need more than 1440 minutes. Nothing changed.',
                      estimates, shares, topics;
                  END IF;
                END $$;
                """);
            Replace(migrationBuilder, 1440);
        }

        private static void Replace(MigrationBuilder migrationBuilder, int limit)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_outcome_minutes", schema: "coaching", table: "outcome_duration_estimates");
            migrationBuilder.AddCheckConstraint(name: "ck_outcome_minutes", schema: "coaching", table: "outcome_duration_estimates",
                sql: string.Format(System.Globalization.CultureInfo.InvariantCulture, Outcome, limit));
            migrationBuilder.DropCheckConstraint(name: "ck_topic_outcome_share_minutes", schema: "coaching", table: "study_catalog_topic_outcome_shares");
            migrationBuilder.AddCheckConstraint(name: "ck_topic_outcome_share_minutes", schema: "coaching", table: "study_catalog_topic_outcome_shares",
                sql: string.Format(System.Globalization.CultureInfo.InvariantCulture, Share, limit));
        }
    }
}
