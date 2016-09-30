# FastSql
Light-weight, fast and easy to use C# library to retrieve, write (including bulk insert) and copy data in Microsoft SQL Server databases. There is support asynchronous operations.

## Samples:

1. Retrieving  entities

  ```csharp
  // simple command
  var q1 = SimpleCommand.ExecuteQuery<Employee>(connStr, "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ToArray();

  // mapped command
  var q2 = MappedCommand.ExecuteQuery<OtherEmployee, Employee>(connStr, "select * from Employee where CompanyID = @CompanyID and Age > @Age", 
   new OtherEmployee() { CompanyID = 1, Age = 40 }).ToArray();
  
  var q3 = MappedCommand.Prepare(connStr, "select * from Employee where CompanyID = @CompanyID and Age > @Age", 
    new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray();
  ```  
  or asynchronously
  ```csharp
  // simple command
  var q1 = await SimpleCommand.ExecuteQueryAsync<Employee>(connStr, "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ToArray();

  // mapped command
  var q2 = await MappedCommand.ExecuteQueryAsync<OtherEmployee, Employee>(connStr, "select * from Employee where CompanyID = @CompanyID and Age > @Age", 
   new OtherEmployee() { CompanyID = 1, Age = 40 }).ToArray();
  
  var q3 = await MappedCommand.Prepare(connStr, "select * from Employee where CompanyID = @CompanyID and Age > @Age", 
    new { CompanyID = 1, Age = 40 }).ExecuteQueryAsync<Employee>().ToArray();
```  

2. Retrieving anonimous entities

  ```csharp
  var proto = new { Company = default(string), Emp = default(string) };
  var q1 = SimpleCommand.ExecuteQueryAnonymous(proto, connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID").ToArray();
  ```
  or asynchronously
  ```csharp
  var proto = new { Company = default(string), Emp = default(string) };
  var q1 = await SimpleCommand.ExecuteQueryAnonymousAsync(proto, connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID").ToArray();
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
  

3. Insert and update entities

  ```csharp
  var emp = new Employee() { ID = 2, CompanyID = 1, Name = "New name", Phone = "111" };
  MappedCommand.Insert(connStr, "Employee", emp, "ID");
  MappedCommand.Update(connStr, "Employee", emp, "ID");
  ```
or asynchronously
  ```csharp
  var emp = new Employee() { ID = 2, CompanyID = 1, Name = "New name", Phone = "111" };
  await MappedCommand.InsertAsync(connStr, "Employee", emp, "ID");
  await MappedCommand.UpdateAsync(connStr, "Employee", emp, "ID");
  ```

More samples contains in Samples project. 


