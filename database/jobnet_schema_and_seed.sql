/* =====================================================================
   Jobnet - Microsoft SQL Server schema + demo seed data
   ---------------------------------------------------------------------
   Target  : SQL Server 2017+ (uses nvarchar(max), datetime2, sequences
             of GO batches). Tested on SQL Server 2022.

   Order of operations:
       1. Create database  (skip if you already created `JobNet`).
       2. CREATE TABLEs    (primary keys defined inline).
       3. CREATE INDEXes   (unique + non-clustered).
       4. ALTER TABLE      add FOREIGN KEY constraints.
       5. INSERT data      (Companies & Users together, then everything
                            that references them).

   How to run from the command line:
       sqlcmd -S localhost,1433 -U sa -P "JobNet!Pass1" ^
              -i jobnet_schema_and_seed.txt

   Or paste straight into SQL Server Management Studio / Azure Data Studio.

   The GUIDs and password hashes below match what the EF Core seeder
   would produce, so you can use this script INSTEAD OF letting the API
   auto-seed at startup.  Demo accounts (passwords stored as BCrypt):
       admin@jobnet.ca       / admin123
       david@northbuild.ca   / employer123
       emma@maplecontractors.ca / employer123
       marcus@example.com    / worker123
       priya@example.com     / worker123
       liam@example.com      / worker123
   ===================================================================== */


/* ---------------------------------------------------------------------
   0. Database
   --------------------------------------------------------------------- */
IF DB_ID('JobNet') IS NULL
BEGIN
    CREATE DATABASE JobNet;
END
GO

USE JobNet;
GO


/* ---------------------------------------------------------------------
   1. Drop existing objects (safe re-run)
       Order matters: drop FKs first, then tables in dependency order.
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Applications',   'U') IS NOT NULL DROP TABLE dbo.Applications;
IF OBJECT_ID('dbo.Reviews',        'U') IS NOT NULL DROP TABLE dbo.Reviews;
IF OBJECT_ID('dbo.Notifications',  'U') IS NOT NULL DROP TABLE dbo.Notifications;
IF OBJECT_ID('dbo.Experiences',    'U') IS NOT NULL DROP TABLE dbo.Experiences;
IF OBJECT_ID('dbo.Certifications', 'U') IS NOT NULL DROP TABLE dbo.Certifications;
IF OBJECT_ID('dbo.WorkerSkills',   'U') IS NOT NULL DROP TABLE dbo.WorkerSkills;
IF OBJECT_ID('dbo.WorkerProfiles', 'U') IS NOT NULL DROP TABLE dbo.WorkerProfiles;
IF OBJECT_ID('dbo.JobSkills',      'U') IS NOT NULL DROP TABLE dbo.JobSkills;
IF OBJECT_ID('dbo.Jobs',           'U') IS NOT NULL DROP TABLE dbo.Jobs;
IF OBJECT_ID('dbo.Users',          'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Companies',      'U') IS NOT NULL DROP TABLE dbo.Companies;
GO


/* =====================================================================
   2. TABLES (with PRIMARY KEYS)
   ===================================================================== */

-- ----------------------------- Companies ------------------------------
CREATE TABLE dbo.Companies (
    Id              uniqueidentifier NOT NULL CONSTRAINT PK_Companies PRIMARY KEY,
    OwnerId         uniqueidentifier NOT NULL,
    Name            nvarchar(200)    NOT NULL,
    Industry        nvarchar(100)    NULL,
    BusinessNumber  nvarchar(40)     NULL,
    Website         nvarchar(300)    NULL,
    Email           nvarchar(256)    NULL,
    Phone           nvarchar(40)     NULL,
    [Address]       nvarchar(300)    NULL,
    City            nvarchar(100)    NULL,
    Province        nvarchar(8)      NULL,
    FoundedYear     int              NULL,
    EmployeeCount   nvarchar(40)     NULL,
    [Description]   nvarchar(2000)   NULL,
    Rating          float            NOT NULL CONSTRAINT DF_Companies_Rating       DEFAULT (0),
    ReviewCount     int              NOT NULL CONSTRAINT DF_Companies_ReviewCount  DEFAULT (0),
    Verified        bit              NOT NULL CONSTRAINT DF_Companies_Verified     DEFAULT (0),
    CreatedAt       datetime2        NOT NULL CONSTRAINT DF_Companies_CreatedAt    DEFAULT (SYSUTCDATETIME())
);
GO

