using Diginsight.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient;

public class AzureAdOptions : IDynamicallyConfigurable
{
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string Uri { get; set; }
    public string RedirectUri { get; set; }
}
