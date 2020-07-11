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
using Renci.SshNet;
using SshNet;

using System.Windows.Diagnostics;
using System.Diagnostics;


namespace desktopapp
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PopUp2 : Window
    {
        public PopUp2()
        {
            InitializeComponent();
        }

        string user = MainWindow.username;
        string password = MainWindow.password;



        public bool confirm { get; set; }
        public SshClient pi { get; set; }
        public string stream { get; set; }

        public string ipAddr { get; set; }
        public string roomNum { get; set; }

        public string personName { get; set; }

        private void SubmitGroup(object sender, RoutedEventArgs e)
        {
            // Check for empty fields
            if ((nameInput.Text == "")
             || (ipInput.Text == "")
             || (roomInput.Text == ""))
            {
                errorMsg.Text = "ERROR: Please fill in all fields!";
                //errorMsg.Foreground = new SolidColorBrush(Colors.Red);
                errorMsg.FontWeight = FontWeights.Bold;
                errorMsg.Visibility = Visibility.Visible;
                return;
            }

            if (MainWindow.socketMap.ContainsKey(ipInput.Text))
            {
                errorMsg.Text = "ERROR: This IP is already connected!";
                errorMsg.FontWeight = FontWeights.Bold;
                errorMsg.Visibility = Visibility.Visible;
                return;
            }

            // Attempt connection
            try
            {
                SshClient client = new SshClient(ipInput.Text, user, password);
                client.Connect();
                pi = client;
                stream = "http://" + ipInput.Text + ":8080/?action=stream";
                roomNum = roomInput.Text;
                personName = nameInput.Text;

                ipAddr = ipInput.Text;
            }
            catch (Exception z)
            {
                errorMsg.Text = "ERROR: Connection failed, please check device ip and user/pw information";
                errorMsg.FontWeight = FontWeights.Bold;
                errorMsg.Visibility = Visibility.Visible;
                return;
            }    

            confirm = true;
            this.Close();
        }

        private void CancelGroup(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
