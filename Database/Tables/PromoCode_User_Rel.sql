CREATE TABLE dbo.PromoCode_User_Rel
(
  Id uniqueidentifier primary key NOT NULL,
  UserKey uniqueidentifier  NOT NULL,
  CodeKey uniqueidentifier  NOT NULL,
)