using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AuthenticationSampleBlazorappClient.Pages
{
    public partial class Weather : ComponentBase
    {
        [Inject] protected ILogger<Weather> logger { get; set; } = null!;
        [Inject] HttpClient Http { get; set; } = null!;

        private WeatherForecast[]? forecasts;

        protected override async Task OnInitializedAsync()
        {
            logger.LogDebug("OnInitializedAsync START");

            forecasts = await Http.GetFromJsonAsync<WeatherForecast[]>("sample-data/weather.json");
        }

        public class WeatherForecast
        {
            public DateOnly Date { get; set; }

            public int TemperatureC { get; set; }

            public string? Summary { get; set; }

            public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
        }

    }
}
