namespace AuthenticationSampleBlazorappClient.Shared
{
    public class TraceLoggerConsole : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Console.WriteLine(state);
            return default;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null)
            {
                Console.WriteLine(state);
            }
            else
            {
                var message = formatter(state, exception);
                Console.WriteLine(message);
            }
        }
    }

    public class TraceLoggerConsoleProvider : ILoggerProvider
    {
        public TraceLoggerConsoleProvider() { }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TraceLoggerConsole();
            return logger;
        }
        public void Dispose()
        {
            ;
        }
    }
}
