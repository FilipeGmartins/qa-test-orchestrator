CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914124818_InitialProjects') THEN
    CREATE TABLE "Projects" (
        "Id" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "ArchivedAt" timestamp with time zone,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_Projects" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914124818_InitialProjects') THEN
    CREATE INDEX "IX_Projects_ArchivedAt_CreatedAt_Id" ON "Projects" ("ArchivedAt", "CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914124818_InitialProjects') THEN
    CREATE INDEX "IX_Projects_CreatedAt_Id" ON "Projects" ("CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914124818_InitialProjects') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260914124818_InitialProjects', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914182548_AddTestSuites') THEN
    CREATE TABLE "TestSuites" (
        "Id" uuid NOT NULL,
        "ProjectId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "Tags" text NOT NULL,
        "Status" character varying(16) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_TestSuites" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TestSuites_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914182548_AddTestSuites') THEN
    CREATE INDEX "IX_TestSuites_ProjectId_CreatedAt_Id" ON "TestSuites" ("ProjectId", "CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914182548_AddTestSuites') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260914182548_AddTestSuites', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915203641_AddCasesAndEnvironments') THEN
    CREATE TABLE "ProjectEnvironments" (
        "Id" uuid NOT NULL,
        "ProjectId" uuid NOT NULL,
        "Name" character varying(16) NOT NULL,
        "BaseUrl" character varying(2048) NOT NULL,
        "Enabled" boolean NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ProjectEnvironments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProjectEnvironments_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915203641_AddCasesAndEnvironments') THEN
    CREATE TABLE "TestCases" (
        "Id" uuid NOT NULL,
        "TestSuiteId" uuid NOT NULL,
        "StableKey" character varying(80) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "Tags" text NOT NULL,
        "Status" character varying(16) NOT NULL,
        "CatalogVersion" integer NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TestCases" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TestCases_TestSuites_TestSuiteId" FOREIGN KEY ("TestSuiteId") REFERENCES "TestSuites" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915203641_AddCasesAndEnvironments') THEN
    CREATE UNIQUE INDEX "IX_ProjectEnvironments_ProjectId_Name" ON "ProjectEnvironments" ("ProjectId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915203641_AddCasesAndEnvironments') THEN
    CREATE INDEX "IX_TestCases_TestSuiteId_CreatedAt_Id" ON "TestCases" ("TestSuiteId", "CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915203641_AddCasesAndEnvironments') THEN
    CREATE UNIQUE INDEX "IX_TestCases_TestSuiteId_StableKey" ON "TestCases" ("TestSuiteId", "StableKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915203641_AddCasesAndEnvironments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915203641_AddCasesAndEnvironments', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915215249_AddTestRuns') THEN
    CREATE TABLE "TestRuns" (
        "Id" uuid NOT NULL,
        "ProjectId" uuid NOT NULL,
        "TestSuiteId" uuid NOT NULL,
        "EnvironmentId" uuid NOT NULL,
        "ConfigurationSnapshot" text NOT NULL,
        "Status" character varying(16) NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "StartedAt" timestamp with time zone,
        "FinishedAt" timestamp with time zone,
        CONSTRAINT "PK_TestRuns" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TestRuns_ProjectEnvironments_EnvironmentId" FOREIGN KEY ("EnvironmentId") REFERENCES "ProjectEnvironments" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TestRuns_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TestRuns_TestSuites_TestSuiteId" FOREIGN KEY ("TestSuiteId") REFERENCES "TestSuites" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915215249_AddTestRuns') THEN
    CREATE INDEX "IX_TestRuns_EnvironmentId" ON "TestRuns" ("EnvironmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915215249_AddTestRuns') THEN
    CREATE INDEX "IX_TestRuns_ProjectId_CreatedAt_Id" ON "TestRuns" ("ProjectId", "CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915215249_AddTestRuns') THEN
    CREATE INDEX "IX_TestRuns_Status_CreatedAt_Id" ON "TestRuns" ("Status", "CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915215249_AddTestRuns') THEN
    CREATE INDEX "IX_TestRuns_TestSuiteId" ON "TestRuns" ("TestSuiteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915215249_AddTestRuns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915215249_AddTestRuns', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    ALTER TABLE "TestRuns" ADD "CancellationRequested" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    ALTER TABLE "TestRuns" ADD "LeaseExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    ALTER TABLE "TestRuns" ADD "LeaseId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    ALTER TABLE "TestRuns" ADD "ProgressJson" text NOT NULL DEFAULT '[]';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    ALTER TABLE "TestRuns" ADD "ResultJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    ALTER TABLE "TestRuns" ADD "RunnerError" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    CREATE INDEX "IX_TestRuns_Status_LeaseExpiresAt" ON "TestRuns" ("Status", "LeaseExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110400_AddRunnerLeases') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916110400_AddRunnerLeases', '10.0.12');
    END IF;
END $EF$;
COMMIT;
