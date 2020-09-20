using System.Configuration;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        public Page TargetPage { get; set; }

        public ErrorWindow(string errorMessage, object sender, string folderPath = null)
        {
            InitializeComponent();

            switch (errorMessage)
            {
                case "*DICOMFolderMissing*":
                    HandleFolderProblem(sender, folderPath, true);
                    break;
                case "*DICOMFolderEmpty*":
                    HandleFolderProblem(sender, folderPath, false);
                    break;
                case "*DatabaseMissing*":
                    HandleDatabaseProblem(sender, true);
                    break;
                case "*DatabaseEmpty*":
                    HandleDatabaseProblem(sender, false);
                    break;
                case "*ModelFileMissing*":
                    HandleMissingModel(sender);
                    break;
                case "*PythonScriptMissing*":
                    HandleMissingScript(sender);
                    break;
                case "*PythonMissing*":
                    HandlePythonMissing(sender);
                    break;
                default:
                    HandleOtherError(errorMessage);
                    break;
            }        
        }

        private void HandleFolderProblem(object sender, string folderPath, bool missing)
        {
            TxtBlockErrorInfo.Text = missing ? $"Folder:\n{folderPath}\ndoes not exist\n" : $"Folder:\n{folderPath}\ncontains no DICOM files\n";
            BtnConfirm.Content = "Go Back To Folder Selection";

            switch (sender)
            {
                case ResultMenu x:
                    TargetPage = new SelectPatientScansMenu();
                    break;
                case SelectMultipleScansMenu x:
                    TargetPage = new SelectMultipleScansMenu();
                    break;
                default:
                    TargetPage = new MainMenu();
                    break;
            }
        }

        private void HandleDatabaseProblem(object sender, bool missing)
        {
            var userMessage = $"Database is {(missing ? "missing" : "empty")}under path:\n{ConfigurationManager.AppSettings["databasePath"]}\n";
        
            BtnConfirm.Content = "Go Back To Result Menu";

            switch (sender)
            {
                case ResultMenu x:
                    BtnConfirm.Content = "Generate New Database";
                    TxtBlockErrorInfo.Text = userMessage + 
                        $"{(missing ? "New database will be generated, if this is not the right path it should be fixed in the config file" : "If this is not expected it should be investigated")}\n";
                    break;
                case SelectMultipleScansMenu x:
                    BtnConfirm.Content = "Generate New Database";
                    TxtBlockErrorInfo.Text = userMessage +
                        $"{(missing ? "New database will be generated, if this is not the right path it should be fixed in the config file" : "If this is not expected it should be investigated")}\n";
                    break;
                case UpdateModelMenu x:
                    BtnConfirm.Visibility = Visibility.Hidden;
                    BtnCancel.SetValue(Grid.ColumnProperty, 0);
                    BtnCancel.SetValue(Grid.ColumnSpanProperty, 2);
                    TxtBlockErrorInfo.Text = userMessage + 
                        $"{(missing ? "Add new scans to solve this or fix file path in the config file" : "If this is not expected it should be investigated")}\n";
                    break;
                default:
                    TargetPage = new MainMenu();
                    break;
            }
        }

        private void HandleMissingModel(object sender)
        {
            TxtBlockErrorInfo.Text = $"Crucial file containing metadata is missing under path: {ConfigurationManager.AppSettings["modelPath"]}\n" +
                $"Please create new model or provide valid path in the config file\n";

            BtnConfirm.Content = "Create New Model";
            
            TargetPage = new UpdateModelMenu(true);
        }

        private void HandleMissingScript(object sender)
        {
            string scriptPath; 
            if(typeof(UpdateModelMenu) == sender.GetType())
            {
                scriptPath = ConfigurationManager.AppSettings["updateScriptPath"];
            }
            else
            {
                scriptPath = ConfigurationManager.AppSettings["classifyScriptPath"];
            }

            TxtBlockErrorInfo.Text = $"Crucial file containing python script is missing under path:\n{scriptPath}\n" +
                $"Please provide valid path in the config file\n";

            BtnConfirm.Visibility = Visibility.Hidden;
            BtnCancel.SetValue(Grid.ColumnProperty, 0);
            BtnCancel.SetValue(Grid.ColumnSpanProperty, 2);
        }

        private void HandlePythonMissing(object sender)
        {
            TxtBlockHyperlink.Visibility = Visibility.Visible;
            TxtBlockErrorInfo.Text = $"Python executable is missing under path:\n{ConfigurationManager.AppSettings["pythonPath"]}\n" +
               $"Please provide valid path in the config file\n" +
               $"Python instalation might be required first\n";

            BtnConfirm.Visibility = Visibility.Hidden;
            BtnCancel.SetValue(Grid.ColumnProperty, 0);
            BtnCancel.SetValue(Grid.ColumnSpanProperty, 2);
        }

        private void HandleOtherError(string error)
        {
            TxtBlockErrorInfo.Text = $"Unknown error:\n{error}\n";

            BtnConfirm.Visibility = Visibility.Hidden;
            BtnCancel.SetValue(Grid.ColumnProperty, 0);
            BtnCancel.SetValue(Grid.ColumnSpanProperty, 2);
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {   
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            TargetPage = new MainMenu();
            Close();
        }

        private void HandleLinkClick(object sender, RoutedEventArgs e)
        {
            Hyperlink hl = (Hyperlink)sender;
            string navigateUri = hl.NavigateUri.ToString();
            Process.Start(new ProcessStartInfo(navigateUri));
            e.Handled = true;
        }
    }
}
