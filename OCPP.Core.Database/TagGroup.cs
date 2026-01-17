using System;
using System.Collections.Generic;

namespace OCPP.Core.Database
{
    public partial class TagGroup
    {
        public int TagGroupId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
