using Life;
using ModKit.ORM;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S.I_PolicePack
{
    public class ContraventionORM : ModEntity<ContraventionORM>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string Plaque { get; set; }
        public string PolicierName { get; set; }
        public DateTime Temps { get; set; }
        public bool Payer { get; set; }
    }
}
