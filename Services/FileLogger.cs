using System;
using System.IO;

namespace FRCM
{
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;

        public FileLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
        }

        public void Log(string message)
        {
            try
            {
                File.AppendAllText(_logFilePath, $"{DateTime.Now}: {message}\n");
            }
            catch (Exception ex)
            {
                // fallback to console
                Console.WriteLine($"Error writing to log file: {ex.Message} - Original message: {message}");
            }
        }
    }
}
