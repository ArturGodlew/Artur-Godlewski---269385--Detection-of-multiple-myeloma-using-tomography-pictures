using System.Windows;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for ModelUpdatedWindow.xaml
    /// </summary>
    public partial class ModelUpdatedWindow : Window
    {
        public ModelUpdatedWindow()
        {
            InitializeComponent();
        }

        private void BtnOKClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
