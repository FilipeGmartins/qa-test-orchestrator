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

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    ALTER TABLE "TestRuns" ADD "ArtifactsPurgedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE TABLE "TestAttempts" (
        "Id" uuid NOT NULL,
        "RunId" uuid NOT NULL,
        "CaseId" uuid NOT NULL,
        "CaseName" character varying(120) NOT NULL,
        "StableKey" character varying(80) NOT NULL,
        "Browser" character varying(16) NOT NULL,
        "Attempt" integer NOT NULL,
        "Status" character varying(16) NOT NULL,
        "DurationMs" bigint NOT NULL,
        "Error" character varying(4000) NOT NULL,
        "Stack" character varying(4000) NOT NULL,
        "Logs" character varying(4000) NOT NULL,
        "RecordedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TestAttempts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TestAttempts_TestCases_CaseId" FOREIGN KEY ("CaseId") REFERENCES "TestCases" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TestAttempts_TestRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "TestRuns" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE TABLE "TestArtifacts" (
        "Id" uuid NOT NULL,
        "AttemptId" uuid NOT NULL,
        "RunId" uuid NOT NULL,
        "RelativePath" character varying(100) NOT NULL,
        "Kind" character varying(16) NOT NULL,
        "Size" bigint NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_TestArtifacts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TestArtifacts_TestAttempts_AttemptId" FOREIGN KEY ("AttemptId") REFERENCES "TestAttempts" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TestArtifacts_TestRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "TestRuns" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE INDEX "IX_TestRuns_ArtifactsPurgedAt_FinishedAt" ON "TestRuns" ("ArtifactsPurgedAt", "FinishedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE INDEX "IX_TestArtifacts_AttemptId" ON "TestArtifacts" ("AttemptId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE INDEX "IX_TestArtifacts_RunId" ON "TestArtifacts" ("RunId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE INDEX "IX_TestAttempts_CaseId_RecordedAt_Id" ON "TestAttempts" ("CaseId", "RecordedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    CREATE UNIQUE INDEX "IX_TestAttempts_RunId_CaseId_Browser_Attempt" ON "TestAttempts" ("RunId", "CaseId", "Browser", "Attempt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916165709_AddResultsAndArtifacts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916165709_AddResultsAndArtifacts', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    ALTER TABLE "TestRuns" ADD "PresetId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    ALTER TABLE "TestRuns" ADD "PresetRevision" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    CREATE TABLE "RunPresets" (
        "Id" uuid NOT NULL,
        "ProjectId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "Revision" integer NOT NULL,
        "Version" uuid NOT NULL,
        "Archived" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_RunPresets" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RunPresets_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    CREATE TABLE "PresetRevisions" (
        "PresetId" uuid NOT NULL,
        "Revision" integer NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "RequestJson" text NOT NULL,
        "SnapshotJson" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PresetRevisions" PRIMARY KEY ("PresetId", "Revision"),
        CONSTRAINT "FK_PresetRevisions_RunPresets_PresetId" FOREIGN KEY ("PresetId") REFERENCES "RunPresets" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    CREATE INDEX "IX_TestRuns_PresetId_PresetRevision" ON "TestRuns" ("PresetId", "PresetRevision");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    CREATE INDEX "IX_RunPresets_ProjectId_Archived_UpdatedAt_Id" ON "RunPresets" ("ProjectId", "Archived", "UpdatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    ALTER TABLE "TestRuns" ADD CONSTRAINT "FK_TestRuns_PresetRevisions_PresetId_PresetRevision" FOREIGN KEY ("PresetId", "PresetRevision") REFERENCES "PresetRevisions" ("PresetId", "Revision") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916230834_AddRunPresets') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916230834_AddRunPresets', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    ALTER TABLE "TestRuns" ADD "CancelledById" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    ALTER TABLE "TestRuns" ADD "CancelledByName" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    ALTER TABLE "TestRuns" ADD "CreatedById" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    ALTER TABLE "TestRuns" ADD "CreatedByName" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    ALTER TABLE "TestRuns" ADD "EnqueuedById" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    ALTER TABLE "TestRuns" ADD "EnqueuedByName" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    CREATE TABLE "AccountRegistries" (
        "Id" integer NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_AccountRegistries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    CREATE TABLE "Accounts" (
        "Id" uuid NOT NULL,
        "Login" character varying(80) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "PasswordHash" character varying(1000) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "Active" boolean NOT NULL,
        "Version" uuid NOT NULL,
        "FailedAttempts" integer NOT NULL,
        "LockedUntil" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    CREATE TABLE "LoginSessions" (
        "Id" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "AccountVersion" uuid NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LoginSessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LoginSessions_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    INSERT INTO "AccountRegistries" ("Id", "Version")
    VALUES (1, '00000000-0000-0000-0000-000000000000');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    CREATE UNIQUE INDEX "IX_Accounts_Login" ON "Accounts" ("Login");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    CREATE INDEX "IX_LoginSessions_AccountId" ON "LoginSessions" ("AccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    CREATE INDEX "IX_LoginSessions_ExpiresAt" ON "LoginSessions" ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921122629_AddAuthentication') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921122629_AddAuthentication', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921194703_AddPageAudits') THEN
    ALTER TABLE "TestSuites" ADD "IsPageAudit" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921194703_AddPageAudits') THEN
    CREATE UNIQUE INDEX "IX_TestSuites_PageAuditProject" ON "TestSuites" ("ProjectId") WHERE "IsPageAudit" = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921194703_AddPageAudits') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921194703_AddPageAudits', '10.0.12');
    END IF;
END $EF$;
COMMIT;
