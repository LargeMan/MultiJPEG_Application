using MjpegProcessor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Renci.SshNet;
using SshNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
//using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.VisualBasic;
using System.Security;
using System.Security.Cryptography;
using System.Xml;
using System.Reflection;
using System.Windows.Automation;

namespace desktopapp
{
    // JSON objects; MODIFY THIS IF YOU MODIFY JSON STRUCTURE

    public class Datum
    {
        public string name { get; set; }
        public string num { get; set; }
        public string ip { get; set; }
        public string group { get; set; }
    }

    public class Module
    {
        public IList<Datum> data { get; set; }
        public IList<string> group { get; set; }
    }

    public class MJPGMaps
    {
        public Image img;
        public string ip;
    }

    public class SocketStuff
    {
        public TextBlock txt;
        public bool enabled;
        public Socket testSck;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODO: Change scope of some of these vars, most of it is unnecessary as global vars
        public static Dictionary<string, SocketStuff> socketMap = new Dictionary<string, SocketStuff>();
        private Dictionary<MjpegDecoder, MJPGMaps> mjpegs = new Dictionary<MjpegDecoder, MJPGMaps>();
        private Dictionary<string, WrapPanel> groupMap = new Dictionary<string, WrapPanel>();


        public static Module module;
        public List<Datum> failedConn = new List<Datum>();
        public string json;

        private IPHostEntry ipHostInfo;
        private IPAddress myIP;
        private string myIPString;
        private IPEndPoint localEndPoint;

        public static string username;
        public static string password;

        public Socket socket;




        //public const int PORT = 11111;
        public const int BUFFERSIZE = 1024;
        public byte[] buffer = new byte[BUFFERSIZE];
        public StringBuilder sb = new StringBuilder();

        public MainWindow()
        {
            InitialPopUp popup = new InitialPopUp();
            popup.ShowDialog();
            InitializeComponent();

            // retrieve username and password for ALL PI UNITS
            // TODO: Add message that tells user program is initializing
            // TODO: Add way to change these after the fact
            username = popup.userName;
            password = popup.passWord;

            // TESTING ONLY
            //socketMap.Add("192.168.1.137", StatusIndicator);

            /*
            Current plan:

            Initialize UI stuff based on JSON data

            Current json layout example:
            {
                "data" : [
                    { "name" : "meme", "num" : "2", "ip" : "192.181.1.131", group : "Default" },
                    { "name" : "lei", "num" : "3", "ip" : "10.0.0.17", group : "Default"}
                ],
                "group" : [ "Default", "test"]
            }
            */




            // IP STUFF

            ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());

            myIP = Array.FindLast(
                    Dns.GetHostEntry(string.Empty).AddressList,
                    a => a.AddressFamily == AddressFamily.InterNetwork);


            myIPString = myIP.ToString();

            Debug.WriteLine("ip is " + myIPString);
            localEndPoint = new IPEndPoint(myIP, 11111);
            socket = new Socket(myIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);


            // Start background server message receiving
            try
            {
                socket.Bind(localEndPoint);
                socket.Listen(100);

                Thread thread = new Thread(TestAlarm);
                thread.IsBackground = true;
                thread.Start();

                Debug.WriteLine("Server setup complete");
            }
            catch (Exception e)
            {
                Debug.WriteLine("ERROR RESTART");
            }


            if (File.Exists("config.json"))
            {
                using (StreamReader r = new StreamReader("config.json"))
                {
                    json = r.ReadToEnd();
                    module = JsonConvert.DeserializeObject<Module>(json);
                }

                foreach (string m in module.group) InitializeGroup(m);
                foreach (Datum m in module.data) InitializeModule(m);
            }
            else // create json and fill global variables
            {
                module = new Module();
                json = "{\"data\" : [],\"group\" : [ \"Default\"]}";
                File.WriteAllText("config.json", json);
                //CreateCam(this, null);
                // TODO: Implement some window that will create a module
            }

            // Initialize Modules
            


           // module = null; // deallocate it (probably better to change scope of var, but might need it later so idk)
        }



        //private void TestAlarm()
        //{