-- ------------------------------- Users --------------------------------
-- Role:   0=Worker, 1=Employer, 2=Admin, 3=Moderator
-- Status: 0=Active, 1=Suspended, 2=Pending
CREATE TABLE dbo.Users (
    Id            uniqueidentifier NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    [Role]        int              NOT NULL,
    FirstName     nvarchar(100)    NOT NULL,
    LastName      nvarchar(100)    NOT NULL,
    Email         nvarchar(256)    NOT NULL,
    PasswordHash  nvarchar(256)    NOT NULL,
    Phone         nvarchar(40)     NULL,
    City          nvarchar(100)    NULL,
    Province      nvarchar(8)      NULL,
    Avatar        nvarchar(4)      NOT NULL,
    [Status]      int              NOT NULL CONSTRAINT DF_Users_Status    DEFAULT (0),
    CreatedAt     datetime2        NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CompanyId     uniqueidentifier NULL
);
GO

-- --------------------------- WorkerProfiles ---------------------------
CREATE TABLE dbo.WorkerProfiles (
    UserId           uniqueidentifier NOT NULL CONSTRAINT PK_WorkerProfiles PRIMARY KEY,
    Headline         nvarchar(200)    NULL,
    Bio              nvarchar(2000)   NULL,
    YearsExperience  int              NOT NULL CONSTRAINT DF_WorkerProfiles_Years DEFAULT (0),
    HourlyRate       decimal(10,2)    NOT NULL CONSTRAINT DF_WorkerProfiles_Rate  DEFAULT (0),
    Availability     nvarchar(40)     NOT NULL CONSTRAINT DF_WorkerProfiles_Avail DEFAULT (N'Flexible'),
    Rating           float            NOT NULL CONSTRAINT DF_WorkerProfiles_Rating       DEFAULT (0),
    ReviewCount      int              NOT NULL CONSTRAINT DF_WorkerProfiles_ReviewCount  DEFAULT (0)
);
GO

-- ---------------------------- WorkerSkills ----------------------------
CREATE TABLE dbo.WorkerSkills (
    Id        uniqueidentifier NOT NULL CONSTRAINT PK_WorkerSkills PRIMARY KEY,
    WorkerId  uniqueidentifier NOT NULL,
    [Name]    nvarchar(80)     NOT NULL
);
GO

-- --------------------------- Certifications ---------------------------
CREATE TABLE dbo.Certifications (
    Id        uniqueidentifier NOT NULL CONSTRAINT PK_Certifications PRIMARY KEY,
    WorkerId  uniqueidentifier NOT NULL,
    [Name]    nvarchar(200)    NOT NULL,
    Issuer    nvarchar(200)    NULL,
    [Year]    int              NOT NULL
);
GO

-- ----------------------------- Experiences ----------------------------
CREATE TABLE dbo.Experiences (
    Id        uniqueidentifier NOT NULL CONSTRAINT PK_Experiences PRIMARY KEY,
    WorkerId  uniqueidentifier NOT NULL,
    Title     nvarchar(200)    NOT NULL,
    Company   nvarchar(200)    NOT NULL,
    [From]    nvarchar(20)     NOT NULL,
    [To]      nvarchar(20)     NOT NULL
);
GO

