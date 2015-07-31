using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples
{
  public class Employee
  {
    public int ID { get; set; }
    public int CompanyID { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public int? Age { get; set; }
    public DateTime? StartWorking { get; set; }
  }
}
