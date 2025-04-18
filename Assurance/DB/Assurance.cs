using ModKit.ORM;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assurance
{
    public class AssuranceOrm : ModEntity<AssuranceOrm>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int VehicleDbId { get; set; }
    }
}
