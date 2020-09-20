using System;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace FinalProject
{
    /// <summary>
    /// Interaction logic for UpdateCurrentModelMenu.xaml
    /// </summary>
    public partial class UpdateModelMenu : Page
    {
        private bool Recalculate { get; set; }
        private BackgroundWorker startScript = new BackgroundWorker();

        public UpdateModelMenu(bool recalculate)
        {
            InitializeComponent();
            Recalculate = recalculate;
            TxtBlockInfo.Text = recalculate ? "Are you sure?\nUpdate to best model can takes hours\n" : "Are you sure?\nUpdate of the model can take up to few minutes\n";
        }

        private void BtnContinueClick(object sender, RoutedEventArgs e)
        {
            BtnContinue.Visibility = Visibility.Hidden;
            BtnCancel.Visibility = Visibility.Hidden;
            TxtBlockInfo.Text = $"This can take few {(Recalculate ? "hours" : "minutes")}\nPlease wait";

            startScript.DoWork += StartScript;
            startScript.RunWorkerAsync();
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef.ParentFrame.Navigate(new MainMenu());
        }

        private void StartScript(object sender, DoWorkEventArgs e)
        {
            if (startScript.CancellationPending)
            {
                e.Cancel = true;
            }

            var args = $"\"{Path.GetFullPath(ConfigurationManager.AppSettings["metadataPath"])}\" \"{Path.GetFullPath(ConfigurationManager.AppSettings["databasePath"])}\" {(Recalculate? "recalculate" :"retrain")}";
            var pyProcess = PythonProcessHelper.GetProcess(ConfigurationManager.AppSettings["updateScriptPath"], args, this);
            
            if(pyProcess == null)
            {
                return;
            }

            pyProcess.WaitForExit();

            string outputReceived;
            while ((outputReceived = pyProcess.StandardOutput.ReadLine()) != null)
            {
                var window = new ErrorWindow(outputReceived, this);
                window.ShowDialog();
                this.Dispatcher.BeginInvoke((Action)(() => App.ParentWindowRef.ParentFrame.Navigate(window.TargetPage)));
                return;
            }

            if(PythonProcessHelper.ReadErrors(pyProcess, this))
            {
                return;
            }

            this.Dispatcher.BeginInvoke((Action)(() => new ModelUpdatedWindow().ShowDialog()));
            this.Dispatcher.BeginInvoke((Action)(() => App.ParentWindowRef.ParentFrame.Navigate(new MainMenu())));
        }
    }
}
