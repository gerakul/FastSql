# FastSql
Light-weight, fast and easy to use C# library to retrieve, write (including bulk insert) and copy data in Microsoft SQL Server databases. There is support asynchronous operations.

## Samples:

1. Retrieving  entities

  ```csharp
  // simple command
  var q1 = SimpleCommand.Compile("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40)
      .ExecuteQuery<Employee>(connStr).ToArray();

  // mapped command with anonymous type
  var a = new { CompanyID = 1, Age = 40 };
  var q3 = MappedCommand.Compile(a, "select * from Employee where CompanyID = @CompanyID and Age > @Age")
      .ExecuteQuery<Employee>(connStr, a).ToArray();
  ```  
  or asynchronously
  ```csharp
  // simple command
  var q1 = await SimpleCommand.Compile("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40)
      .ExecuteQueryAsync<Employee>(connStr).ToArray();

  // mapped command with anonymous type
  var a = new { CompanyID = 1, Age = 40 };
  var q3 = await MappedCommand.Compile(a, "select * from Employee where CompanyID = @CompanyID and Age > @Age")
      .ExecuteQueryAsync<Employee>(connStr, a).ToArray();
  ```  

2. Retrieving anonimous entities

  ```csharp
  var q1 = SimpleCommand.Compile("select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID")
      .ExecuteQueryAnonymous(new { Company = default(string), Emp = default(string) }, connStr);
  ```
  or asynchronously
  ```csharp
  var q1 = SimpleCommand.Compile("select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID")
      .ExecuteQueryAnonymousAsync(new { Company = default(string), Emp = default(string) }, connStr);
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
or asynchronously
  ```csharp
  Employee[] newEmployees = new Employee[] { 
    new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
    new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
    new Employee() { CompanyID = 2, Name = "New Employee1" }
  };
  
  await newEmployees.WriteToServerAsync(connStr, "Employee");
  ```

More samples contains in Samples project. 


