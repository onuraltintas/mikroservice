using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stores every address in its canonical form (trimmed, lower-case), matching
    /// <c>User.NormalizeEmail</c>. With that, the existing unique index on "Email" is
    /// case-insensitive in practice and sign-in look-ups use it instead of scanning.
    /// The migration stops, changing nothing, when two accounts differ only by case:
    /// those must be merged or renamed deliberately first.
    /// </summary>
    public partial class NormalizeUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE conflicts integer;
                BEGIN
                    SELECT count(*) INTO conflicts
                    FROM (
                        SELECT lower(btrim("Email"))
                        FROM identity.users
                        GROUP BY 1
                        HAVING count(*) > 1
                    ) duplicated;

                    IF conflicts > 0 THEN
                        RAISE EXCEPTION 'NormalizeUserEmails: % address(es) belong to several accounts that differ only by letter case. Merge or rename them first; list them with: SELECT lower(btrim("Email")) AS email, array_agg("Id") AS accounts FROM identity.users GROUP BY 1 HAVING count(*) > 1;', conflicts;
                    END IF;

                    UPDATE identity.users
                    SET "Email" = lower(btrim("Email"))
                    WHERE "Email" <> lower(btrim("Email"));
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original letter case is not kept; canonical addresses remain valid for the
            // previous code, which compared addresses case-insensitively.
        }
    }
}
