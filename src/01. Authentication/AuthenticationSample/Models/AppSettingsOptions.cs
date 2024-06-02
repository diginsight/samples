using Diginsight.CAOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSample
{
    public class AppSettingsOptions : IDynamicallyPostConfigurable
    {
        public bool PermissionCheckEnabled { get; set; } = true;
        public bool TraceRequestBody { get; set; }
        public bool TraceResponseBody { get; set; }
    }

}
