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
    /// Interaction logic for PopUp1.xaml
    /// </summary>
    public partial class PopUp1 : Window
    {
        public string NewName = "NaN";
        public PopUp1()
        {
            InitializeComponent();
        }
        private void SubmitGroup(object sender, RoutedEventArgs e)
        {
            if (nameInput.Text == "") nameInput.Text = "New Group";
            NewName = nameInput.Text;
            this.Close();
        }

        public string TheValue
        {
            get { return NewName; }
        }
    }

}