-- -------------------------------- Jobs --------------------------------
-- PaymentType: 0=Hourly, 1=Fixed, 2=Daily
-- Status:      0=Open,   1=Paused, 2=Closed, 3=Filled
CREATE TABLE dbo.Jobs (
    Id            uniqueidentifier NOT NULL CONSTRAINT PK_Jobs PRIMARY KEY,
    CompanyId     uniqueidentifier NOT NULL,
    Title         nvarchar(200)    NOT NULL,
    Category      nvarchar(80)     NOT NULL,
    [Description] nvarchar(max)    NOT NULL,
    Activity      nvarchar(2000)   NULL,
    Location      nvarchar(200)    NOT NULL,
    DueDate       datetime2        NOT NULL,
    PaymentType   int              NOT NULL,
    PaymentAmount decimal(12,2)    NOT NULL,
    Currency      nvarchar(8)      NOT NULL CONSTRAINT DF_Jobs_Currency DEFAULT (N'CAD'),
    [Status]      int              NOT NULL CONSTRAINT DF_Jobs_Status   DEFAULT (0),
    PostedAt      datetime2        NOT NULL CONSTRAINT DF_Jobs_PostedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ------------------------------ JobSkills -----------------------------
CREATE TABLE dbo.JobSkills (
    Id      uniqueidentifier NOT NULL CONSTRAINT PK_JobSkills PRIMARY KEY,
    JobId   uniqueidentifier NOT NULL,
    [Name]  nvarchar(80)     NOT NULL
);
GO

-- ---------------------------- Applications ----------------------------
-- Status: 0=Submitted, 1=Shortlisted, 2=Selected, 3=Rejected, 4=Withdrawn
CREATE TABLE dbo.Applications (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Applications PRIMARY KEY,
    JobId        uniqueidentifier NOT NULL,
    WorkerId     uniqueidentifier NOT NULL,
    CoverLetter  nvarchar(4000)   NOT NULL,
    ExpectedRate decimal(12,2)    NOT NULL,
    [Status]     int              NOT NULL CONSTRAINT DF_Applications_Status      DEFAULT (0),
    SubmittedAt  datetime2        NOT NULL CONSTRAINT DF_Applications_SubmittedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ------------------------------ Reviews -------------------------------
CREATE TABLE dbo.Reviews (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_Reviews PRIMARY KEY,
    FromUserId   uniqueidentifier NOT NULL,
    ToUserId     uniqueidentifier NULL,
    ToCompanyId  uniqueidentifier NULL,
    JobId        uniqueidentifier NULL,
    Rating       int              NOT NULL,
    Comment      nvarchar(2000)   NOT NULL,
    CreatedAt    datetime2        NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    -- A review targets exactly one of: a user OR a company.
    CONSTRAINT CK_Reviews_Target CHECK (
        (ToUserId IS NOT NULL AND ToCompanyId IS NULL) OR
        (ToUserId IS NULL     AND ToCompanyId IS NOT NULL)
    )
);
GO

-- ---------------------------- Notifications ---------------------------
-- Type: 0=Application, 1=Status, 2=Review, 3=System
CREATE TABLE dbo.Notifications (
    Id         uniqueidentifier NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId     uniqueidentifier NOT NULL,
    [Type]     int              NOT NULL,
    Title      nvarchar(200)    NOT NULL,
    [Message]  nvarchar(1000)   NOT NULL,
    [Link]     nvarchar(500)    NULL,
    [Read]     bit              NOT NULL CONSTRAINT DF_Notifications_Read      DEFAULT (0),
    CreatedAt  datetime2        NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO


/* =====================================================================
   3. INDEXES
   ===================================================================== */
CREATE UNIQUE INDEX IX_Users_Email                  ON dbo.Users (Email);
CREATE        INDEX IX_Users_CompanyId              ON dbo.Users (CompanyId);

CREATE        INDEX IX_Companies_OwnerId            ON dbo.Companies (OwnerId);

CREATE        INDEX IX_Jobs_CompanyId               ON dbo.Jobs (CompanyId);
CREATE        INDEX IX_Jobs_Status                  ON dbo.Jobs ([Status]);
CREATE        INDEX IX_Jobs_PostedAt                ON dbo.Jobs (PostedAt);
CREATE        INDEX IX_JobSkills_JobId              ON dbo.JobSkills (JobId);

CREATE UNIQUE INDEX IX_WorkerSkills_WorkerId_Name   ON dbo.WorkerSkills (WorkerId, [Name]);
CREATE        INDEX IX_Certifications_WorkerId      ON dbo.Certifications (WorkerId);
CREATE        INDEX IX_Experiences_WorkerId         ON dbo.Experiences (WorkerId);

CREATE UNIQUE INDEX IX_Applications_JobId_WorkerId  ON dbo.Applications (JobId, WorkerId);
CREATE        INDEX IX_Applications_WorkerId        ON dbo.Applications (WorkerId);

CREATE        INDEX IX_Reviews_FromUserId           ON dbo.Reviews (FromUserId);
CREATE        INDEX IX_Reviews_ToUserId             ON dbo.Reviews (ToUserId);
CREATE        INDEX IX_Reviews_ToCompanyId          ON dbo.Reviews (ToCompanyId);
CREATE        INDEX IX_Reviews_JobId                ON dbo.Reviews (JobId);

CREATE        INDEX IX_Notifications_UserId_CreatedAt ON dbo.Notifications (UserId, CreatedAt);
GO


/* =====================================================================
   4. FOREIGN KEYS
   ===================================================================== */
ALTER TABLE dbo.Users
    ADD CONSTRAINT FK_Users_Companies_CompanyId
        FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id)
        ON DELETE SET NULL;

ALTER TABLE dbo.Companies
    ADD CONSTRAINT FK_Companies_Users_OwnerId
        FOREIGN KEY (OwnerId) REFERENCES dbo.Users (Id)
        ON DELETE NO ACTION;

ALTER TABLE dbo.WorkerProfiles
    ADD CONSTRAINT FK_WorkerProfiles_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
        ON DELETE CASCADE;

ALTER TABLE dbo.WorkerSkills
    ADD CONSTRAINT FK_WorkerSkills_WorkerProfiles_WorkerId
        FOREIGN KEY (WorkerId) REFERENCES dbo.WorkerProfiles (UserId)
        ON DELETE CASCADE;

ALTER TABLE dbo.Certifications
    ADD CONSTRAINT FK_Certifications_WorkerProfiles_WorkerId
        FOREIGN KEY (WorkerId) REFERENCES dbo.WorkerProfiles (UserId)
        ON DELETE CASCADE;

ALTER TABLE dbo.Experiences
    ADD CONSTRAINT FK_Experiences_WorkerProfiles_WorkerId
        FOREIGN KEY (WorkerId) REFERENCES dbo.WorkerProfiles (UserId)
        ON DELETE CASCADE;

ALTER TABLE dbo.Jobs
    ADD CONSTRAINT FK_Jobs_Companies_CompanyId
        FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id)
        ON DELETE CASCADE;

ALTER TABLE dbo.JobSkills
    ADD CONSTRAINT FK_JobSkills_Jobs_JobId
        FOREIGN KEY (JobId) REFERENCES dbo.Jobs (Id)
        ON DELETE CASCADE;

ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_Jobs_JobId
        FOREIGN KEY (JobId) REFERENCES dbo.Jobs (Id)
        ON DELETE CASCADE;

ALTER TABLE dbo.Applications
    ADD CONSTRAINT FK_Applications_Users_WorkerId
        FOREIGN KEY (WorkerId) REFERENCES dbo.Users (Id)
        ON DELETE NO ACTION;

ALTER TABLE dbo.Reviews
    ADD CONSTRAINT FK_Reviews_Users_FromUserId
        FOREIGN KEY (FromUserId) REFERENCES dbo.Users (Id)
        ON DELETE NO ACTION;

ALTER TABLE dbo.Reviews
    ADD CONSTRAINT FK_Reviews_Users_ToUserId
        FOREIGN KEY (ToUserId) REFERENCES dbo.Users (Id)
        ON DELETE NO ACTION;

ALTER TABLE dbo.Reviews
    ADD CONSTRAINT FK_Reviews_Companies_ToCompanyId
        FOREIGN KEY (ToCompanyId) REFERENCES dbo.Companies (Id)
        ON DELETE NO ACTION;

ALTER TABLE dbo.Reviews
    ADD CONSTRAINT FK_Reviews_Jobs_JobId
        FOREIGN KEY (JobId) REFERENCES dbo.Jobs (Id)
        ON DELETE SET NULL;

ALTER TABLE dbo.Notifications
    ADD CONSTRAINT FK_Notifications_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
        ON DELETE CASCADE;
GO


/* =====================================================================
   5. SEED DATA
   ---------------------------------------------------------------------
   IDs are the same deterministic GUIDs the EF Core seeder uses, so this
   script and the API auto-seeder can be used interchangeably.
   ===================================================================== */

-- Users and Companies reference each other (Users.CompanyId -> Companies.Id
-- AND Companies.OwnerId -> Users.Id), so we insert in three steps:
--   5.1  Insert Users with CompanyId NULL  (satisfies Companies.OwnerId FK later).
--   5.2  Insert Companies  (OwnerId now points to existing Users).
--   5.3  UPDATE the employer Users to attach their CompanyId.

-- 5.1 Users (CompanyId left NULL for now)
-- Passwords are BCrypt hashes (cost 11) of: admin123 / employer123 / worker123
INSERT INTO dbo.Users
    (Id, [Role], FirstName, LastName, Email, PasswordHash,
     Phone, City, Province, Avatar, [Status], CreatedAt, CompanyId)
VALUES
    ('b5b60ea1-705b-982a-14c7-99d136a1f17d', 2, N'Sarah',  N'Mitchell',
     N'admin@jobnet.ca',
     N'$2a$11$lyIU2ITVGusPEzMhbp1IDuPc6g664oI/A30NDXWb0aJY2KRt.1XO6',
     N'+1 (604) 555-0001', N'Vancouver', N'BC', N'SM', 0, SYSUTCDATETIME(), NULL),

    ('8332f183-9cab-2fb1-872d-d6e30f828dbe', 1, N'David', N'Chen',
     N'david@northbuild.ca',
     N'$2a$11$eM6AiBV.SmjbuwHVaTfd7O3j2WX2lqIKzB1GdY9yJY.AsbiubE59u',
     N'+1 (416) 555-0123', N'Toronto', N'ON', N'DC', 0, SYSUTCDATETIME(), NULL),

    ('28828ca1-ade8-bb38-bfdc-fb8f1da929b2', 1, N'Emma',  N'Tremblay',
     N'emma@maplecontractors.ca',
     N'$2a$11$eM6AiBV.SmjbuwHVaTfd7O3j2WX2lqIKzB1GdY9yJY.AsbiubE59u',
     N'+1 (514) 555-0188', N'Montreal', N'QC', N'ET', 0, SYSUTCDATETIME(), NULL),

    ('7274d5c6-3bdb-f7ce-26d0-d10de0943146', 0, N'Marcus', N'Johnson',
     N'marcus@example.com',
     N'$2a$11$n/w/RjPMsRVZF6adjQoS9uart1b65x8ocVM3OzG6MfainanXWMVMG',
     N'+1 (403) 555-0177', N'Calgary', N'AB', N'MJ', 0, SYSUTCDATETIME(), NULL),

    ('30bd3309-9624-2a6c-67ed-c337835ead40', 0, N'Priya',  N'Sharma',
     N'priya@example.com',
     N'$2a$11$n/w/RjPMsRVZF6adjQoS9uart1b65x8ocVM3OzG6MfainanXWMVMG',
     N'+1 (647) 555-0145', N'Toronto', N'ON', N'PS', 0, SYSUTCDATETIME(), NULL),

    ('3aebc015-5a47-95a1-abea-0c0573fd8e24', 0, N'Liam',   N'O''Brien',
     N'liam@example.com',
     N'$2a$11$n/w/RjPMsRVZF6adjQoS9uart1b65x8ocVM3OzG6MfainanXWMVMG',
     N'+1 (902) 555-0199', N'Halifax', N'NS', N'LO', 0, SYSUTCDATETIME(), NULL);
GO

-- 5.2 Companies (OwnerId points to existing Users)
INSERT INTO dbo.Companies
    (Id, OwnerId, Name, Industry, BusinessNumber, Website, Email, Phone,
     [Address], City, Province, FoundedYear, EmployeeCount, [Description],
     Rating, ReviewCount, Verified, CreatedAt)
VALUES
    ('2c6d418d-57ea-d74b-3c54-ad99695fc490',  -- c-1
     '8332f183-9cab-2fb1-872d-d6e30f828dbe',  -- owner = u-emp-1 (David)
     N'NorthBuild Construction Inc.', N'Commercial Construction', N'123456789RC0001',
     N'https://northbuild.ca', N'contact@northbuild.ca', N'+1 (416) 555-0100',
     N'120 King Street West, Toronto, ON', N'Toronto', N'ON', 2008, N'50-200',
     N'NorthBuild is a leading commercial construction firm in the GTA, specializing in office buildings, retail spaces, and mixed-use developments.',
     4.6, 32, 1, SYSUTCDATETIME()),

    ('3e17c259-8cf5-7703-bb98-ffb5e4da06f5',  -- c-2
     '28828ca1-ade8-bb38-bfdc-fb8f1da929b2',  -- owner = u-emp-2 (Emma)
     N'Maple Contractors Ltd.', N'Residential & Renovations', N'987654321RC0001',
     N'https://maplecontractors.ca', N'hello@maplecontractors.ca', N'+1 (514) 555-0100',
     N'450 Rue Saint-Jacques, Montreal, QC', N'Montreal', N'QC', 2014, N'10-50',
     N'Family-run residential renovation specialists serving the Greater Montreal area for over a decade.',
     4.3, 18, 1, SYSUTCDATETIME());
GO

-- 5.3 Attach employers to their companies
UPDATE dbo.Users SET CompanyId = '2c6d418d-57ea-d74b-3c54-ad99695fc490'
 WHERE Id = '8332f183-9cab-2fb1-872d-d6e30f828dbe';   -- David -> NorthBuild

UPDATE dbo.Users SET CompanyId = '3e17c259-8cf5-7703-bb98-ffb5e4da06f5'
 WHERE Id = '28828ca1-ade8-bb38-bfdc-fb8f1da929b2';   -- Emma  -> Maple Contractors
GO

-- 5.4 WorkerProfiles (PK is UserId)
INSERT INTO dbo.WorkerProfiles
    (UserId, Headline, Bio, YearsExperience, HourlyRate, Availability, Rating, ReviewCount)
VALUES
    ('7274d5c6-3bdb-f7ce-26d0-d10de0943146',  -- Marcus
     N'Journeyman Electrician - Red Seal Certified',
     N'Red Seal certified electrician with 8 years of experience in commercial and industrial wiring. Available for short and long-term contracts across Alberta.',
     8, 55.00, N'Full-time', 4.8, 14),

    ('30bd3309-9624-2a6c-67ed-c337835ead40',  -- Priya
     N'Interior Painter & Drywall Finisher',
     N'Detail-oriented painter and drywall finisher with experience in high-end residential renovations.',
     5, 38.00, N'Part-time', 4.5, 9),

    ('3aebc015-5a47-95a1-abea-0c0573fd8e24',  -- Liam
     N'General Labourer / Site Helper',
     N'Reliable site labourer comfortable with demolition, material handling, and clean-up. Available for short-term gigs across the Maritimes.',
     2, 24.00, N'Flexible', 4.2, 4);
GO

-- 5.5 WorkerSkills
INSERT INTO dbo.WorkerSkills (Id, WorkerId, [Name]) VALUES
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Electrical'),
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Commercial Wiring'),
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Conduit Bending'),
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Blueprint Reading'),
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Troubleshooting'),

    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Painting'),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Drywall'),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Taping & Mudding'),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Wallpaper'),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Surface Prep'),

    (NEWID(), '3aebc015-5a47-95a1-abea-0c0573fd8e24', N'Demolition'),
    (NEWID(), '3aebc015-5a47-95a1-abea-0c0573fd8e24', N'Material Handling'),
    (NEWID(), '3aebc015-5a47-95a1-abea-0c0573fd8e24', N'Site Clean-up'),
    (NEWID(), '3aebc015-5a47-95a1-abea-0c0573fd8e24', N'Hand Tools');
