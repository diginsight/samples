using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AuthenticationSampleClient;

public class GetNameConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var identity = value as ClaimsIdentity;
        if (identity == null) return null;

        var name = identity.Name;
        if (string.IsNullOrEmpty(name)) { name = identity?.Claims?.FirstOrDefault(c => c.Type == "name")?.Value; }
        return name;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}
