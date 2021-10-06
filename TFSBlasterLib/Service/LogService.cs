using NLog;
using System.Collections.Generic;

namespace TFSBlasterLib.Service
{
    internal class LogService
    {
        private readonly Dictionary<string, Logger> _loggers = new Dictionary<string, Logger>();

        public virtual void LogToConsole(string name, string message)
        {
            GetLogger(name).Info(message);
        }

        public virtual void LogDebug(string name, string message)
        {
            GetLogger(name).Debug(message);
        }

        public virtual void LogToFile(string name, string message)
        {
            GetLogger(name).Trace(message);
        }

        private Logger GetLogger(string name)
        {
            if (_loggers.ContainsKey(name))
            {
                return _loggers[name];
            }

            var logger = NLog.LogManager.GetLogger(name);
            _loggers.Add(name, logger);
            return logger;
        }
    }
}
