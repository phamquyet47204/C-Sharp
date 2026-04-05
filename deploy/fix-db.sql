USE VinhKhanhCleanDb;

DECLARE @constraintName NVARCHAR(256);
SELECT @constraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('Pois') AND c.name = 'Status';
IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Pois DROP CONSTRAINT ' + @constraintName);

ALTER TABLE Pois DROP COLUMN Status;
ALTER TABLE Pois ADD Status INT NOT NULL DEFAULT 0;
UPDATE Pois SET Status = CASE WHEN IsApproved = 1 THEN 2 ELSE 1 END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Pois_OwnerId' AND object_id=OBJECT_ID('Pois'))
    CREATE INDEX IX_Pois_OwnerId ON Pois(OwnerId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Pois_AspNetUsers_OwnerId')
    ALTER TABLE Pois ADD CONSTRAINT FK_Pois_AspNetUsers_OwnerId FOREIGN KEY (OwnerId) REFERENCES AspNetUsers(Id);

PRINT 'Fix complete';
