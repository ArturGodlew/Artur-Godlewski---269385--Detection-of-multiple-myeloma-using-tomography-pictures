using System.Windows;

namespace FinalProject
{
    /// <summary>
    /// Interaction logic for ConfirmSaveResultWindow.xaml
    /// </summary>
    public partial class ConfirmSaveResultWindow : Window
    {       
        public ConfirmSaveResultWindow()
        {
            InitializeComponent();
            TextBlockInfo.Text = "Result will be saved to .csv file containing all the results that are used for evaluation model updating\n" +
                "You have final say over the result given by current model\n" +
                "You should only save results that you are completly sure about.";
        }

        private void BtnSavePositiveClick(object sender, RoutedEventArgs e)
        {          
            DialogResult = true;
            Close();
        }

        private void BtnSaveNegativeClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = null;
            Close();
        }
    }
}
