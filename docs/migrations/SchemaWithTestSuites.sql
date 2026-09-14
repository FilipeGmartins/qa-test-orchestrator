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
