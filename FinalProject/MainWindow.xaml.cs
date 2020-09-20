using System.Windows;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Height = (System.Windows.SystemParameters.PrimaryScreenHeight * 0.8);
            Width = (System.Windows.SystemParameters.PrimaryScreenWidth * 0.4);
            MinHeight = 300;
            MinWidth = 200;
            Application.Current.MainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            App.ParentWindowRef = this;
            this.ParentFrame.Navigate(new MainMenu());
        }
    }
}
