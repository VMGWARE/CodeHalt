using System;
using System.IO;
using System.Windows.Threading;

public class Log
{
    private string path;

    /// <summary>
    /// Constructor for the Log class.
    /// </summary>
    public Log(string logFilePath = null)
    {
        if (logFilePath == null)
        {
            path = Environment.CurrentDirectory + "\\";
        }
        else
        {
            path = logFilePath;
        }
        if (path[path.Length - 1] != '\\')
        {
            path += "\\";
        }
    }

    /// <summary>
    /// Private helper method to write the log message to the log file.
    /// </summary>
    private void LogToFile(string message, bool newLine = true, bool timestamp = true, string logLevel = "")
    {
        if (File.Exists(path + "log.txt"))
        {
            using StreamWriter file = new(path + "log.txt", true);
            if (timestamp)
            {
                file.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ");
            }
            file.Write(logLevel);
            file.Write(message);
            if (newLine)
            {
                file.WriteLine();
            }
        }
        else
        {
            using StreamWriter file = new(path + "log.txt");
            if (timestamp)
            {
                file.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ");
            }
            file.Write(logLevel);
            file.Write(message);
            if (newLine)
            {
                file.WriteLine();
            }
        }
    }

    /// <summary>
    /// Logs an informational message to the log file.
    /// </summary>
    public void Info(string message, bool newLine = true, bool timestamp = true)
    {
        LogToFile(message, newLine, timestamp, "[INFO] ");
    }

    /// <summary>
    /// Logs a warning message to the log file.
    /// </summary>
    public void Warning(string message, bool newLine = true, bool timestamp = true)
    {
        LogToFile(message, newLine, timestamp, "[WARNING] ");
    }

    /// <summary>
    /// Logs an error message to the log file.
    /// </summary>
    public void Error(string message, bool newLine = true, bool timestamp = true)
    {
        LogToFile(message, newLine, timestamp, "[ERROR] ");
    }

    /// <summary>
    /// Logs a debug message to the log file.
    /// </summary>
    public void Debug(string message, bool newLine = true, bool timestamp = true)
    {
        LogToFile(message, newLine, timestamp, "[DEBUG] ");
    }

    /// <summary>
    /// Logs a fatal message to the log file.
    /// </summary>
    public void Fatal(string message, bool newLine = true, bool timestamp = true)
    {
        LogToFile(message, newLine, timestamp, "[FATAL] ");
    }

    /// <summary>
    /// Logs a status message to the log file.
    /// </summary>
    public void Status(string message, bool newLine = true, bool timestamp = true)
    {
        LogToFile(message, newLine, timestamp, "[STATUS] ");
    }
}
