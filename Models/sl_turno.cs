using System;
using System.Collections.Generic;

namespace smartlunch_api.Models
{
    public partial class sl_turno
    {
        public int id { get; set; }

        public string nombre { get; set; }          // nvarchar(50), not null
        public TimeSpan? horadesde { get; set; }    // time(0), null
        public TimeSpan? horahasta { get; set; }    // time(0), null

        public bool deletemark { get; set; }        // bit, not null

        public DateTime? createdate { get; set; }
        public string createuser { get; set; }
        public DateTime? updatedate { get; set; }
        public string updateuser { get; set; }

        public virtual ICollection<sl_menudd> menuesDelDia { get; set; }

        public sl_turno()
        {
            menuesDelDia = new HashSet<sl_menudd>();
        }
        
    }
}