GO

-- 5.6 Certifications
INSERT INTO dbo.Certifications (Id, WorkerId, [Name], Issuer, [Year]) VALUES
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Red Seal - Construction Electrician', N'Government of Canada', 2019),
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Working at Heights',                  N'Alberta OHS',          2024),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'WHMIS 2015',                          N'CCOHS',                2023),
    (NEWID(), '3aebc015-5a47-95a1-abea-0c0573fd8e24', N'WHMIS 2015',                          N'CCOHS',                2025);
GO

-- 5.7 Experiences
INSERT INTO dbo.Experiences (Id, WorkerId, Title, Company, [From], [To]) VALUES
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Lead Electrician',       N'BrightSpark Electric',  N'2022', N'Present'),
    (NEWID(), '7274d5c6-3bdb-f7ce-26d0-d10de0943146', N'Journeyman Electrician', N'PowerLine Services',    N'2018', N'2022'),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Senior Painter',         N'Freelance',             N'2021', N'Present'),
    (NEWID(), '30bd3309-9624-2a6c-67ed-c337835ead40', N'Painter',                N'Crisp Coats Inc.',      N'2019', N'2021'),
    (NEWID(), '3aebc015-5a47-95a1-abea-0c0573fd8e24', N'General Labourer',       N'Atlantic Build Co.',    N'2024', N'Present');
