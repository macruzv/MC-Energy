using System;
using System.Collections.Generic;

namespace OCPP.Core.Database
{
    public partial class Customer
    {
        public Customer()
        {
            ChargeTags = new HashSet<ChargeTag>();
        }

        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Identifier { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        public virtual ICollection<ChargeTag> ChargeTags { get; set; }
    }
}
