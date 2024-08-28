using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthenticationSampleBlazorappClient.Pages
{
    public partial class Home : ComponentBase
    {
        [Inject] protected ILogger<Home> logger { get; set; } = null!;

    }
}
