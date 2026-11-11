-- PostgreSQL version of Examples/InitQuery.sql: the tables, rows and procedure used by the README examples.
-- Table and column names are quoted PascalCase, so the same C# models work on both providers
-- (the PostgreSQL text generator quotes column names).

create table "Company" (
    "ID" int not null primary key,
    "Name" varchar(50) not null,
    "DateOfFoundation" date not null
);

insert into "Company" ("ID", "Name", "DateOfFoundation") values (1, 'Mega Co', '1990-06-08');
insert into "Company" ("ID", "Name", "DateOfFoundation") values (2, 'Oldest Co', '1820-08-09');

create table "Employee" (
    "ID" serial primary key,
    "CompanyID" int not null,
    "Name" varchar(50) not null,
    "Phone" varchar(50) null,
    "Age" int null,
    "StartWorking" timestamp null
);

insert into "Employee" ("CompanyID", "Name", "Phone", "Age", "StartWorking") values (1, 'John Smith', '123-123-123', 46, null);
insert into "Employee" ("CompanyID", "Name", "Phone", "Age", "StartWorking") values (1, 'Anton Ivanov', null, null, '2006-01-20');
insert into "Employee" ("CompanyID", "Name", "Phone", "Age", "StartWorking") values (1, 'Engelbert Kaufmann', '222-333-444', 23, '2010-01-20');
insert into "Employee" ("CompanyID", "Name", "Phone", "Age", "StartWorking") values (2, 'Ivan Antonov', '123-456-789', 40, '2008-02-22');
insert into "Employee" ("CompanyID", "Name", "Phone", "Age", "StartWorking") values (2, 'Kate Donahue', '111-999-888', 26, null);

-- The stored procedure is a function. Npgsql 4 called it as select * from TestProc("CompanyID" => ..., "Age" => ...):
-- the function name is not quoted (folds to lower case), the parameter names are quoted. The body uses $1 and $2,
-- because the parameter names equal the column names. Npgsql 7+ runs CALL TestProc(...), which the server rejects for
-- a function; the tests pin that error (DatabaseFixture.CallTestProc).
create function TestProc("CompanyID" int, "Age" int) returns setof "Employee" language sql as $$
    select * from "Employee" where "CompanyID" = $1 and "Age" > $2
$$;
