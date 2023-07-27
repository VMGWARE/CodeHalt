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
        Log Log = new Log(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CodeHalt\\");
        int isAdministrator = 0;
        BackgroundWorker worker = new BackgroundWorker();
        public MainWindow()
        {
            // CodeHalt started
            Log.Status("CodeHalt started!");
            // If another instance of CodeHalt is already running...
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                // Log this error
                Log.Error("Another instance of CodeHalt is already running!");
                // Display an error message
                MessageBox.Show("Another instance of CodeHalt is already running!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Exit the process
                Environment.Exit(0);
            }
            // If CodeHalt doesn't have admin rights...
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Log this error
                Log.Warning("CodeHalt does not have admin rights!");
            }
            // If CodeHalt has admin rights...
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Log that CodeHalt has admin rights
                Log.Info("CodeHalt has admin rights!");
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
                            Log.Status("CodeHalt pre-UI load complete!");
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
                Log.Info("Shortcut already exists!");
                return true;
            }

            // Log that the shortcut doesn't exist
            Log.Info("Shortcut doesn't exist, creating it...");
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
                Log.Info("Shortcut created!");
                return true;
            }
            else
            {
                // If the shortcut wasn't created, then log that it failed and return false
                Log.Error("Shortcut creation failed!");
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
                Log.Info("Folder doesn't exist, creating it...");
                // Create the folder
                Directory.CreateDirectory(path);
                // Log that the folder was created
                Log.Info("Folder created!");
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
            Log.Info("Waiting for threads to finish...");
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
                Log.Info("Background worker is busy");
            }
            int tries = 0;
            while (worker.IsBusy)
            {
                StopBackgroundWorker();
                if (tries > 10)
                {
                    Log.Info("Background worker is still busy after 10 tries, exiting anyway");
                    break;
                }
                tries++;
            }
            if (worker.IsBusy == false)
            {
                Log.Info("Background worker is not busy");
            }
            // Log that the threads have finished
            UpdateStatus("Exiting...");
            Log.Info("Closing CodeHalt...");
            Log.Status("CodeHalt closed!");
            Environment.Exit(0);
        }

        /// <summary>
        /// Catch any unhandled exceptions and log them
        /// </summary>
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            log("Unhandled exception: " + e.Exception.Message, true, true, 2);
            log("Stack trace: " + e.Exception.StackTrace, true, true, 2);
            Log.Error("CodeHalt crashed!");
            Environment.Exit(0);
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
                Log.Info("Generating processes file...");
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
                        Log.Info("Adding " + process + " to file...");
                        // Update the status label
                        UpdateStatus("Adding " + process + " to file...");
                        // Write the process name to the processes.txt file
                        file.WriteLine(process);
                    }
                    // Log to the console that we've written to the processes.txt file
                    Log.Info("Created processes file!");
                    // Update the status label
                    UpdateStatus("Created processes file!");
                    // Open the folder
                    OpenInExplorer(null, null);

                }
                catch (Exception e)
                {
                    // Log the error to the console
                    Log.Error("Error: " + e);
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
                Log.Info("Scanning processes...");
                UpdateStatus("Scanning processes...");

                // Clear list
                ProcessList.Items.Clear();

                // Set variables
                string[] processes = null;

                // Get processes from file
                try { processes = System.IO.File.ReadAllLines(path + "processes.txt"); }
                catch (Exception ex)
                {
                    Log.Error("Failed to read processes.txt: " + ex.Message);
                    UpdateStatus("Failed to read processes.txt: Check the log for more details!");
                    return;
                }

                Process[] runningProcesses = Process.GetProcesses();

                for (int i = 0; i < processes.Length; i++)
                {
                    processes[i] = processes[i].ToLower();
                }

                Log.Info("Found " + runningProcesses.Length + " processes...");
                UpdateStatus("Found " + runningProcesses.Length + " processes...");
                Log.Info("Adding processes to list...");
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
                    Log.Warning("No processes found!");
                }
                else
                {
                    UpdateStatus("Found " + ProcessList.Items.Count + " processes!");
                    Log.Info("Found " + ProcessList.Items.Count + " processes!");
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
                Log.Info("Stopping processes...");
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

                Log.Info("Found " + runningProcesses.Length + " processes...");
                UpdateStatus("Found " + runningProcesses.Length + " processes...");
                int totalProcesses = runningProcesses.Length;
                int currentProcess = 0;
                int failedProcesses = 0;
                string processName = "";

                // Add processes to list
                Log.Info("Stopping processes...");
                UpdateStatus("Stopping processes...");
                foreach (Process runningProcess in runningProcesses)
                {
                    if (processes.Contains(runningProcess.ProcessName.ToLower()))
                    {
                        Log.Info("Stopping " + runningProcess.ProcessName + "...");
                        UpdateStatus("Stopping " + runningProcess.ProcessName + "...");
                        processName = runningProcess.ProcessName + " (" + runningProcess.Id + ")" + " - " + runningProcess.MainWindowTitle;
                        try
                        {
                            runningProcess.Kill();
                            Log.Info("Stopped '" + runningProcess.ProcessName + "'!");
                            currentProcess++;
                        }
                        catch (Exception)
                        {
                            failedProcesses++;
                            Log.Error("Failed to stop '" + runningProcess.ProcessName + "'!");
                            UpdateStatus("Failed to stop '" + runningProcess.ProcessName + "'!");
                        }
                        ProcessList.Items.Remove(processName);
                    }
                }
                if (failedProcesses > 0)
                {
                    Log.Error("Failed to stop " + failedProcesses + " processes!");
                    UpdateStatus("Failed to stop " + failedProcesses + " processes!");
                }
                else
                {
                    Log.Info("Stopped processes!");
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
                    Log.Info("Opening path in explorer...");
                    Process.Start("explorer.exe", path);
                    Log.Info("Opened path in explorer!");
                    UpdateStatus("Opened '" + path + "' in explorer!");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to open path in explorer: " + ex.Message);
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
                    Log.Warning("No processes selected!");
                    UpdateStatus("No processes selected!");
                    return;
                }
                Log.Info("Terminating " + selectedProcesses.Count + " processes...");
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
                        Log.Warning("Failed to parse process ID from string: " + ex.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus("Failed to get process ID from string: " + ex.Message);
                        Log.Warning("Failed to get process ID from string: " + ex.Message);
                        continue;
                    }
                    UpdateStatus("Stopping process " + processId + "...");
                    Log.Info("Stopping process " + processId + "...");
                    Process process = Process.GetProcessById(processId);
                    if (process == null)
                    {
                        UpdateStatus("Process " + processId + " does not exist!");
                        Log.Warning("Process " + processId + " does not exist!");
                        continue;
                    }
                    else
                    {
                        try { process.Kill(); Log.Info("Stopped process " + processId + "!"); terminated++; }
                        catch (Exception) { UpdateStatus("Failed to stop process " + processId + "!"); Log.Warning("Failed to stop process " + processId + "!"); }
                    }
                }
                if (terminated == 1)
                {
                    UpdateStatus("Stopped " + terminated + " process!");
                    Log.Info("Stopped " + terminated + " process!");
                }
                else
                {
                    UpdateStatus("Stopped " + terminated + " processes!");
                    Log.Info("Stopped " + terminated + " processes!");
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
                Log.Info("Updated process list!");
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
            Log.Info("Switched to active mode!");
            // Start the background worker
            StartBackgroundWorker();
            Log.Info("Started background worker!");
        }

        /// <summary>
        /// It stops the background worker
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="RoutedEventArgs">The event data.</param>
        private void PassiveMode(object sender, RoutedEventArgs e)
        {
            Log.Info("Switched to passive mode!");
            // Stop the background worker
            StopBackgroundWorker();
            Log.Info("Stopped background worker!");
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
                        Log.Info("Stopping process " + runningProcess.Id + "...");
                        UpdateStatus("Stopping process " + runningProcess.Id + "...");
                        try { runningProcess.Kill(); Log.Info("Stopped process " + runningProcess.Id + "!"); UpdateStatus("Stopped process " + runningProcess.Id + "!"); }
                        catch (Exception) { UpdateStatus("Failed to stop process " + runningProcess.Id + "!"); Log.Warning("Failed to stop process " + runningProcess.Id + "!"); }
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
