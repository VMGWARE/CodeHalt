using IWshRuntimeLibrary;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using File = System.IO.File;

namespace CodeHalt
{
    public partial class MainWindow : Window
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CodeHalt\\";
        int isAdministrator = 0;
        BackgroundWorker worker = new BackgroundWorker();
        public MainWindow()
        {
            // CodeHalt started
            log("CodeHalt started!", level: 5);
            // If another instance of CodeHalt is already running...
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                // Log this error
                log("Another instance of CodeHalt is already running!", true, true, 2);
                // Display an error message
                MessageBox.Show("Another instance of CodeHalt is already running!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Exit the process
                Environment.Exit(0);
            }
            // If CodeHalt doesn't have admin rights...
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Log this error
                log("CodeHalt does not have admin rights!", true, true, 1);
            }
            // If CodeHalt has admin rights...
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Log that CodeHalt has admin rights
                log("CodeHalt has admin rights!");
                // Change the title of the window to include "(Administrator)"
                this.Title = "CodeHalt  (Administrator)";
                // Set isAdministrator to 1
                isAdministrator = 1;
            }
            // Make the required folder if it doesn't exist
            MakeRequiredFolder();
            // Initialize the UI
            InitializeComponent();
            // Start a new thread
            Task.Factory.StartNew(() =>
                        {
                            // Generate a file containing all processes to be tracked
                            GenerateProcessFile();
                            // Scan the processes
                            ScanProcesses(null, null);
                            // Add CodeHalt to the start menu
                            AddToStartMenu();
                            // Log that CodeHalt UI load is complete
                            log("CodeHalt pre-UI load complete!", level: 5);
                        });
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
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + @"\Programs\CodeHalt\CodeHalt.lnk"))
            {
                // If the shortcut exists, then log that it exists and return true
                log("Shortcut already exists!");
                return true;
            }

            // Log that the shortcut doesn't exist
            log("Shortcut doesn't exist, creating it...");
            object shStartMenu = (object)"StartMenu";
            WshShell shell = new WshShell();
            string shortcutAddress = (string)shell.SpecialFolders.Item(ref shStartMenu) + @"\Programs\CodeHalt\CodeHalt.lnk";
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
        /// The function creates a folder and a file if they do not already exist.
        /// </summary>
        public void MakeRequiredFolder()
        {
            // Check if the folder exists
            if (!Directory.Exists(path))
            {
                // If the folder doesn't exist, then log that it doesn't exist
                log("Folder doesn't exist, creating it...");
                // Create the folder
                Directory.CreateDirectory(path);
                // Log that the folder was created
                log("Folder created!");
            }
        }

        /// <summary>
        /// Returns the current version of CodeHalt
        /// </summary>
        public string CurrentVersion
        {
            get
            {
                return $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()} (Build {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build})";
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
            if (worker.IsBusy == true)
            {
                log("Background worker is busy");
            }
            int tries = 0;
            while (worker.IsBusy)
            {
                StopBackgroundWorker();
                if (tries > 10)
                {
                    log("Background worker is still busy after 10 tries, exiting anyway");
                    break;
                }
                tries++;
            }
            if (worker.IsBusy == false)
            {
                log("Background worker is not busy");
            }
            // Log that the threads have finished
            UpdateStatus("Exiting...");
            log("Closing CodeHalt...");
            log("CodeHalt closed!", level: 5);
            Environment.Exit(0);
        }

        /// <summary>
        /// Catch any unhandled exceptions and log them
        /// </summary>
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            log("Unhandled exception: " + e.Exception.Message, true, true, 2);
            log("Stack trace: " + e.Exception.StackTrace, true, true, 2);
            log("CodeHalt crashed!", level: 2);
            Environment.Exit(0);
        }

        /// <summary>
        /// It writes a message to a log file
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="newLine">If true, adds a new line to the end of the message.</param>
        /// <param name="timestamp">If true, it will add a timestamp to the log.</param>
        /// <param name="level">0 = info, 1 = warning, 2 = error, 3 = debug, 4 = verbose, 5 = status</param>
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
                        case 3:
                            file.Write("[DEBUG] ");
                            break;
                        case 4:
                            file.Write("[FATAL] ");
                            break;
                        case 5:
                            file.Write("[STATUS] ");
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

        /// <summary>
        /// Checks if a file is locked
        /// </summary> 
        private bool IsFileLocked(Stream baseStream)
        {
            // If we can read the next byte, the stream is not closed 
            try
            {
                baseStream.ReadByte();
                return false;
            }
            // If we can't read the next byte, the stream is closed 
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>
        /// It creates a file called processes.txt in the app data folder if it doesn't exist
        /// </summary>
        private void GenerateProcessFile()
        {
            // If the processes.txt file doesn't exist, create it
            if (!System.IO.File.Exists(path + "processes.txt"))
            {
                // Log to the console what we're doing
                log("Generating processes file...");
                // Update the status label
                UpdateStatus("Generating processes file...");
                try
                {
                    // Create a string array that contains all the processes you want to kill
                    string[] processes = { "code", "chrome", "firefox", "node", "sublime_text", "devenv", "laragon" };
                    // Create the directory for the processes.txt file
                    System.IO.Directory.CreateDirectory(path);
                    // Create a new StreamWriter object to write to the processes.txt file
                    using StreamWriter file = new(path + "processes.txt");
                    // Log to the console that we're writing to the processes.txt file
                    UpdateStatus("Adding processes to file...");
                    // Loop through all the processes in the processes array
                    foreach (string process in processes)
                    {
                        // Log to the console what process we're writing to the file
                        log("Adding " + process + " to file...");
                        // Update the status label
                        UpdateStatus("Adding " + process + " to file...");
                        // Write the process name to the processes.txt file
                        file.WriteLine(process);
                    }
                    // Log to the console that we've written to the processes.txt file
                    log("Created processes file!");
                    // Update the status label
                    UpdateStatus("Created processes file!");
                    // Open the folder
                    OpenInExplorer(null, null);

                }
                catch (Exception e)
                {
                    // Log the error to the console
                    log("Error: " + e, level: 2);
                    // Update the status label
                    UpdateStatus("Error: " + e);
                }
            }
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
                this.Title = "CodeHalt" + (isAdministrator == 1 ? " (Administrator)" : "") + " - Scanning...";
                log("Scanning processes...");
                UpdateStatus("Scanning processes...");

                // Clear list
                ProcessList.Items.Clear();

                // Set variables
                string[] processes = null;

                // Get processes from file
                try { processes = System.IO.File.ReadAllLines(path + "processes.txt"); }
                catch (Exception ex)
                {
                    log("Failed to read processes.txt: " + ex.Message, level: 2);
                    UpdateStatus("Failed to read processes.txt: Check the log for more details!");
                    return;
                }

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
                    log("No processes found!", level: 1);
                }
                else
                {
                    UpdateStatus("Found " + ProcessList.Items.Count + " processes!");
                    log("Found " + ProcessList.Items.Count + " processes!");
                }
                this.Title = "CodeHalt" + (isAdministrator == 1 ? " (Administrator)" : "");

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
                    if (processes.Contains(runningProcess.ProcessName.ToLower()))
                    {
                        log("Stopping " + runningProcess.ProcessName + "...");
                        UpdateStatus("Stopping " + runningProcess.ProcessName + "...");
                        processName = runningProcess.ProcessName + " (" + runningProcess.Id + ")" + " - " + runningProcess.MainWindowTitle;
                        try
                        {
                            runningProcess.Kill();
                            log("Stopped '" + runningProcess.ProcessName + "'!");
                            currentProcess++;
                        }
                        catch (Exception)
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
            Action openpath = new Action(() =>
            {
                try
                {
                    log("Opening path in explorer...");
                    Process.Start("explorer.exe", path);
                    log("Opened path in explorer!");
                    UpdateStatus("Opened '" + path + "' in explorer!");
                }
                catch (Exception ex)
                {
                    log("Failed to open path in explorer: " + ex.Message, level: 2);
                    UpdateStatus("Failed to open '" + path + "' in explorer!");
                }
            });
            this.Dispatcher.Invoke(openpath, DispatcherPriority.Background);
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
                    log("No processes selected!", level: 1);
                    UpdateStatus("No processes selected!");
                    return;
                }
                log("Terminating " + selectedProcesses.Count + " processes...");
                int terminated = 0;
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
                        log("Failed to parse process ID from string: " + ex.Message, level: 1);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus("Failed to get process ID from string: " + ex.Message);
                        log("Failed to get process ID from string: " + ex.Message, level: 1);
                        continue;
                    }
                    UpdateStatus("Stopping process " + processId + "...");
                    log("Stopping process " + processId + "...");
                    Process process = Process.GetProcessById(processId);
                    if (process == null)
                    {
                        UpdateStatus("Process " + processId + " does not exist!");
                        log("Process " + processId + " does not exist!", level: 1);
                        continue;
                    }
                    else
                    {
                        try { process.Kill(); log("Stopped process " + processId + "!"); terminated++; }
                        catch (Exception) { UpdateStatus("Failed to stop process " + processId + "!"); log("Failed to stop process " + processId + "!", level: 1); }
                    }
                }
                if (terminated == 1)
                {
                    UpdateStatus("Stopped " + terminated + " process!");
                    log("Stopped " + terminated + " process!");
                }
                if (terminated == 0)
                {
                    UpdateStatus("Stopped " + terminated + " processes!");
                    log("Stopped " + terminated + " processes!");
                }
                else
                {
                    UpdateStatus("Stopped " + terminated + " processes!");
                    log("Stopped " + terminated + " processes!");
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

        /// <summary>
        /// It starts the background worker
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">The event data.</param>
        private void ActiveMode(object sender, RoutedEventArgs e)
        {
            log("Switched to active mode!");
            // Start the background worker
            StartBackgroundWorker();
            log("Started background worker!");
        }

        /// <summary>
        /// It stops the background worker
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">The event data.</param>
        private void PassiveMode(object sender, RoutedEventArgs e)
        {
            log("Switched to passive mode!");
            // Stop the background worker
            StopBackgroundWorker();
            log("Stopped background worker!");
        }

        /// <summary>
        /// It starts a background worker that runs the function BackgroundWorker_DoWork() in the
        /// background
        /// </summary>
        private void StartBackgroundWorker()
        {
            worker.DoWork += new DoWorkEventHandler(BackgroundWorker_DoWork);
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// It stops the background worker
        /// </summary>
        private void StopBackgroundWorker()
        {
            // Check if the background worker is running
            if (worker.IsBusy)
            {
                // Cancel the background worker
                worker.CancelAsync();
            }
        }

        /// <summary>
        /// It reads a text file, checks if any of the processes in the text file are running, and if
        /// they are, it kills them
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="DoWorkEventArgs">The event arguments for the DoWork event.</param>
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Boolean run = true;
            while (run)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    run = false;
                    break;
                }
                string[] processes = System.IO.File.ReadAllLines(path + "processes.txt");
                Process[] runningProcesses = Process.GetProcesses();
                for (int i = 0; i < processes.Length; i++) { processes[i] = processes[i].ToLower(); }
                foreach (Process runningProcess in runningProcesses)
                {
                    if (processes.Contains(runningProcess.ProcessName.ToLower()))
                    {
                        log("Stopping process " + runningProcess.Id + "...");
                        UpdateStatus("Stopping process " + runningProcess.Id + "...");
                        try { runningProcess.Kill(); log("Stopped process " + runningProcess.Id + "!"); UpdateStatus("Stopped process " + runningProcess.Id + "!"); }
                        catch (Exception) { UpdateStatus("Failed to stop process " + runningProcess.Id + "!"); log("Failed to stop process " + runningProcess.Id + "!", level: 1); }
                    }
                }
                Thread.Sleep(10000);
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void OpenEditWindow(object sender, RoutedEventArgs e)
        {
            EditWindow editWin = new EditWindow();
            editWin.ShowDialog();
        }
    }
}
