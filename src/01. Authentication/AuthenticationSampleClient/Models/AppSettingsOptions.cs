using Diginsight.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient;

public class AppSettingsOptions : IDynamicallyConfigurable
{
    public bool PermissionCheckEnabled { get; set; } = true;
}
