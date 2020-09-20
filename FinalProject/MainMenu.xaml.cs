using System.Windows;
using System.Windows.Controls;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for MainMenu.xaml
    /// </summary>
    public partial class MainMenu : Page
    {
        public MainMenu()
        {
            InitializeComponent();
        }

        private void BtnGoToSelectScanMenuClick(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef.ParentFrame.Navigate(new SelectPatientScansMenu());
        }

        private void BtnGoToSelectMultipleScansMenuClick(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef.ParentFrame.Navigate(new SelectMultipleScansMenu());
        }

        private void BtnGoToUpdateCurrentModelMenuClick(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef.ParentFrame.Navigate(new UpdateModelMenu(false));
        }

        private void BtnGoToUpdateToBestModelMenuClick(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef.ParentFrame.Navigate(new UpdateModelMenu(true));
            
        }
        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            if (!App.ParentWindowRef.ParentFrame.CanGoBack && !App.ParentWindowRef.ParentFrame.CanGoForward)
            {
                return;
            }

            var entry = App.ParentWindowRef.ParentFrame.RemoveBackEntry();
            while (entry != null)
            {
                entry = App.ParentWindowRef.ParentFrame.RemoveBackEntry();
            }
        }
    }
}