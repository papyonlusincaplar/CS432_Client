using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace CS432_Client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        Socket clientSocket;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        public void Reset()
        {
            textBox_Username.Text = "";
            textBox_Password.Text = "";
            textBox_IP.Text = "";
            textBox_Port.Text = "";
        }
                
        private void ConnectBtn_Click(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBox_IP.Text;
            int port;
            if (textBox_Username.Text == "" || textBox_Password.Text == "" || textBox_IP.Text == "" || textBox_Port.Text == "")
            {
                string message = "There are empty fields, please try again.";
                Reset();
                MessageBox.Show(message);
                return;
            }
            if (Int32.TryParse(textBox_Port.Text, out port))
            {
                try
                {
                    clientSocket.Connect(IP, port);
                    ConnectBtn.Enabled = false;
                    connected = true;
                    textBox_Status.AppendText("Connected to server\n");

                    Thread receiveThread = new Thread(new ThreadStart(Receive));
                    receiveThread.Start();

                }
                catch
                {
                    string message = "Could not connect to server\n";
                    Reset();
                    MessageBox.Show(message);
                    return;
                }
            }
            else
            {
                string message = "Check the port\n";
                Reset();
                MessageBox.Show(message);
                return;
            }

        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[64];
                    clientSocket.Receive(buffer);

                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                    textBox_Status.AppendText(incomingMessage + "\n");
                }
                catch
                {
                    if (!terminating)
                    {
                        textBox_Status.AppendText("The server has disconnected\n");
                    }
                    clientSocket.Close();
                    connected = false;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }
    }
}
