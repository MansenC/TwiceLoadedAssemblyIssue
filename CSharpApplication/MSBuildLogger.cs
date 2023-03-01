using Microsoft.Build.Framework;

namespace Example
{
    internal class MSBuildLogger : ILogger
    {
        public string Parameters { get; set; }

        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

        private IEventSource _eventSource;

        public void OnErrorRaised(object sender, BuildErrorEventArgs args)
        {
            Console.WriteLine($"MSB ERROR: {args.Message} @{args.File}:{args.LineNumber}");
        }

        public void OnWarningRaised(object sender, BuildWarningEventArgs args)
        {
            Console.WriteLine($"MSB WARNG: {args.Message} @{args.File}:{args.LineNumber}");
        }

        public void Initialize(IEventSource eventSource)
        {
            _eventSource = eventSource;
            _eventSource.ErrorRaised += OnErrorRaised;
            _eventSource.WarningRaised += OnWarningRaised;
        }

        public void Shutdown()
        {
            if (_eventSource == null)
            {
                return;
            }

            _eventSource.ErrorRaised -= OnErrorRaised;
            _eventSource.WarningRaised -= OnWarningRaised;
            _eventSource = null;
        }
    }
}
