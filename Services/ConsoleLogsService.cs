using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Services
{
    /// <summary>
    /// Service for managing Unity console logs
    /// </summary>
    public class ConsoleLogsService : IConsoleLogsService
    {
        // Structure to store log information
        private class LogEntry
        {
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public LogType Type { get; set; }
            public DateTime Timestamp { get; set; }
        }
        
        // Collection to store all log messages
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ConsoleLogsService()
        {
            StartListening();
        }
        
        /// <summary>
        /// Start listening for logs
        /// </summary>
        public void StartListening()
        {
            // Register for log messages
            Application.logMessageReceived += OnLogMessageReceived;

#if UNITY_6000_0_OR_NEWER
            // Unity 6 specific implementation
            ConsoleWindowUtility.consoleLogsChanged += OnConsoleCountChanged;
#else
            // Unity 2022.3 implementation using reflection
            EditorApplication.update += CheckConsoleClearViaReflection;
#endif
        }
        
        /// <summary>
        /// Stop listening for logs
        /// </summary>
        public void StopListening()
        {
            // Unregister from log messages
            Application.logMessageReceived -= OnLogMessageReceived;
            
#if UNITY_6000_0_OR_NEWER
            // Unity 6 specific implementation
            ConsoleWindowUtility.consoleLogsChanged -= OnConsoleCountChanged;
#else
            // Unity 2022.3 implementation using reflection
            EditorApplication.update -= CheckConsoleClearViaReflection;
#endif
        }
        
        /// <summary>
        /// Get all logs as a JSON array
        /// </summary>
        /// <returns>JArray containing all logs</returns>
        public JArray GetAllLogsAsJson()
        {
            // Convert log entries to a JSON array
            JArray logsArray = new JArray();
            
            lock (_logEntries)
            {
                foreach (var entry in _logEntries)
                {
                    logsArray.Add(new JObject
                    {
                        ["message"] = entry.Message,
                        ["stackTrace"] = entry.StackTrace,
                        ["type"] = entry.Type.ToString(),
                        ["timestamp"] = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    });
                }
            }
            
            return logsArray;
        }
        
        /// <summary>
        /// Clear all stored logs
        /// </summary>
        private void ClearLogs()
        {
            lock (_logEntries)
            {
                _logEntries.Clear();
            }
        }
        
        /// <summary>
        /// Check if console was cleared using reflection (for Unity 2022.3)
        /// </summary>
        private void CheckConsoleClearViaReflection()
        {
            try
            {
                // Get current log counts using LogEntries (internal Unity API)
                var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntriesType == null) return;
                
                var getCountMethod = logEntriesType.GetMethod("GetCount",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (getCountMethod == null) return;
                
                int currentTotalCount = (int)getCountMethod.Invoke(null, null);
                        
                // If we had logs before, but now we don't, console was likely cleared
                if (currentTotalCount == 0 && _logEntries.Count > 0)
                {
                    ClearLogs();
                }
            }
            catch (Exception ex)
            {
                // Just log the error but don't break functionality
                Debug.LogError($"[MCP Unity] Error checking console clear: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Callback for when a log message is received
        /// </summary>
        /// <param name="logString">The log message</param>
        /// <param name="stackTrace">The stack trace</param>
        /// <param name="type">The log type</param>
        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            // Add the log entry to our collection
            _logEntries.Add(new LogEntry
            {
                Message = logString,
                StackTrace = stackTrace,
                Type = type,
                Timestamp = DateTime.Now
            });
        }
        
#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Called when the console logs count changes
        /// </summary>
        private void OnConsoleCountChanged()
        {
            ConsoleWindowUtility.GetConsoleLogCounts(out int error, out int warning, out int log);
            if (error == 0 && warning == 0 && log == 0 && _logEntries.Count > 0)
            {
                ClearLogs();
            }
        }
#endif
    }
}
