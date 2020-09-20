using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for SelectMultipleScansMenu.xaml
    /// </summary>
    public partial class SelectMultipleScansMenu : Page
    {
        private List<string> positivePaths = new List<string>();
        
        BackgroundWorker startScript = new BackgroundWorker();

        private List<string> negativePaths = new List<string>();

        public SelectMultipleScansMenu()
        {
            InitializeComponent();
            TextBlockInfo.Text = "Select folders containing scans of only ONE patient.\nAll files inside folders will be tested if they are DICOM file.\nHaving proper extention .DMC is not necessary.";
        }

        private void BtnSelectFolderClick(object sender, RoutedEventArgs e)
        {
            var window = new CommonOpenFileDialog();
            window.IsFolderPicker = true;
            window.Multiselect = true;
            var result = window.ShowDialog();

            if(result != CommonFileDialogResult.Cancel )
            {
                if(((Button)sender).Name == "SelectPositives")
                {
                    positivePaths = window.FileNames.ToList();
                    TextBoxPathPositives.Text = String.Join("\n", positivePaths);
                }
                else
                {
                    negativePaths = window.FileNames.ToList();
                    TextBoxPathNegatives.Text = String.Join("\n", negativePaths);
                }
            }
        }

        private void BtnContinueClick(object sender, RoutedEventArgs e)
        {
            TextBlockInfo.Text = "Images are being added to the database.\nDepending on number of folders this can take from few seconds to few hours\nPlease wait.";
            VBoxInfo.SetValue(Grid.RowSpanProperty, 4);
            GrpBoxNegative.Visibility = Visibility.Hidden;
            GrpBoxPositive.Visibility = Visibility.Hidden;
            BtnCancel.Visibility = Visibility.Hidden;
            BtnContinue.Visibility = Visibility.Hidden;

            startScript.DoWork += StartScript;
            startScript.RunWorkerAsync();

            this.UpdateLayout();
        }

        private void StartScript(object sender, DoWorkEventArgs e)
        {
            var selectedClassPaths = positivePaths.Select(x => ("positive", x)).Concat(negativePaths.Select(x => ("negative", x)));

            var pathToScript = Path.GetFullPath(ConfigurationManager.AppSettings["classifyScriptPath"]);

            var databasePath = Path.GetFullPath(ConfigurationManager.AppSettings["databasePath"]);
            foreach (var classPath in selectedClassPaths.ToList())
            {

                var pyProcess = PythonProcessHelper.GetProcess(pathToScript, $"\"{classPath.Item2}\" \"{databasePath}\" {classPath.Item1}", this);
                pyProcess.WaitForExit();
                var errorMessage = pyProcess.StandardError.ReadToEnd();
                if (errorMessage.Count() != 0)
                {
                    var errorWindow = new ErrorWindow(errorMessage, this);
                    errorWindow.ShowDialog();
                    this.Dispatcher.BeginInvoke((Action)(() => App.ParentWindowRef.ParentFrame.Navigate(errorWindow.TargetPage)));
                    return;
                }
            }

            this.Dispatcher.BeginInvoke((Action)(() => App.ParentWindowRef.ParentFrame.Navigate(new MainMenu())));
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef.ParentFrame.Navigate(new MainMenu());
        }

        private void TextBoxPathTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = ((TextBox)sender);

            var result = textBox.Text.Split('\n').Where(x => Directory.Exists(x)).ToList();

            if (textBox.Name == "TextBoxPathNegatives")
            {
                negativePaths = result;
            }
            else
            {
                positivePaths = result;
            }
        }
    }
}
