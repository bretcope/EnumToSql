using System;
using System.IO;
using EnumToSql.Logging;

namespace EnumToSql
{
    /// <summary>
    /// Helper class for logging data from EnumToSql methods.
    /// </summary>
    public class Logger : IDisposable
    {
        readonly object _lock = new object();
        
        readonly TextWriter _stream;
        readonly ILogFormatter _formatter;
        readonly Logger _parent;

        int _nestedLevel;
        int _writeCount = 0;
        int _lastBlockOpenOrClose = -1;

        /// <summary>
        /// Instantiates a new logger for use with EnumToSql methods.
        /// </summary>
        /// <param name="stream">The stream where logging information will be sent.</param>
        /// <param name="formatter">Controls how log messages are formatted. If null, <see cref="LogFormatters.Plain"/> is used.</param>
        public Logger(TextWriter stream, ILogFormatter formatter = null)
        {
            _stream = stream;
            _formatter = formatter ?? LogFormatters.Plain;
            _nestedLevel = 0;
        }

        Logger(Logger parent)
        {
            _stream = new StringWriter();
            _formatter = parent._formatter;
            _parent = parent;
            _nestedLevel = parent._nestedLevel;
        }

        void IDisposable.Dispose()
        {
            if (_parent != null)
            {
                // child loggers need to flush to their parent when disposed
                var sw = (StringWriter)_stream;
                lock (_parent._lock)
                {
                    var parentWriteCount = _parent._writeCount;

                    _parent.WriteRaw(sw.GetStringBuilder().ToString());

                    _parent._writeCount = parentWriteCount + _writeCount;
                    if (_lastBlockOpenOrClose != -1)
                    {
                        var diff = _writeCount - _lastBlockOpenOrClose;
                        _parent._lastBlockOpenOrClose = _parent._writeCount - diff;
                    }
                }
            }
        }

        internal Logger CreateChildLogger()
        {
            return new Logger(this);
        }

        internal void Info(string message)
        {
            var text = _formatter.Message(_nestedLevel, Severity.Info, message);
            WriteRaw(text);
        }

        internal void Warning(string message)
        {
            var text = _formatter.Message(_nestedLevel, Severity.Warning, message);
            WriteRaw(text);
        }

        internal void Error(string message)
        {
            var text = _formatter.Message(_nestedLevel, Severity.Error, message);
            WriteRaw(text);
        }

        internal void Exception(Exception ex)
        {
            var typed = ex as EnumsToSqlException;
            if (typed?.IsLogged == true)
                return; // don't double-log the exception

            var stackTrace = ex.StackTrace;
            if (stackTrace == "")
                stackTrace = null;

            var text = _formatter.Message(_nestedLevel, Severity.Error, ex.Message, stackTrace);
            WriteRaw(text);

            if (typed != null)
                typed.IsLogged = true;
        }

        internal IDisposable OpenBlock(string name)
        {
            return new LoggerBlock(this, name);
        }

        int WriteRaw(string text)
        {
            lock (_lock)
            {
                _stream.Write(text);
                _writeCount++;
                return _writeCount;
            }
        }

        class LoggerBlock : IDisposable
        {
            readonly Logger _logger;
            readonly string _name;
            readonly int _nestedLevel;

            public LoggerBlock(Logger logger, string name)
            {
                _logger = logger;
                _name = name;
                _nestedLevel = logger._nestedLevel;

                var text = logger._formatter.OpenBlock(_nestedLevel, name);
                _logger._lastBlockOpenOrClose = _logger.WriteRaw(text);

                logger._nestedLevel = _nestedLevel + 1;
            }

            public void Dispose()
            {
                var lastActionWasBlockOpenOrClose = _logger._writeCount == _logger._lastBlockOpenOrClose;

                var text = _logger._formatter.CloseBlock(_nestedLevel, _name, lastActionWasBlockOpenOrClose);
                _logger._lastBlockOpenOrClose = _logger.WriteRaw(text);

                _logger._nestedLevel = _nestedLevel;
            }
        }
    }
}