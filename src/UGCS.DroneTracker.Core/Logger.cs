using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using log4net;

namespace UGCS.DroneTracker.Core
{
    public interface IApplicationLogger
    {
        void LogException(Exception exception);
        void LogError(string message);
        void LogWarningMessage(string message);
        void LogInfoMessage(string message);
        void LogDebugMessage(string message);
    }


    public class DefaultApplicationLogger : IApplicationLogger
    {
        private readonly ILog _log = null;

        public DefaultApplicationLogger(Type loggerForType)
        {
            _log = LogManager.GetLogger(loggerForType);
        }

        public static IApplicationLogger GetLogger<T>()
        {
            return new DefaultApplicationLogger(typeof(T));
        }

        public void LogException(Exception exception)
        {
            if (_log.IsErrorEnabled)
                _log.Error(string.Format(CultureInfo.InvariantCulture, "{0}", exception.Message), exception);
        }
        public void LogError(string message)
        {
            if (_log.IsErrorEnabled)
                _log.Error(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }
        public void LogWarningMessage(string message)
        {
            if (_log.IsWarnEnabled)
                _log.Warn(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }
        public void LogInfoMessage(string message)
        {
            if (_log.IsInfoEnabled)
                _log.Info(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }

        public void LogDebugMessage(string message)
        {
            if (_log.IsDebugEnabled)
                _log.Debug(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }
    }
}