GO

-- 5.8 Jobs
INSERT INTO dbo.Jobs
    (Id, CompanyId, Title, Category, [Description], Activity, Location,
     DueDate, PaymentType, PaymentAmount, Currency, [Status], PostedAt)
VALUES
    ('d3841e47-7446-f81f-a895-38b9b2ca761b',  -- j-1
     '2c6d418d-57ea-d74b-3c54-ad99695fc490',  -- NorthBuild
     N'Commercial Electrician - Downtown Tower Project', N'Electrical',
     N'We are hiring 3 commercial electricians for a 12-month tower fit-out in downtown Toronto. You will install conduit, pull wire, and terminate panels under the supervision of a master electrician.',
     N'Day shifts Monday-Friday, occasional Saturdays. Tools provided on site; PPE required.',
     N'Toronto, ON', '2026-08-31T00:00:00', 0, 52.00, N'CAD', 0, '2026-05-10T10:00:00'),

    ('130b50d7-4407-4762-7780-e592d9f8d6b1',  -- j-2
     '2c6d418d-57ea-d74b-3c54-ad99695fc490',
     N'Site Supervisor - Retail Renovation', N'Supervision',
     N'Site supervisor needed for an 8-week retail renovation in Mississauga. Coordinate trades, manage timelines, and ensure safety compliance.',
     N'Full-time, 8 weeks', N'Mississauga, ON',
     '2026-07-15T00:00:00', 1, 18000.00, N'CAD', 0, '2026-05-14T14:30:00'),

    ('877a8d51-6724-75fb-624c-23e544a5f3fc',  -- j-3
     '3e17c259-8cf5-7703-bb98-ffb5e4da06f5',  -- Maple Contractors
     N'Interior Painter - Plateau Condo Refresh', N'Painting',
     N'Paint 12 condo units in the Plateau. Surfaces are prepped. We supply paint and materials - you bring brushes, rollers, and drop sheets.',
     N'3-week contract starting June 1', N'Montreal, QC',
     '2026-06-21T00:00:00', 1, 6400.00, N'CAD', 0, '2026-05-12T09:15:00'),

    ('71d8a241-1b7e-c5db-6176-cdaf5c0810c9',  -- j-4
     '3e17c259-8cf5-7703-bb98-ffb5e4da06f5',
     N'General Labourer - Demolition Crew', N'General Labour',
     N'Two general labourers needed for a kitchen and bathroom demolition in Westmount. Must be comfortable with manual labour and lifting.',
     N'1-week project, Mon-Fri 8-4', N'Westmount, QC',
     '2026-06-07T00:00:00', 0, 26.00, N'CAD', 0, '2026-05-18T08:00:00'),

    ('fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5',  -- j-5  (Closed)
     '2c6d418d-57ea-d74b-3c54-ad99695fc490',
     N'HVAC Technician - Office Retrofit', N'HVAC',
     N'Licensed HVAC technician needed for a 6-week office retrofit. Install rooftop units and ductwork.',
     N'6 weeks, full-time', N'Toronto, ON',
     '2026-07-30T00:00:00', 0, 48.00, N'CAD', 2, '2026-04-22T11:00:00');
