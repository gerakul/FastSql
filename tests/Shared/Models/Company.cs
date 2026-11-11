using System;

namespace Gerakul.FastSql.Tests.Shared
{
    // same shape as the seeded Company table
    public class Company
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public DateTime DateOfFoundation { get; set; }
    }
}
