using Diginsight.CAOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSample
{
    public class FeatureFlagOptions : IDynamicallyPostConfigurable
    {
        public bool DeviceDeleteHierarchyEnabled { get; set; }
    }

}
