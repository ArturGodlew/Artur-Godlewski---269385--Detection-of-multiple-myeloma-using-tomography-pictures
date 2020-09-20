using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for SelectPatientScansWindow.xaml
    /// </summary>
    public partial class SelectPatientScansMenu : Page
    {
        public string selectedPath = "";
        BackgroundWorker loadNextPage = new BackgroundWorker();
        public SelectPatientScansMenu()
        {
            InitializeComponent();
            TextBlockInfo.Text = "Select folder containing scans of only ONE patient.\nAll files inside this folder will be tested if they are DICOM file.\nHaving proper extention .DMC is not necessary.";
            BtnContinue.IsEnabled = false;
        }

        private void BtnSelectFolderClick(object sender, RoutedEventArgs e)
        {
            var window = new FolderBrowserDialog();
            var result = window.ShowDialog();
            
            if(result != System.Windows.Forms.DialogResult.Cancel)
            {
                selectedPath = window.SelectedPath;
                TextBoxPath.Text = selectedPath;
            }            
        }

        private void BtnContinueClick(object sender, RoutedEventArgs e)
        {
            TextBlockInfo.Text = "Images are being classified.\nPlease wait up to few minutes.";
            TextBoxPath.Visibility = Visibility.Hidden;
            BtnCancel.Visibility = Visibility.Hidden;
            BtnContinue.Visibility = Visibility.Hidden;
            BtnSelectFolder.Visibility = Visibility.Hidden;

            loadNextPage.DoWork += LoadNextPage;
            loadNextPage.RunWorkerAsync();   
        }

        public void LoadNextPage(object sender, DoWorkEventArgs e)
        {
            this.Dispatcher.BeginInvoke((Action)(() => App.ParentWindowRef.ParentFrame.Navigate(new ResultMenu(selectedPath))));
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            BtnContinue.IsEnabled = true;
            App.ParentWindowRef.ParentFrame.Navigate(new MainMenu());
        }

        private void TextBoxPathChanged(object sender, RoutedEventArgs e)
        {
            if(TextBoxPath.Text == "")
            {
                BtnContinue.IsEnabled = false;
  
            }
            else
            {
                BtnContinue.IsEnabled = true;
                selectedPath = TextBoxPath.Text;
            }
        }
    }
}
