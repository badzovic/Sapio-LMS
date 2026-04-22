using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIA.Sapio.LMS.Data.Entities
{
    public class RegistrationToken
    {
        public int ID { get; set; }
        public string TOKEN { get; set; } = string.Empty;
        public DateTime CREATED_AT { get; set; }
        public DateTime EXPIRES_AT { get; set; }
        public string IS_ACTIVE { get; set; } = "Y";
        public int USED_COUNT { get; set; }
        public string? CREATED_BY_USER_ID { get; set; }
        public DateTime? DEACTIVATED_AT { get; set; }
        public string? DEACTIVATED_BY_USER_ID { get; set; }
        public string? NOTE { get; set; }
    }
}
