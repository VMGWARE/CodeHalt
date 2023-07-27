using System;
using System.Windows;
using System.Windows.Input;

namespace CodeHalt
{
    /// <summary>
    /// Interaction logic for EditWindow.xaml
    /// </summary>
    public partial class EditWindow : Window
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CodeHalt\\";
        Log Log = new Log(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CodeHalt\\");

        public EditWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            string processes = System.IO.File.ReadAllText(path + "processes.txt");
            ProcessTextBox.Text = processes;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Log
            Log.Info("Updating processes.txt");

            // Update processes.txt
            System.IO.File.WriteAllText(path + "processes.txt", ProcessTextBox.Text);

            // Validate processes.txt
            if (System.IO.File.ReadAllText(path + "processes.txt") == ProcessTextBox.Text)
            {
                Log.Info("Successfully updated processes.txt");
            }
            else
            {
                Log.Error("Failed to update processes.txt");
            }

            // Exit
            Close();
        }

        private void ProcessTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Add new line on Enter key press
                ProcessTextBox.Text += Environment.NewLine;

                // Move caret to new line
                ProcessTextBox.CaretIndex = ProcessTextBox.Text.Length;
            }
        }
    }
}
