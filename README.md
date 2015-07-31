# FastSql
Light-weight, fast and easy to use C# library to retrieve, write (using bulk insert) and copy data in Microsoft SQL Server databases

## Samples:

1. Retrieving  entities

  ```csharp
  var q = SqlExecutor.ExecuteQuery<Employee>(connStr, 
    "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ToArray();
  ```  

2. Retrieving anonimous entities

  ```csharp
  var q = SqlExecutor.ExecuteQueryAnonymous(new { Company = default(string), Emp = default(string) }, 
    connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID").ToArray();
  ```

3. Bulk insert of entities to database

  ```csharp
  Employee[] newEmployees = new Employee[] { 
    new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
    new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
    new Employee() { CompanyID = 2, Name = "New Employee1" }
  };
  
  newEmployees.WriteToServer(connStr, "Employee");
  ```

More samples contains in Samples project. 


