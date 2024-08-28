using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthenticationSampleBlazorappClient.Pages
{
    public partial class Counter : ComponentBase
    {
        [Inject] protected ILogger<Counter> logger { get; set; } = null!;

        private int currentCount = 0;

        private void IncrementCount()
        {
            // using var scope = logger.BeginMethodScope();
            logger.LogDebug("IncrementCount START");

            IncrementCounterImpl();
        }


        public int IncrementCounterImpl()
        {
            logger.LogDebug("IncrementCounterImpl START");
            // using var scope = logger.BeginMethodScope();
            currentCount++;

            //scope.Result = currentCount;
            return currentCount;
        }
    }
}
