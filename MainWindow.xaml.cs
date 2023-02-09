using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Windows.Threading;
using System.Threading;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace CodeHalt
{
    public partial class MainWindow : Window
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CodeHalt\\";
        int isAdministrator = 0;
        public MainWindow()
        {
            InitializeComponent();
            log("CodeHalt started!");
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                log("Another instance of CodeHalt is already running!", true, true, 2);
                MessageBox.Show("Another instance of CodeHalt is already running!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                log("CodeHalt does not have admin rights!", true, true, 1);
            }
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                log("CodeHalt has admin rights!");
                this.Title = "CodeHalt (Administator)";
                isAdministrator = 1;
            }
            Task.Factory.StartNew(() =>
                    {
                        GenerateProccessFile();
                        ScanProcesses(null, null);
                        AddToStartMenu();
                    });
            log("CodeHalt pre-UI load complete!");
        }

        /// <summary>
        /// It checks if the shortcut exists, if it doesn't, it creates it
        /// </summary>
        /// <returns>
        /// The method is returning a boolean value.
        /// </returns>
        private bool AddToStartMenu()
        {
            // Check if the shortcut already exists
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + @"\Programs\CodeHalt.lnk"))
            {
                // If the shortcut exists, then log that it exists and return true
                log("Shortcut already exists!");
                return true;
            }

            // Log that the shortcut doesn't exist
            log("Shortcut doesn't exist, creating it...");
            object shStartMenu = (object)"StartMenu";
            WshShell shell = new WshShell();
            string shortcutAddress = (string)shell.SpecialFolders.Item(ref shStartMenu) + @"\Programs\CodeHalt.lnk";
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.WorkingDirectory = Environment.CurrentDirectory;
            shortcut.Description = "CodeHalt - A simple process manager";
            shortcut.TargetPath = Environment.CurrentDirectory + "\\CodeHalt.exe";
            shortcut.Save();

            // Check if the shortcut was created
            if (File.Exists(shortcutAddress))
            {
                // If the shortcut was created, then log that it was created and return true
                log("Shortcut created!");
                return true;
            }
            else
            {
                // If the shortcut wasn't created, then log that it failed and return false
                log("Shortcut creation failed!", true, true, 2);
                return false;
            }
        }

        /// <summary>
        /// Update the status bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            // The user has closed the window.  We need to wait for the threads to finish before we can exit.
            log("Waiting for threads to finish...");
            UpdateStatus("Waiting for threads to finish...");
            try
            {
                // Wait for all the threads to finish.  We use Task.WaitAll() so that we can catch any exceptions that may happen.
                Task.WaitAll();
            }
            catch (AggregateException ex)
            {
                foreach (Exception inner in ex.InnerExceptions)
                {
                    // If we catch an exception, log it.
                    log("Exception: " + inner.Message);
                }
            }
            // Log that the threads have finished
            UpdateStatus("Exiting...");
            log("Closing CodeHalt...");
            log("CodeHalt closed!");
            Environment.Exit(0);
        }

        /// <summary>
        /// It writes a message to a log file
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="newLine">If true, adds a new line to the end of the message.</param>
        /// <param name="timestamp">If true, it will add a timestamp to the log.</param>
        /// <param name="level">0 = info, 1 = warning, 2 = error</param>
        private void log(string message, bool newLine = true, bool timestamp = true, int level = 0)
        {
            Action logToFile = new Action(() =>
            {
                if (path == null)
                {
                    path = Environment.CurrentDirectory + "\\";
                }
                if (path[path.Length - 1] != '\\')
                {
                    path += "\\";
                }
                if (File.Exists(path + "log.txt"))
                {
                    using StreamWriter file = new(path + "log.txt", true);
                    if (timestamp)
                    {
                        file.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ");
                    }
                    switch (level)
                    {
                        case 0:
                            file.Write("[INFO] ");
                            break;
                        case 1:
                            file.Write("[WARNING] ");
                            break;
                        case 2:
                            file.Write("[ERROR] ");
                            break;
                    }
                    file.Write(message);
                    if (newLine)
                    {
                        file.WriteLine();
                    }
                    file.Close();
                }
                else
                {
                    using StreamWriter file = new(path + "log.txt");
                    if (timestamp)
                    {
                        file.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ");
                    }
                    switch (level)
                    {
                        case 0:
                            file.Write("[INFO] ");
                            break;
                        case 1:
                            file.Write("[WARNING] ");
                            break;
                        case 2:
                            file.Write("[ERROR] ");
                            break;
                    }
                    file.Write(message);
                    if (newLine)
                    {
                        file.WriteLine();
                    }
                    file.Close();
                }
            });
            this.Dispatcher.Invoke(logToFile, DispatcherPriority.Background);
        }

        private bool IsFileLocked(Stream baseStream)
        {
            try
            {
                baseStream.ReadByte();
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>
        /// It creates a file called processes.txt in the appdata folder if it doesn't exist
        /// </summary>
        private void GenerateProccessFile()
        {
            Task.Factory.StartNew(() =>
            {
                if (!System.IO.File.Exists(path + "processes.txt"))
                {
                    log("Generating processes file...");
                    UpdateStatus("Generating processes file...");
                    string[] processes = { "code", "chrome", "firefox", "node", "sublime_text", "devenv", "laragon" };
                    System.IO.Directory.CreateDirectory(path);
                    using StreamWriter file = new(path + "processes.txt");
                    UpdateStatus("Writing processes to file...");
                    foreach (string process in processes)
                    {
                        log("Writing " + process + " to file...");
                        UpdateStatus("Writing " + process + " to file...");
                        file.WriteLine(process);
                    }
                    log("Created processes file!");
                    UpdateStatus("Generated processes file!");
                }
            });
        }

        /// <summary>
        /// It scans the processes.txt file for processes, and if it finds one, it adds it to the list
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">The event arguments</param>
        private void ScanProcesses(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Have it show the loading animation in the navbar where the search bar is
                this.Title = "CodeHalt" + (isAdministrator == 1 ? " (Administator)" : "") + " - Scanning...";
                log("Scanning processes...");
                UpdateStatus("Scanning processes...");

                // Clear list
                ProcessList.Items.Clear();

                // Get processes from file
                string[] processes = System.IO.File.ReadAllLines(path + "processes.txt");

                Process[] runningProcesses = Process.GetProcesses();

                for (int i = 0; i < processes.Length; i++)
                {
                    processes[i] = processes[i].ToLower();
                }

                log("Found " + runningProcesses.Length + " processes...");
                UpdateStatus("Found " + runningProcesses.Length + " processes...");
                log("Adding processes to list...");
                UpdateStatus("Adding processes to list...");
                int c = 0;
                int total = runningProcesses.Length;
                foreach (Process runningProcess in runningProcesses)
                {
                    c++;
                    if (processes.Contains(runningProcess.ProcessName.ToLower()))
                    {
                        UpdateStatus("Adding " + runningProcess.ProcessName + " to list...");
                        ProcessList.Items.Add(runningProcess.ProcessName + " (" + runningProcess.Id + ")" + " - " + runningProcess.MainWindowTitle);
                    }
                }
                if (ProcessList.Items.Count == 0)
                {
                    UpdateStatus("No processes found!");
                    log("No processes found!");
                }
                else
                {
                    UpdateStatus("Found " + ProcessList.Items.Count + " processes!");
                    log("Found " + ProcessList.Items.Count + " processes!");
                }
                this.Title = "CodeHalt" + (isAdministrator == 1 ? " (Administator)" : "");

            });

        }

        /// <summary>
        /// It reads a text file, gets all the processes running on the computer, and then kills the
        /// processes that are in the text file
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">The event arguments</param>
        private void StopProcesses(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                log("Stopping processes...");
                UpdateStatus("Stopping processes...");
                // Get processes from file
                string[] processes = System.IO.File.ReadAllLines(path + "processes.txt");
                processes = processes.Where(x => !x.StartsWith("#")).ToArray();
                processes = processes.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                for (int i = 0; i < processes.Length; i++)
                {
                    processes[i] = processes[i].ToLower();
                }
                Process[] runningProcesses = Process.GetProcesses();

                log("Found " + runningProcesses.Length + " processes...");
                UpdateStatus("Found " + runningProcesses.Length + " processes...");
                int totalProcesses = runningProcesses.Length;
                int currentProcess = 0;
                int failedProcesses = 0;
                string processName = "";

                // Add processes to list
                log("Stopping processes...");
                UpdateStatus("Stopping processes...");
                foreach (Process runningProcess in runningProcesses)
                {
                    currentProcess++;
                    if (processes.Contains(runningProcess.ProcessName.ToLower()))
                    {
                        log("Stopping " + runningProcess.ProcessName + "...");
                        UpdateStatus("Stopping " + runningProcess.ProcessName + "...");
                        processName = runningProcess.ProcessName + " (" + runningProcess.Id + ")" + " - " + runningProcess.MainWindowTitle;
                        try
                        {
                            runningProcess.Kill();
                        }
                        catch (Exception ex)
                        {
                            failedProcesses++;
                            log("Failed to stop '" + runningProcess.ProcessName + "'!", level: 2);
                            UpdateStatus("Failed to stop '" + runningProcess.ProcessName + "'!");
                        }
                        ProcessList.Items.Remove(processName);
                    }
                }
                if (failedProcesses > 0)
                {
                    log("Failed to stop " + failedProcesses + " processes!", level: 2);
                    UpdateStatus("Failed to stop " + failedProcesses + " processes!");
                }
                else
                {
                    log("Stopped processes!");
                    UpdateStatus("Stopped processes!");
                }
            });
        }

        /// <summary>
        /// It takes a string as an argument and updates the status label with the string
        /// </summary>
        /// <param name="status">The status to update the label with.</param>
        private void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Text = status;
            });
        }

        /// <summary>
        /// It opens the path in explorer
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">The event arguments.</param>
        private void OpenInExplorer(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                log("Opening path in explorer...");
                Process.Start("explorer.exe", path);
                log("Opened path in explorer!");
                UpdateStatus("Opened '" + path + "' in explorer!");
            });
        }

        /// <summary>
        /// If any of the processes running on the computer have the same name as the one passed in,
        /// return true
        /// </summary>
        /// <param name="name">The name of the process to check for.</param>
        /// <returns>
        /// A boolean value.
        /// </returns>
        private bool IsProcessRunning(string name)
        {
            return Process.GetProcesses().Any(x => x.ProcessName == name);
        }

        /// <summary>
        /// This function terminates the selected processes in the list view.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">This is the event that is being handled.</param>
        private void TerminateSelectedProcesses(object sender, RoutedEventArgs e)
        {
            // Run this on a new thread but have the same priority as the UI thread
            Action terminateProcessesSelected = new Action(() =>
            {
                var selectedProcesses = ProcessList.SelectedItems;
                if (selectedProcesses.Count == 0)
                {
                    log("No processes selected!");
                    UpdateStatus("No processes selected!");
                    return;
                }
                log("Terminating " + selectedProcesses.Count + " processes...");
                foreach (var selectedProcess in selectedProcesses)
                {
                    int processId;
                    try
                    {
                        processId = int.Parse(selectedProcess.ToString().Substring(selectedProcess.ToString().IndexOf('(') + 1, selectedProcess.ToString().IndexOf(')') - selectedProcess.ToString().IndexOf('(') - 1));
                    }
                    catch (FormatException ex)
                    {
                        UpdateStatus("Failed to parse process ID from string: " + ex.Message);
                        log("Failed to parse process ID from string: " + ex.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus("Failed to get process ID from string: " + ex.Message);
                        log("Failed to get process ID from string: " + ex.Message);
                        continue;
                    }
                    UpdateStatus("Stopping process " + processId + "...");
                    log("Stopping process " + processId + "...");
                    Process process = Process.GetProcessById(processId);
                    if (process == null)
                    {
                        UpdateStatus("Process " + processId + " does not exist!");
                        log("Process " + processId + " does not exist!");
                        continue;
                    }
                    else
                    {
                        try { process.Kill(); log("Stopped process " + processId + "!"); }
                        catch (Exception ex) { UpdateStatus("Failed to stop process " + processId + "!"); log("Failed to stop process " + processId + "!"); }
                    }
                }
                if (selectedProcesses.Count == 1)
                {
                    UpdateStatus("Stopped " + selectedProcesses.Count + " process!");
                    log("Stopped " + selectedProcesses.Count + " process!");
                }
                else
                {
                    UpdateStatus("Stopped " + selectedProcesses.Count + " processes!");
                    log("Stopped " + selectedProcesses.Count + " processes!");
                }
                ProcessList.Items.Clear();
                string[] processes = System.IO.File.ReadAllLines(path + "processes.txt");
                Process[] runningProcesses = Process.GetProcesses();
                for (int i = 0; i < processes.Length; i++) { processes[i] = processes[i].ToLower(); }
                foreach (Process runningProcess in runningProcesses)
                {
                    if (processes.Contains(runningProcess.ProcessName.ToLower()))
                    {
                        ProcessList.Items.Add(runningProcess.ProcessName + " (" + runningProcess.Id + ")" + " - " + runningProcess.MainWindowTitle);
                    }
                }
                log("Updated process list!");
            });
            this.Dispatcher.Invoke(terminateProcessesSelected, DispatcherPriority.Background);
        }
        private void ActiveMode(object sender, RoutedEventArgs e)
        {
            log("Switched to active mode!");
        }
        private void PassiveMode(object sender, RoutedEventArgs e)
        {
            log("Switched to passive mode!");
        }
    }
}