GO

-- 5.9 JobSkills
INSERT INTO dbo.JobSkills (Id, JobId, [Name]) VALUES
    (NEWID(), 'd3841e47-7446-f81f-a895-38b9b2ca761b', N'Commercial Wiring'),
    (NEWID(), 'd3841e47-7446-f81f-a895-38b9b2ca761b', N'Conduit Bending'),
    (NEWID(), 'd3841e47-7446-f81f-a895-38b9b2ca761b', N'Blueprint Reading'),

    (NEWID(), '130b50d7-4407-4762-7780-e592d9f8d6b1', N'Supervision'),
    (NEWID(), '130b50d7-4407-4762-7780-e592d9f8d6b1', N'Scheduling'),
    (NEWID(), '130b50d7-4407-4762-7780-e592d9f8d6b1', N'Safety Compliance'),

    (NEWID(), '877a8d51-6724-75fb-624c-23e544a5f3fc', N'Painting'),
    (NEWID(), '877a8d51-6724-75fb-624c-23e544a5f3fc', N'Surface Prep'),

    (NEWID(), '71d8a241-1b7e-c5db-6176-cdaf5c0810c9', N'Demolition'),
    (NEWID(), '71d8a241-1b7e-c5db-6176-cdaf5c0810c9', N'Hand Tools'),
    (NEWID(), '71d8a241-1b7e-c5db-6176-cdaf5c0810c9', N'Material Handling'),

    (NEWID(), 'fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5', N'HVAC'),
    (NEWID(), 'fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5', N'Ductwork'),
    (NEWID(), 'fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5', N'Sheet Metal');
