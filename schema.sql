-- ============================================================
-- Banking API — MySQL 8.0+ DDL Reference Script
-- ============================================================
-- This file is a backup reference only.
-- The authoritative schema is managed by EF Core migrations:
--
--   dotnet ef database update \
--     --project src/BankingApi.Infrastructure \
--     --startup-project src/BankingApi.Api
--
-- To apply this script directly:
--   mysql -u root -p banking_db < db-scripts/schema.sql
-- ============================================================

CREATE DATABASE IF NOT EXISTS banking_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE banking_db;

-- ============================================================
-- Users
-- ============================================================
-- Includes regular customer users AND the two NGL system users.
-- Gender = 'System' identifies NGL internal users.
-- Password is always a BCrypt hash (work factor 12).
-- ============================================================

CREATE TABLE IF NOT EXISTS Users (
    Id         CHAR(36)     NOT NULL,
    FirstName  VARCHAR(100) NOT NULL,
    MiddleName VARCHAR(100) NULL,
    LastName   VARCHAR(100) NOT NULL,
    Gender     VARCHAR(10)  NOT NULL
                     COMMENT 'Male | Female | Other | System',
    Address    VARCHAR(255) NULL,
    State      VARCHAR(100) NULL,
    Country    VARCHAR(100) NOT NULL DEFAULT 'Nigeria',
    Email      VARCHAR(150) NOT NULL,
    Password   VARCHAR(255) NOT NULL
                     COMMENT 'BCrypt hash, work factor 12',
    CreatedAt  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- Accounts
-- ============================================================
-- One account per user (1:1 with Users via UserId UNIQUE).
-- AccountType = 'Customer' for regular users.
-- AccountType = 'NGL'      for internal system pool accounts.
-- NglPoolType = 'Credit'   — receives Amount+Fee from sender.
-- NglPoolType = 'Debit'    — pays Amount out to recipient.
-- IsSystemAccount flag lets queries exclude NGL accounts easily.
-- BVN is NULL for NGL accounts; UNIQUE (partial) for customers.
-- ============================================================

CREATE TABLE IF NOT EXISTS Accounts (
    Id              CHAR(36)      NOT NULL,
    UserId          CHAR(36)      NOT NULL,
    AccountNumber   VARCHAR(10)   NOT NULL
                          COMMENT '10-digit zero-padded, system-generated',
    BVN             VARCHAR(11)   NULL
                          COMMENT '11-digit numeric; NULL for NGL accounts',
    Balance         DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    Currency        VARCHAR(10)   NOT NULL DEFAULT 'NGN',
    AccountType     VARCHAR(20)   NOT NULL DEFAULT 'Customer'
                          COMMENT 'Customer | NGL',
    NglPoolType     VARCHAR(20)   NULL
                          COMMENT 'Credit | Debit | Fee | NULL for customer accounts',
    IsSystemAccount TINYINT(1)    NOT NULL DEFAULT 0
                          COMMENT '1 = NGL system account, hidden from customer APIs',
    CreatedAt       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       DATETIME      NOT NULL,

    CONSTRAINT PK_Accounts        PRIMARY KEY (Id),
    CONSTRAINT FK_Accounts_Users  FOREIGN KEY (UserId)
        REFERENCES Users (Id)
        ON DELETE CASCADE,
    CONSTRAINT UQ_Accounts_UserId
        UNIQUE (UserId),
    CONSTRAINT UQ_Accounts_AccountNumber
        UNIQUE (AccountNumber)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- Partial unique index: BVN must be unique when not NULL
-- (NGL accounts have BVN = NULL and must not conflict with each other)
CREATE UNIQUE INDEX IX_Accounts_BVN
    ON Accounts (BVN)
    -- MySQL 8.0.13+ supports functional index expressions;
    -- use a generated column approach for older 8.0 patch versions.
    -- The EF Core migration uses HasFilter("BVN IS NOT NULL").
    -- Replace with the line below if your MySQL does not support
    -- filtered unique indexes via the client driver:
    --   ALTER TABLE Accounts ADD CONSTRAINT ... (manual partial workaround)
    WHERE (BVN IS NOT NULL);

-- ============================================================
-- Transactions
-- ============================================================
-- Every fund transfer produces EXACTLY 3 rows (legs) sharing
-- the same Reference value.
--
-- Leg types per Reference:
--   CustomerTransfer — Sender       → NGL Credit  (Amount + Fee)
--   FeeCapture       — NGL Credit   → NGL Debit   (Fee only)
--   NGLDebit         — NGL Debit    → Recipient   (Amount only)
--
-- Status values : Pending | Completed | Failed
-- Type values   : CustomerTransfer | FeeCapture | NGLDebit
-- ============================================================

CREATE TABLE IF NOT EXISTS Transactions (
    Id                  CHAR(36)      NOT NULL,
    Reference           VARCHAR(50)   NOT NULL
                              COMMENT 'Shared across all 3 legs of one transfer',
    SourceAccountNumber VARCHAR(10)   NOT NULL,
    DestAccountNumber   VARCHAR(10)   NOT NULL,
    Amount              DECIMAL(18,2) NOT NULL,
    Fee                 DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    TotalDebited        DECIMAL(18,2) NOT NULL,
    Narration           VARCHAR(255)  NULL,
    Status              VARCHAR(20)   NOT NULL
                              COMMENT 'Pending | Completed | Failed',
    Type                VARCHAR(20)   NOT NULL
                              COMMENT 'CustomerTransfer | FeeCapture | NGLDebit',
    CreatedAt           DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT PK_Transactions PRIMARY KEY (Id)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- Non-unique index on Reference — multiple legs share the same value
CREATE INDEX IX_Transactions_Reference
    ON Transactions (Reference);

-- ============================================================
-- Seed Data
-- ============================================================
-- NGL system accounts are seeded here as a SQL reference.
-- In the application, seeding is handled by DatabaseSeeder.cs
-- (idempotent, Development environment only).
--
-- Customer test accounts (john@test.com / jane@test.com) are
-- intentionally excluded from this script — use the application
-- seeder or register via POST /api/auth/register instead.
-- ============================================================

-- NGL Credit system user
INSERT IGNORE INTO Users
    (Id, FirstName, LastName, Gender, Email, Password, Country, CreatedAt)
VALUES (
    'aaaaaaaa-0001-0000-0000-000000000001',
    'NGL', 'Credit', 'System',
    'ngl.credit@system.internal',
    -- BCrypt hash of 'System@NGL1' (work factor 12) — regenerate before use
    '$2a$12$placeholderHashForNGLCreditReplaceWithRealBCryptHash000',
    'Nigeria',
    UTC_TIMESTAMP()
);

-- NGL Credit account (AccountNumber = 0000000001)
INSERT IGNORE INTO Accounts
    (Id, UserId, AccountNumber, BVN, Balance, Currency,
     AccountType, NglPoolType, IsSystemAccount, CreatedAt, UpdatedAt)
VALUES (
    'bbbbbbbb-0001-0000-0000-000000000001',
    'aaaaaaaa-0001-0000-0000-000000000001',
    '0000000001', NULL, 0.00, 'NGN',
    'NGL', 'Credit', 1,
    UTC_TIMESTAMP(), UTC_TIMESTAMP()
);

-- NGL Debit system user
INSERT IGNORE INTO Users
    (Id, FirstName, LastName, Gender, Email, Password, Country, CreatedAt)
VALUES (
    'aaaaaaaa-0002-0000-0000-000000000002',
    'NGL', 'Debit', 'System',
    'ngl.debit@system.internal',
    -- BCrypt hash of 'System@NGL2' (work factor 12) — regenerate before use
    '$2a$12$placeholderHashForNGLDebitReplaceWithRealBCryptHashXXXX',
    'Nigeria',
    UTC_TIMESTAMP()
);

-- NGL Debit account (AccountNumber = 0000000002, pre-funded ₦1,000,000)
INSERT IGNORE INTO Accounts
    (Id, UserId, AccountNumber, BVN, Balance, Currency,
     AccountType, NglPoolType, IsSystemAccount, CreatedAt, UpdatedAt)
VALUES (
    'bbbbbbbb-0002-0000-0000-000000000002',
    'aaaaaaaa-0002-0000-0000-000000000002',
    '0000000002', NULL, 1000000.00, 'NGN',
    'NGL', 'Debit', 1,
    UTC_TIMESTAMP(), UTC_TIMESTAMP()
);

-- ============================================================
-- Verification queries
-- ============================================================
-- Run these after applying the script to confirm setup:
--
--   SELECT Id, Email, Gender FROM Users;
--   SELECT AccountNumber, AccountType, NglPoolType,
--          IsSystemAccount, Balance FROM Accounts;
--   SHOW INDEX FROM Accounts;
--   SHOW INDEX FROM Transactions;
-- ============================================================