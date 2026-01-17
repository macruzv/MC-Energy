using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database
{
    public class SystemSetting
    {
        [Key]
        public string SettingId { get; set; }
        public string Value { get; set; }
    }
}
