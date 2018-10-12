CREATE TABLE dbo.PromoCodeUsers (
    Id uniqueidentifier primary key,
    UserID uniqueidentifier,
    Code varchar (40),
    FirstName nvarchar(64),
	LastName nvarchar(64),
    Email nvarchar(128),
    SubmissionDate datetime2(6)
);