GO

-- 5.10 Applications
-- Status:  0=Submitted, 1=Shortlisted, 2=Selected, 3=Rejected, 4=Withdrawn
INSERT INTO dbo.Applications
    (Id, JobId, WorkerId, CoverLetter, ExpectedRate, [Status], SubmittedAt)
VALUES
    (NEWID(),
     'd3841e47-7446-f81f-a895-38b9b2ca761b',  -- j-1
     '7274d5c6-3bdb-f7ce-26d0-d10de0943146',  -- Marcus
     N'I have 8 years of commercial wiring experience including two recent tower projects in Calgary. Available immediately.',
     55.00, 0, '2026-05-11T13:20:00'),

    (NEWID(),
     '877a8d51-6724-75fb-624c-23e544a5f3fc',  -- j-3
     '30bd3309-9624-2a6c-67ed-c337835ead40',  -- Priya
     N'I have completed similar condo refreshes in the Plateau and can start June 1. References available on request.',
     38.00, 1, '2026-05-13T10:45:00'),

    (NEWID(),
     '71d8a241-1b7e-c5db-6176-cdaf5c0810c9',  -- j-4
     '3aebc015-5a47-95a1-abea-0c0573fd8e24',  -- Liam
     N'Available for the full week. Comfortable with demolition and clean-up.',
     26.00, 0, '2026-05-19T07:50:00'),

    (NEWID(),
     'fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5',  -- j-5
     '7274d5c6-3bdb-f7ce-26d0-d10de0943146',  -- Marcus
     N'Cross-trained in HVAC controls and rooftop unit installs.',
     50.00, 3, '2026-04-25T15:10:00');
