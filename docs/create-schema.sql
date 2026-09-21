CREATE DATABASE IF NOT EXISTS rezerv_booking
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE rezerv_booking;

CREATE TABLE Businesses (
  Id INT NOT NULL AUTO_INCREMENT,
  Name VARCHAR(150) NOT NULL,
  PRIMARY KEY (Id)
) ENGINE=InnoDB;

CREATE TABLE Customers (
  Id INT NOT NULL AUTO_INCREMENT,
  Name VARCHAR(150) NOT NULL,
  Email VARCHAR(254) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Customers_Email (Email)
) ENGINE=InnoDB;

CREATE TABLE Packages (
  Id INT NOT NULL AUTO_INCREMENT,
  CustomerId INT NOT NULL,
  BusinessId INT NOT NULL,
  TotalCredits INT NOT NULL,
  RemainingCredits INT NOT NULL,
  ExpiresAtUtc DATETIME(6) NOT NULL,
  PurchasedAtUtc DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_Packages_CustomerId_BusinessId_ExpiresAtUtc (CustomerId, BusinessId, ExpiresAtUtc),
  CONSTRAINT FK_Packages_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
  CONSTRAINT FK_Packages_Businesses FOREIGN KEY (BusinessId) REFERENCES Businesses(Id),
  CONSTRAINT CK_Packages_Credits CHECK (TotalCredits > 0 AND RemainingCredits >= 0 AND RemainingCredits <= TotalCredits)
) ENGINE=InnoDB;

CREATE TABLE Schedules (
  Id INT NOT NULL AUTO_INCREMENT,
  BusinessId INT NOT NULL,
  ClassName VARCHAR(150) NOT NULL,
  InstructorName VARCHAR(150) NOT NULL,
  StartTimeUtc DATETIME(6) NOT NULL,
  EndTimeUtc DATETIME(6) NOT NULL,
  Capacity INT NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_Schedules_BusinessId_StartTimeUtc (BusinessId, StartTimeUtc),
  CONSTRAINT FK_Schedules_Businesses FOREIGN KEY (BusinessId) REFERENCES Businesses(Id),
  CONSTRAINT CK_Schedules_TimeAndCapacity CHECK (EndTimeUtc > StartTimeUtc AND Capacity > 0)
) ENGINE=InnoDB;

CREATE TABLE Bookings (
  Id INT NOT NULL AUTO_INCREMENT,
  CustomerId INT NOT NULL,
  TimetableScheduleId INT NOT NULL,
  PackageId INT NOT NULL,
  Status INT NOT NULL DEFAULT 0,
  BookedAtUtc DATETIME(6) NOT NULL,
  CancelledAtUtc DATETIME(6) NULL,
  CreditRefunded TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (Id),
  KEY IX_Bookings_TimetableScheduleId_Status (TimetableScheduleId, Status),
  KEY IX_Bookings_CustomerId_Status (CustomerId, Status),
  CONSTRAINT FK_Bookings_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
  CONSTRAINT FK_Bookings_Schedules FOREIGN KEY (TimetableScheduleId) REFERENCES Schedules(Id),
  CONSTRAINT FK_Bookings_Packages FOREIGN KEY (PackageId) REFERENCES Packages(Id)
) ENGINE=InnoDB;

CREATE TABLE WaitlistEntries (
  Id INT NOT NULL AUTO_INCREMENT,
  CustomerId INT NOT NULL,
  TimetableScheduleId INT NOT NULL,
  Status INT NOT NULL DEFAULT 0,
  JoinedAtUtc DATETIME(6) NOT NULL,
  PromotedAtUtc DATETIME(6) NULL,
  PRIMARY KEY (Id),
  KEY IX_WaitlistEntries_TimetableScheduleId_Status_JoinedAtUtc (TimetableScheduleId, Status, JoinedAtUtc),
  CONSTRAINT FK_WaitlistEntries_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
  CONSTRAINT FK_WaitlistEntries_Schedules FOREIGN KEY (TimetableScheduleId) REFERENCES Schedules(Id)
) ENGINE=InnoDB;
