using Diginsight.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient;

public class FeatureFlagOptions : IDynamicallyConfigurable
{
    public bool DeviceDeleteHierarchyEnabled { get; set; }
}