        //}





        
        // This runs in a background thread and only accepts one incoming message at a time
        // Since the pi's should rarely be sending messages the queue should never be overwhelmed
        // In theory, this is very vulnerable to attack, but the network should be private anyways
        private void TestAlarm()
        {
            try
            {

                while (true)
                {

                    Debug.WriteLine("Waiting for a connection..." + myIPString);
                    Socket handler = socket.Accept();
                    Thread thread = new Thread(() => ConnectedSocket(handler));
                    thread.IsBackground = true;
                    thread.Start();
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine("wtf");
                Debug.WriteLine(e);
            }

            // TODO: ADD ERROR SUPPRESSION SYSTEM
        } 

        private void ConnectedSocket(Socket handler)
        {
            try
            {
                string TERMINATOR = "<EOF>"; // <----------------- CHANGE THIS TO WHATEVER YOU WANT FOR THE PI SCRIPT MESSAGE ENDING

                while (true)
                {
                    // Buffer stuff
                    string data = null;
                    byte[] bytes = new byte[1024];

                    // Read bytes of message
                    while (true)
                    {
                        int bytesRec = handler.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf(TERMINATOR) > -1)
                        {
                            Debug.WriteLine("Message : " + data);
                            break;
                        }
                    }

                    // Debugging and sending message back to source as verification
                    Debug.WriteLine("Text received : {0}", data);

                    byte[] msg = Encoding.ASCII.GetBytes(data);

                    //handler.Send(msg);

                    // Parse IP and remove port number
                    string clientIP = handler.RemoteEndPoint.ToString();

                    // Remove port number, its unnecessary
                    int index = clientIP.LastIndexOf(":");
                    if (index > 0)
                        clientIP = clientIP.Substring(0, index);


                    Debug.WriteLine(clientIP);

                    // Check for error and if IP is valid
                    if (socketMap.ContainsKey(clientIP))
                    {
                        if (socketMap[clientIP].testSck == null)
                        {
                            socketMap[clientIP].testSck = handler;
                        }

                        if (!socketMap[clientIP].enabled) continue;

                        Debug.WriteLine("Made it past hashmap check...");
                        Debug.WriteLine(data);
                        // Checks if message is ALARM<EOF> or WARNING<EOF> (NOTE: NO SPACE BETWEEN TERMINATOR AND MESSAGE)
                        if (data == "ALARM" + TERMINATOR || data == "WARNING" + TERMINATOR)
                        {
                            Debug.WriteLine("Made it past message check...");
                            Debug.WriteLine(data);

                            // Change the corresponding status bar
                            this.Dispatcher.Invoke(() =>
                            {
                                // TODO: CHANGE THIS <-----------------------------------------------------------------
                                // Need to modify dictionary based on json format
                                var textref = socketMap[clientIP];

                                Button message = new Button();

                                if (data == "ALARM" + TERMINATOR)
                                {
                                    textref.txt.Text = "ALARM: CHECK PATIENT IMMEDIATELY";
                                    textref.txt.Background = Brushes.Red;
                                    message.Content = DateTime.UtcNow.ToString("MM-dd-yyyy");
                                    message.Content += " " + DateTime.Now.ToString("t");
                                    message.Content += (" ALARM AT:  ");
                                    // BEHOLD CRINGE

                                    Grid temp = ((Grid)((StackPanel)textref.txt.Parent)
                                                            .Children.OfType<Border>()
                                                            .FirstOrDefault()
                                                            .Child);
                                    message.Content +=  temp
                                                            .Children.OfType<TextBlock>()
                                                            .FirstOrDefault()
                                                            .Text;
                                    message.Background = Brushes.Red;
                                    messagelog.Children.Add(message);
                                }
                                else
                                {
                                    textref.txt.Text = "WARNING: Movement detected";
                                    textref.txt.Background = Brushes.Orange;
                                    message.Content = DateTime.UtcNow.ToString("MM-dd-yyyy");
                                    message.Content += " " + DateTime.Now.ToString("t");
                                    message.Content += (" WARNING AT:  ");
                                    // BEHOLD CRINGE
                                    Grid temp = ((Grid)((StackPanel)textref.txt.Parent)
                                                            .Children.OfType<Border>()
                                                            .FirstOrDefault()
                                                            .Child);
                                    message.Content += temp
                                                            .Children.OfType<TextBlock>()
                                                            .FirstOrDefault()
                                                            .Text;

                                    message.Background = Brushes.Orange;
                                    messagelog.Children.Add(message);
                                }

                                message.Click += (o, ev) =>
                                {
                                    messagelog.Children.Remove(message);
                                };

                                // Expand the group for visibility
                                try
                                {
                                    // Absolute cancer, but I don't know a nicer way of doing this
                                    // HEIRARCHY: textblock <- stackpanel <- groupbox <- wrappanel <- groupbox <- expander
                                    StackPanel A = (StackPanel)textref.txt.Parent;
                                    GroupBox B = (GroupBox)A.Parent;
                                    WrapPanel C = (WrapPanel)B.Parent;
                                    GroupBox D = (GroupBox)C.Parent;
                                    Expander E = (Expander)D.Parent;

                                    E.IsExpanded = true;
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine("Couldnt find parent??");
                                }

                            });
                        }
                        else if (data == "OKAY" + TERMINATOR) // OKAY MESSAGE
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                var textref = socketMap[clientIP];
                                textref.txt.Text = "Status: Normal";
                                textref.txt.Background = Brushes.ForestGreen;
                            });
                        }
                    }
                    //handler.Shutdown(SocketShutdown.Both);
                    //handler.Close();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("wtf");
                Debug.WriteLine(e);
            }
        }


        // Creates groups. Groups are more or less useless for now
        // TODO: Make groups not useless
        private void CreateGroup(object sender, RoutedEventArgs e)
        {
            PopUp1 popup = new PopUp1
            {
                Owner = Application.Current.MainWindow, // We must also set the owner for this to work.
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            popup.ShowDialog();
            string test = popup.TheValue;

            if (test == "NaN") return;
            if (test.ToLower() == "default") return;


            module.group.Add(test);
            string testy = JsonConvert.SerializeObject(module, Newtonsoft.Json.Formatting.Indented);
            Debug.WriteLine(testy);


            File.WriteAllText("config.json", testy);


            InitializeGroup(test);
        }


        private void InitializeGroup(string name)
        {
            // Prevent another default added
            if (name.ToLower() == "default")
            {
                groupMap.Add("Default", Default);
                return;
            }

            Expander expander = new Expander
            {
                Header = name,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCCCCCC")),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF222222")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF333333"))
            };

            GroupBox newGroup = new GroupBox
            {
                BorderThickness = new Thickness(0.1),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF444444"))
            };

            WrapPanel wrap = new WrapPanel();

            groupMap.Add(name, wrap);

            expander.Content = newGroup;
            newGroup.Content = wrap;
            GroupWindow.Children.Add(expander);


            Button btn = new Button
            {
                Content = name,
                Background = Brushes.DarkGray,
                Foreground = Brushes.White,
            };
            groupnamestodelete.Children.Add(btn);

            btn.Click += (o, ev) =>
            {
                groupnamestodelete.Children.Remove(btn);
                GroupWindow.Children.Remove(expander);
            };
        }




        private void CreateCam(object sender, RoutedEventArgs e)
        {
            // Create PopUp
            PopUp2 popup = new PopUp2
            {
                Owner = Application.Current.MainWindow, // We must also set the owner for this to work.
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            popup.ShowDialog();


            // Occurs after popup closes
            if (popup.confirm) // closed via create button
            {
                Datum m = new Datum
                {
                    name = popup.personName,
                    num = popup.roomNum,
                    ip = popup.ipAddr,
                    group = popup.selectedGroup
                };

                // Add to json file
                //module = JsonConvert.DeserializeObject<Module>(json);
                module.data.Add(m);
                string testy = JsonConvert.SerializeObject(module, Newtonsoft.Json.Formatting.Indented);
                Debug.WriteLine(testy);


                File.WriteAllText("config.json", testy);


                //module = null; // send module to the void


                InitializeModule(m, popup.pi);
                
            }
        }



 

        // Second field is so that the stream connected from a popup is reused
        private void InitializeModule(Datum m, SshClient client=null)
        {
            // TODO: Replace debug writeline with proper error message
            if (socketMap.ContainsKey(m.ip))
            {
                Debug.WriteLine("ERROR: Key already in dict");
                return;
            }


            // Establish connection first
            string stream;


            try
            {
                if (client == null)
                {
                    client = new SshClient(m.ip, username, password);
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    client.Connect();
                }

                stream = "http://" + m.ip + ":8080/?action=stream";

            }
            catch (Exception z)
            {
                // TODO: Make this display somewhere
                failedConn.Add(m);
                return;
            }

            // WPF visual stuff
            SolidColorBrush darkGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF333333"));
            SolidColorBrush bordGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF000000"));
            SolidColorBrush foreGray = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEEEEEE"));

            GroupBox newGroup = new GroupBox
            {
                BorderThickness = new Thickness(0.1),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };

            StackPanel newStack = new StackPanel();
            newGroup.Content = newStack;

            Border border = new Border
            {
                BorderBrush = bordGray,
                BorderThickness = new Thickness(0.1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(3),
                Background = darkGray
            };

            // Grid nonsense
            Grid minigrid = new Grid();
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition();
            c2.Width = GridLength.Auto;
            minigrid.ColumnDefinitions.Add(c1);
            minigrid.ColumnDefinitions.Add(c2);


            TextBlock roomNum = new TextBlock
            {
                FontSize = 14,
                Text = m.num,
                Margin = new Thickness(5),
                Background = darkGray,
                TextAlignment = TextAlignment.Center,
                Foreground = foreGray,
                FontWeight = FontWeights.Bold
            };


            // Module options
            Menu menu = new Menu();
            MenuItem options = new MenuItem
            {
                Header = " + ",
                FontSize = 14,
                Background = foreGray
            };
                MenuItem move = new MenuItem
                {
                    Header = "Move to"
                };
                MenuItem delete = new MenuItem
                {
                    Header = "Delete this module"
                };
                MenuItem reposition = new MenuItem
                {
                    Header = "Move module position"
                };
                MenuItem editRoom = new MenuItem
                {
                    Header = "Edit Room #"
                };
                MenuItem editName = new MenuItem
                {
                    Header = "Edit Name"
                };


            menu.Items.Add(options);
                options.Items.Add(move);
                options.Items.Add(delete);
                options.Items.Add(reposition);
                options.Items.Add(editRoom);
                options.Items.Add(editName);


            minigrid.Children.Add(roomNum);
            minigrid.Children.Add(menu);
            roomNum.SetValue(Grid.ColumnProperty, 0);
            menu.SetValue(Grid.ColumnProperty, 1);

            Image imgtest = new Image
            {
                MaxWidth = 288,
                MinWidth = 288
            };
            TextBlock personName = new TextBlock
            {
                FontSize = 14,
                Text = m.name,
                Margin = new Thickness(1),
                Background = Brushes.Gray,
                TextAlignment = TextAlignment.Center
            };
            TextBlock status = new TextBlock
            {
                FontSize = 14,
                Text = "Status: Normal",
                Margin = new Thickness(1),
                Background = Brushes.ForestGreen,
                TextAlignment = TextAlignment.Center
            };
            Button testBtn = new Button
            {
                Content = "Reset",
                Background = Brushes.DarkGray,
                Foreground = Brushes.White,
            };
            Button disableBtn = new Button
            {
                Content = "Disable Alarms",
                Background = Brushes.DarkGray,
                Foreground = Brushes.White,
            };

            border.Child = minigrid;
            newStack.Children.Add(border);
            newStack.Children.Add(imgtest);
            newStack.Children.Add(personName);
            newStack.Children.Add(status);
            newStack.Children.Add(testBtn);
            newStack.Children.Add(disableBtn);

            // TODO: CHANGE THIS LATER WHEN DEALING WITH GROUPS
            // m.group blah blah


            groupMap[m.group].Children.Add(newGroup);
            //Default.Children.Add(newGroup);



            // Create new stream thread
            var mjpeg = new MjpegDecoder();
            mjpeg.ParseStream(new Uri(stream));

            // Repeated code is bad, but I couldnt figure out a way to store the lambda as a method or a variable
            // Essentially sets the image component to update every frame


            mjpeg.FrameReady += (o, ev) =>
            {
                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    imgtest.Source = ev.BitmapImage;
                }));
            };

            MJPGMaps grp = new MJPGMaps
            {
                img = imgtest,
                ip = stream
            };

            mjpegs.Add(mjpeg, grp);
            mjpeg.Error += _mjpg_Error;



            // Ensure there are no duplicate python script instances running (MIGHT BE UNNECESSARY/BAD)
            client.RunCommand("sudo killall python3");


            // REPLACE SCRIPT.PY WITH NAME OF SCRIPT, MAKE SURE IT TAKES A COMMAND ARGUMENT
            // Runs in background
            var script = client.CreateCommand("python3 script.py " + myIPString);
            script.BeginExecute();

            // Associate reset button with the following lambda function
            testBtn.Click += (o, ev) =>
            {
                // Kill currently running stream if it exists
                mjpeg.StopStream();
                client.RunCommand("sudo killall mjpg_streamer");

                // Line below Directly start from mjpg_streamer; unnecessary for now
                //client.RunCommand("mjpg_streamer -i \"input_raspicam.so - x 1280 - y 720 - fps 30\" -o output_http.so");

                // Start script in background
                var cmd = client.CreateCommand("bash startcam.sh");
                cmd.BeginExecute();


                mjpeg.ParseStream(new Uri(stream));

                // Need to reset stream source as well
                mjpeg.FrameReady += (o, ev) =>
                {
                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                    {
                        imgtest.Source = ev.BitmapImage;
                    }));   
                };
                mjpeg.Error += _mjpg_Error;

                //client.RunCommand("sudo killall mjpg_streamer");
                //client.RunCommand("./startcam.sh");
            };

            SocketStuff sck = new SocketStuff
            {
                txt = status,
                enabled = true
            };

            Button message = new Button();
            message.ToolTip = new ToolTip { Content = "Cannot remove this until alarms are enabled" };

            message.Content = DateTime.UtcNow.ToString("MM-dd-yyyy");
            message.Content += " " + DateTime.Now.ToString("t");
            message.Content += (" ALARMS DISABLED AT:  ");
            // BEHOLD CRINGE
            message.Content += roomNum.Text;
            message.Background = Brushes.Blue;

            // Disables alarm receiving and sends a message to pi
            disableBtn.Click += (o, ev) =>
            {
                byte[] msg;

                lock (sck)
                {
                    sck.enabled = !sck.enabled;

                    if (sck.enabled)
                    {
                        sck.txt.Text = "Status: Normal";
                        sck.txt.Background = Brushes.ForestGreen;
                        disableBtn.Content = "Disable Alarms";
                        try
                        {
                            msg = Encoding.ASCII.GetBytes("ENABLE<EOF>");
                            sck.testSck.Send(msg);
                        }
                        catch (Exception exp)
                        {
                            Debug.WriteLine("Socket doesn't exist yet...");
                        }

                        // Add disabled message to log as a reminder


                        if (messagelog.Children.Contains(message)) messagelog.Children.Remove(message);

                    }
                    else
                    {
                        sck.txt.Text = "ALARMS DISABLED";
                        sck.txt.Background = Brushes.Blue;
                        disableBtn.Content = "Enable Alarms";
                        try
                        {
                            msg = Encoding.ASCII.GetBytes("DISABLE<EOF>");
                            sck.testSck.Send(msg);
                        }
                        catch (Exception exp)
                        {
                            Debug.WriteLine("Socket doesn't exist yet...");
                        }

                        messagelog.Children.Add(message);
                    }

                }

            };


            // Now to add the target IP address to the map
            //Debug.WriteLine(popup.ipAddr);
            try
            {

                socketMap.Add(m.ip, sck);
            }
            catch (Exception e)
            {
                // this should never be a problem as the check happens fairly early
                Debug.WriteLine("Cant add same key twice");
            }



            // Settings button implementation

            List<MenuItem> temp = new List<MenuItem>();

            foreach (string grptmp in module.group)
            {

                MenuItem tmp = new MenuItem
                {
                    Header = grptmp
                };

                move.Items.Add(tmp);
                temp.Add(tmp);

                tmp.Click += (o, exv) =>
                {
                    WrapPanel oldwrp = groupMap[m.group];
                    WrapPanel newwrp = groupMap[grptmp];
                    oldwrp.Children.Remove(newGroup);
                    newwrp.Children.Add(newGroup);


                    m.group = grptmp;

                    string testy = JsonConvert.SerializeObject(module, Newtonsoft.Json.Formatting.Indented);
                    Debug.WriteLine(testy);


                    File.WriteAllText("config.json", testy);


                };
            }


            delete.Click += (o, v) =>
            {
                groupMap[m.group].Children.Remove(newGroup);
                mjpegs.Remove(mjpeg);
            };

            reposition.Click += (o, v) =>
            {

            };

            StackPanel editRoomPanel = new StackPanel { Orientation = Orientation.Horizontal };
            TextBox editRoomText = new TextBox { Width = 100 };
            Button editRoomBtn = new Button { Content = "Submit" };

            editRoomPanel.Children.Add(editRoomText);
            editRoomPanel.Children.Add(editRoomBtn);
            editRoom.Items.Add(editRoomPanel);

            editRoomBtn.Click += (o, v) =>
            {
                if (editRoomText.Text == "") return;
                roomNum.Text = editRoomText.Text;
            };


            StackPanel editNamePanel = new StackPanel { Orientation = Orientation.Horizontal };
            TextBox editNameText = new TextBox { Width = 100 };
            Button editNameBtn = new Button { Content = "Submit" };

            editNamePanel.Children.Add(editNameText);
            editNamePanel.Children.Add(editNameBtn);
            editName.Items.Add(editNamePanel);

            editNameBtn.Click += (o, v) =>
            {
                if (editNameText.Text == "") return;
                personName.Text = editNameText.Text;
            };

        }




        private void _mjpg_Error(object sender, MjpegProcessor.ErrorEventArgs e)
        {
            Debug.WriteLine(e.Message);

            // RESET THE STREAM
            try
            {
                MJPGMaps grp = mjpegs[(MjpegDecoder)sender];
                ((MjpegDecoder)sender).StopStream();
                // restart stream
                ((MjpegDecoder)sender).ParseStream(new Uri(grp.ip));

                /*
                ((MjpegDecoder)sender).FrameReady += (o, ev) =>
                {
                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                    {
                        grp.img.Source = ev.BitmapImage;
                    }));
                };
                
                ((MjpegDecoder)sender).Error += _mjpg_Error;
                */

            }
            catch (Exception ev)
            {
                Debug.WriteLine(ev.Message);
            }
        }



        // Attempt at scanning the network range for automatically adding raspberry pi units
        // Basically a QOL feature
        private void ScanIP(object sender, RoutedEventArgs e)
        {
            int index = myIPString.LastIndexOf(".");
            string iprange = myIPString.Substring(0, index);


        }




        // AForge nonsense, might try to use it later for saving memory

        /*
        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        } */















        /*
        FrameworkElement CloneFrameworkElement(FrameworkElement originalElement)
        {
            string elementString = XamlWriter.Save(originalElement);

            StringReader stringReader = new StringReader(elementString);
            XmlReader xmlReader = XmlReader.Create(stringReader);
            FrameworkElement clonedElement = (FrameworkElement)XamlReader.Load(xmlReader);

            return clonedElement;
        } */

        private void WindowClose(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // save default group
        private void WindowClosing(object sender, CancelEventArgs e)
        {
           
            //base.OnClosing(e);

            //string xaml = System.Windows.Markup.XamlWriter.Save(this.Content);
            //Debug.WriteLine(xaml);
            //System.IO.File.WriteAllText("state.txt", xaml);

            /*
            StringBuilder outstr = new StringBuilder();

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;
            settings.NewLineOnAttributes = true;


            XamlDesignerSerializationManager dsm = new XamlDesignerSerializationManager(XmlWriter.Create(outstr, settings));
            dsm.XamlWriterMode = XamlWriterMode.Expression;

            XamlWriter.Save(Default, dsm);
            string savedControls = outstr.ToString();

            File.WriteAllText(@"AA.xaml", savedControls);
            */
        }

       /*
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            string xaml = System.Windows.Markup.XamlWriter.Save(this.Content);
            System.IO.File.WriteAllText("state.txt", xaml);
        } */

    }
}
