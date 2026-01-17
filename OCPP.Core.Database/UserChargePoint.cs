using System;

namespace OCPP.Core.Database
{
    public partial class UserChargePoint
    {
        public int UserId { get; set; }
        public string ChargePointId { get; set; }

        public virtual User User { get; set; }
        public virtual ChargePoint ChargePoint { get; set; }
    }
}
