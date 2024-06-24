using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Json2Csv;

internal class Observability
{
    public static readonly ActivitySource ActivitySource = new(Assembly.GetExecutingAssembly().GetName().Name!);
}
