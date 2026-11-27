-- fix_office_type_isActive.sql
-- Fixes: "Conversion failed when converting the varchar value 'Y' to data type int"
-- Root cause: isActive column is char(1) ('Y'/'N') but SP compared with integer 1/0
-- Also fixes the @isActive parameter type from BIT to CHAR(1) and INSERT default from 1 to 'Y'

ALTER PROCEDURE usp_MasterOfficeType_CRUD
(
    @Action VARCHAR(30),
    @officeTypeID INT = NULL,
    @officeTypeName VARCHAR(150) = NULL,
    @isActive CHAR(1) = NULL,
    @searchText VARCHAR(150) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Action = 'INSERT'
    BEGIN
        INSERT INTO Master_OfficeType (officeTypeName, isActive)
        VALUES (@officeTypeName, ISNULL(@isActive, 'Y'));
        SELECT SCOPE_IDENTITY() AS officeTypeID;
        RETURN;
    END

    IF @Action = 'UPDATE'
    BEGIN
        UPDATE Master_OfficeType
        SET officeTypeName = @officeTypeName,
            isActive = ISNULL(@isActive, isActive)
        WHERE officeTypeID = @officeTypeID;
        SELECT 1 AS Updated;
        RETURN;
    END

    IF @Action = 'GET_BY_ID'
    BEGIN
        SELECT * FROM Master_OfficeType WHERE officeTypeID = @officeTypeID;
        RETURN;
    END

    IF @Action = 'GET_ALL'
    BEGIN
        SELECT COUNT(*) AS TotalCount FROM Master_OfficeType WHERE isActive = 'Y';
        SELECT * FROM Master_OfficeType WHERE isActive = 'Y'
        ORDER BY officeTypeName
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
        RETURN;
    END

    IF @Action = 'SEARCH'
    BEGIN
        SELECT * FROM Master_OfficeType
        WHERE isActive = 'Y'
          AND (@searchText IS NULL OR officeTypeName LIKE '%' + @searchText + '%')
        ORDER BY officeTypeName;
        RETURN;
    END

    IF @Action = 'DELETE'
    BEGIN
        UPDATE Master_OfficeType SET isActive = 'N' WHERE officeTypeID = @officeTypeID;
        SELECT 1 AS Deleted;
        RETURN;
    END
END
GO

PRINT 'usp_MasterOfficeType_CRUD fixed: isActive now uses char Y/N instead of int 1/0';
GO
