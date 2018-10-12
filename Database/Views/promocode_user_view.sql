create view dbo.promocode_user_view as
select distinct 
p.id,
p.code,
p.firstname,
p.lastname,
p.email,
pc.groupname 
from promocode_user_rel pur join promocodeusers p on pur.userkey = p.id join promocodes pc on pc.id = pur.codekey where p.firstname is not null