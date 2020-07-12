using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace desktopapp
{
    /// <summary>
    /// Interaction logic for ScanPopUp.xaml
    /// </summary>
    public partial class ScanPopUp : Window
    {
        public ScanPopUp()
        {
            InitializeComponent();
        }

        private void SubmitGroup(object sender, RoutedEventArgs e)
        {
            // Check for empty fields
            if ((upperInput.Text == "")
             || (ipInput.Text == "")
             || (lowerInput.Text == ""))
            {
                errorMsg.Text = "ERROR: Please fill in all fields!";
                errorMsg.Foreground = new SolidColorBrush(Colors.Red);
                errorMsg.FontWeight = FontWeights.Bold;
                errorMsg.Visibility = Visibility.Visible;
                return;
            }

            int upper = -1;
            int lower = -1;

            if (Int32.TryParse(upperInput.Text, out upper))
            {
                if (upper < lower || upper > 255)
                {
                    // ERROR MESSAGE HERE

                    return;
                }
            }

            else if (Int32.TryParse(lowerInput.Text, out lower))
            {
                if (lower > upper || lower > 254)
                {
                    // ERROR MESSAGE HERE

                    return;
                }
            }
            else
            {
                // ERROR MESSAGE HERE
                return;
            }


        }

        private void CancelGroup(object sender, RoutedEventArgs e)
        {

        }


    }



}
