using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AuthenticationSampleBlazorappClient.Pages
{
    public partial class UserProfile : ComponentBase
    {
        [Inject] protected ILogger<UserProfile> logger { get; set; } = null!;
        
        [Inject] protected Microsoft.Graph.GraphServiceClient GraphServiceClient { get; set; } = null!;


        User? user;

        protected override async Task OnInitializedAsync()
        {
            logger.LogDebug("OnInitializedAsync START");

            try
            {
                user = await GraphServiceClient.Me.GetAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
