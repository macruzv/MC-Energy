using System;
using System.Collections.Generic;

namespace OCPP.Core.Database
{
    public partial class User
    {
        public User()
        {
            UserRoles = new HashSet<UserRole>();
            UserChargePoints = new HashSet<UserChargePoint>();
        }

        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreateDateTime { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; }
        public virtual ICollection<UserChargePoint> UserChargePoints { get; set; }
    }
}
