using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ONIModPack.Core
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
    
    public static class ModLogger
    {
        private static readonly string LogFileName = $"ONIModPack_{System.DateTime.Now:yyyyMMdd}.log";
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ONIModPack",
            "Logs"
        );

        private static readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private static System.Threading.Timer _flushTimer;
        private static readonly ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();
        private static bool _disposed;
        private static int _isFlushing = 0;
        private static LogLevel _currentLogLevel = LogLevel.Debug;
        private static bool _enableUnityLog = true;
        private static long _maxFileSizeBytes = 10L * 1024 * 1024;

        public static LogLevel CurrentLogLevel
        {
            get => _currentLogLevel;
            set => _currentLogLevel = value;
        }

        public static bool EnableUnityLog
        {
            get => _enableUnityLog;
            set => _enableUnityLog = value;
        }

        public static void Configure(LogLevel level, long maxFileSizeBytes)
        {
            _currentLogLevel = level;
            if (maxFileSizeBytes > 0)
            {
                _maxFileSizeBytes = maxFileSizeBytes;
            }
            Info($"[ModLogger] Configured: level={level}, maxFileSize={_maxFileSizeBytes / 1024 / 1024}MB");
        }

        static ModLogger()
        {
            InitializeLogger();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void InitializeLogger()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            _flushTimer = new System.Threading.Timer(FlushQueue, null, 5000, 5000);
        }
        
        public static void RegisterShutdownHandler()
        {
            try
            {
                UnityEngine.Application.quitting -= OnApplicationQuitting;
                UnityEngine.Application.quitting += OnApplicationQuitting;
            }
            catch
            {
            }
        }
        
        private static void OnApplicationQuitting()
        {
            Flush();
        }

        private static bool ShouldLog(LogLevel level)
        {
            return level >= _currentLogLevel;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Trace(string message) => WriteLog(LogLevel.Trace, message);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Debug(string message) => WriteLog(LogLevel.Debug, message);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Info(string message) => WriteLog(LogLevel.Info, message);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Warning(string message) => WriteLog(LogLevel.Warn, message);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Error(string message) => WriteLog(LogLevel.Error, message);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Fatal(string message) => WriteLog(LogLevel.Fatal, message);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Exception(System.Exception ex, string context = "")
        {
            WriteLog(LogLevel.Error, $"{context}: {ex}");
        }

        private static void WriteLog(LogLevel level, string message)
        {
            if (_disposed) return;
            if (!ShouldLog(level)) return;
            
            try
            {
                var levelStr = level.ToString().ToUpper();
                var logLine = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr,-7}] [{Thread.CurrentThread.ManagedThreadId,3}] {message}";

                if (_enableUnityLog)
                {
                    UnityEngine.Debug.Log(logLine);
                }

                _logQueue.Enqueue(logLine);
                
                if (level >= LogLevel.Error)
                {
                    FlushQueue(null);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    UnityEngine.Debug.LogError($"[ModLogger] Failed to write log: {ex.Message}");
                }
                catch
                {
                }
            }
        }

        private static void FlushQueue(object state)
        {
            if (_disposed) return;
            
            if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var linesToWrite = new System.Collections.Generic.List<string>();
                while (_logQueue.TryDequeue(out var line))
                {
                    linesToWrite.Add(line);
                }

                if (linesToWrite.Count == 0) return;

                var logPath = Path.Combine(LogDirectory, LogFileName);
                WriteToFile(logPath, linesToWrite);
            }
            catch (Exception ex)
            {
                try
                {
                    UnityEngine.Debug.LogError($"[ModLogger] Failed to flush log queue: {ex.Message}");
                }
                catch
                {
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isFlushing, 0);
            }
        }
        
        private static void WriteToFile(string path, System.Collections.Generic.List<string> lines)
        {
            _fileLock.EnterWriteLock();
            try
            {
                RotateIfNeeded(path);

                using (var writer = new StreamWriter(path, true))
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }

        private static void RotateIfNeeded(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < _maxFileSizeBytes) return;

                string archivePath = Path.Combine(
                    Path.GetDirectoryName(path) ?? LogDirectory,
                    $"{Path.GetFileNameWithoutExtension(path)}_{System.DateTime.Now:HHmmss}.log");

                int suffix = 1;
                while (File.Exists(archivePath))
                {
                    archivePath = Path.Combine(
                        Path.GetDirectoryName(path) ?? LogDirectory,
                        $"{Path.GetFileNameWithoutExtension(path)}_{System.DateTime.Now:HHmmss}_{suffix}.log");
                    suffix++;
                }

                File.Move(path, archivePath);
            }
            catch
            {
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Flush()
        {
            FlushQueue(null);
            
            int maxWait = 10;
            while (_logQueue.Count > 0 && maxWait > 0)
            {
                System.Threading.Thread.Sleep(10);
                FlushQueue(null);
                maxWait--;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Dispose()
        {
            _flushTimer?.Dispose();
            _flushTimer = null;
            
            Flush();
            
            _disposed = true;
            _fileLock?.Dispose();
        }
    }
    
    public static class Logger
    {
        public static void Trace(string message) => ModLogger.Trace(message);
        public static void Debug(string message) => ModLogger.Debug(message);
        public static void Info(string message) => ModLogger.Info(message);
        public static void Warning(string message) => ModLogger.Warning(message);
        public static void Error(string message) => ModLogger.Error(message);
        public static void Fatal(string message) => ModLogger.Fatal(message);
        public static void Exception(System.Exception ex, string context = "") => ModLogger.Exception(ex, context);
        public static void Flush() => ModLogger.Flush();
        public static void Dispose() => ModLogger.Dispose();
    }
}