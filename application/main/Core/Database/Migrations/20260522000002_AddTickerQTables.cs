using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260522000002_AddTickerQTables")]
public sealed class AddTickerQTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE SCHEMA IF NOT EXISTS ticker;

            CREATE TABLE IF NOT EXISTS ticker."CronTickers" (
                id uuid NOT NULL,
                function text NULL,
                description text NULL,
                init_identifier text NULL,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL,
                expression text NULL,
                request bytea NULL,
                retries integer NOT NULL,
                retry_intervals integer[] NULL,
                is_enabled boolean NOT NULL,
                is_system_paused boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT pk_cron_tickers PRIMARY KEY (id)
            );

            CREATE TABLE IF NOT EXISTS ticker."TimeTickers" (
                id uuid NOT NULL,
                function text NULL,
                description text NULL,
                init_identifier text NULL,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL,
                status text NOT NULL,
                lock_holder text NULL,
                request bytea NULL,
                execution_time timestamp with time zone NULL,
                locked_at timestamp with time zone NULL,
                executed_at timestamp with time zone NULL,
                exception_message text NULL,
                skipped_reason text NULL,
                elapsed_time bigint NOT NULL,
                retries integer NOT NULL,
                retry_count integer NOT NULL,
                retry_intervals integer[] NULL,
                parent_id uuid NULL,
                run_condition integer NULL,
                CONSTRAINT pk_time_tickers PRIMARY KEY (id),
                CONSTRAINT fk_time_tickers_time_tickers_parent_id FOREIGN KEY (parent_id) REFERENCES ticker."TimeTickers" (id)
            );

            CREATE TABLE IF NOT EXISTS ticker."CronTickerOccurrences" (
                id uuid NOT NULL,
                status text NOT NULL,
                lock_holder text NULL,
                execution_time timestamp with time zone NOT NULL,
                cron_ticker_id uuid NOT NULL,
                locked_at timestamp with time zone NULL,
                executed_at timestamp with time zone NULL,
                exception_message text NULL,
                skipped_reason text NULL,
                elapsed_time bigint NOT NULL,
                retry_count integer NOT NULL,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_cron_ticker_occurrences PRIMARY KEY (id),
                CONSTRAINT fk_cron_ticker_occurrences_cron_tickers_cron_ticker_id FOREIGN KEY (cron_ticker_id) REFERENCES ticker."CronTickers" (id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_time_tickers_parent_id
                ON ticker."TimeTickers" (parent_id);
            CREATE INDEX IF NOT EXISTS ix_time_tickers_status_execution_time
                ON ticker."TimeTickers" (status, execution_time);
            CREATE INDEX IF NOT EXISTS ix_cron_ticker_occurrences_cron_ticker_id
                ON ticker."CronTickerOccurrences" (cron_ticker_id);
            CREATE INDEX IF NOT EXISTS ix_cron_ticker_occurrences_status_execution_time
                ON ticker."CronTickerOccurrences" (status, execution_time);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_cron_ticker_occurrences_execution_time_cron_ticker_id
                ON ticker."CronTickerOccurrences" (execution_time, cron_ticker_id);
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CronTickerOccurrences", schema: "ticker");
        migrationBuilder.DropTable(name: "TimeTickers", schema: "ticker");
        migrationBuilder.DropTable(name: "CronTickers", schema: "ticker");
    }
}
