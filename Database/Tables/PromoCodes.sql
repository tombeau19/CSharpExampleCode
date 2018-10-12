CREATE TABLE dbo.PromoCodes (
    Id uniqueidentifier primary key,
    Code varchar (40),
    GroupName varchar (40),
    StartDate date, 
	EndDate date
);