using Diginsight.CAOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient
{
    public class AzureKeyVaultOptions : IDynamicallyPostConfigurable
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Uri { get; set; }
        public string RedirectUri { get; set; }
    }
    public class AzureAdOptions : IDynamicallyPostConfigurable
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Uri { get; set; }
        public string RedirectUri { get; set; }
    }

}