GO

-- 5.11 Reviews
INSERT INTO dbo.Reviews
    (Id, FromUserId, ToUserId, ToCompanyId, JobId, Rating, Comment, CreatedAt)
VALUES
    (NEWID(),
     '8332f183-9cab-2fb1-872d-d6e30f828dbe',  -- David (employer) reviews Marcus (worker)
     '7274d5c6-3bdb-f7ce-26d0-d10de0943146', NULL,
     'fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5',
     5, N'Marcus did excellent work on our last retrofit. Highly recommended.', '2026-04-30T16:00:00'),

    (NEWID(),
     '7274d5c6-3bdb-f7ce-26d0-d10de0943146',  -- Marcus reviews NorthBuild
     NULL, '2c6d418d-57ea-d74b-3c54-ad99695fc490',
     'fde1a9db-7be5-9f18-bdb0-0fae16ae5ff5',
     5, N'Clear scope, on-time payments, professional site management.', '2026-04-30T17:00:00');
GO

-- 5.12 Notifications
-- Type: 0=Application, 1=Status
INSERT INTO dbo.Notifications
    (Id, UserId, [Type], Title, [Message], [Link], [Read], CreatedAt)
VALUES
    (NEWID(),
     '8332f183-9cab-2fb1-872d-d6e30f828dbe',  -- David (employer)
     0, N'New application',
     N'Marcus Johnson applied to "Commercial Electrician - Downtown Tower Project".',
     N'/employer/jobs/d3841e47-7446-f81f-a895-38b9b2ca761b',
     0, '2026-05-11T13:20:00'),

    (NEWID(),
     '30bd3309-9624-2a6c-67ed-c337835ead40',  -- Priya
     1, N'You have been shortlisted',
     N'Maple Contractors shortlisted you for "Interior Painter - Plateau Condo Refresh".',
     N'/worker/applications',
     0, '2026-05-15T09:00:00'),

    (NEWID(),
     '7274d5c6-3bdb-f7ce-26d0-d10de0943146',  -- Marcus
     1, N'Application update',
     N'Your application for "HVAC Technician - Office Retrofit" was not selected.',
     N'/worker/applications',
     1, '2026-04-28T11:30:00');
GO


/* =====================================================================
   6. Sanity check
   ===================================================================== */
SELECT 'Users'          AS [Table], COUNT(*) AS [Rows] FROM dbo.Users
UNION ALL SELECT 'Companies',       COUNT(*)            FROM dbo.Companies
UNION ALL SELECT 'WorkerProfiles',  COUNT(*)            FROM dbo.WorkerProfiles
UNION ALL SELECT 'WorkerSkills',    COUNT(*)            FROM dbo.WorkerSkills
UNION ALL SELECT 'Certifications',  COUNT(*)            FROM dbo.Certifications
UNION ALL SELECT 'Experiences',     COUNT(*)            FROM dbo.Experiences
UNION ALL SELECT 'Jobs',            COUNT(*)            FROM dbo.Jobs
UNION ALL SELECT 'JobSkills',       COUNT(*)            FROM dbo.JobSkills
UNION ALL SELECT 'Applications',    COUNT(*)            FROM dbo.Applications
UNION ALL SELECT 'Reviews',         COUNT(*)            FROM dbo.Reviews
UNION ALL SELECT 'Notifications',   COUNT(*)            FROM dbo.Notifications;
GO
