using System;
using Gerakul.FastSql.Tests.Shared;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    // the rows inserted by the seed scripts (same values as Examples/InitQuery.sql); new arrays on every call
    public static class SeedData
    {
        public static Company[] Companies => new[]
        {
            new Company() { ID = 1, Name = "Mega Co", DateOfFoundation = new DateTime(1990, 6, 8) },
            new Company() { ID = 2, Name = "Oldest Co", DateOfFoundation = new DateTime(1820, 8, 9) }
        };

        public static Employee[] Employees => new[]
        {
            new Employee() { ID = 1, CompanyID = 1, Name = "John Smith", Phone = "123-123-123", Age = 46, StartWorking = null },
            new Employee() { ID = 2, CompanyID = 1, Name = "Anton Ivanov", Phone = null, Age = null, StartWorking = new DateTime(2006, 1, 20) },
            new Employee() { ID = 3, CompanyID = 1, Name = "Engelbert Kaufmann", Phone = "222-333-444", Age = 23, StartWorking = new DateTime(2010, 1, 20) },
            new Employee() { ID = 4, CompanyID = 2, Name = "Ivan Antonov", Phone = "123-456-789", Age = 40, StartWorking = new DateTime(2008, 2, 22) },
            new Employee() { ID = 5, CompanyID = 2, Name = "Kate Donahue", Phone = "111-999-888", Age = 26, StartWorking = null }
        };
    }
}
