﻿using System;
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
using System.Security.Cryptography;


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

        static byte[] hashWithSHA256(string input)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create a hasher object from System.Security.Cryptography
            SHA256CryptoServiceProvider sha256Hasher = new SHA256CryptoServiceProvider();
            // hash and save the resulting byte array
            byte[] result = sha256Hasher.ComputeHash(byteInput);

            return result;
        }

        public void Reset()
        {
            textBox_Username.Text = "";
            textBox_Password.Text = "";
            textBox_IP.Text = "";
            textBox_Port.Text = "";
        }

        private void startClient()
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
                    string str = textBox_Username.Text;
                    clientSocket.Connect(IP, port);
                    ConnectBtn.Text = "Disconnect";
                    connected = true;
                    textBox_Status.AppendText("Connected to server\n");

                    byte[] sha256 = hashWithSHA256(textBox_Password.Text);
                    byte[] newsha256 = sha256.Take(16).ToArray();
                    byte[] bytes = Encoding.ASCII.GetBytes(str);
                    byte[] sendbytes = newsha256.Concat(bytes).ToArray();
                    clientSocket.Send(sendbytes);

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

        private void stopClient()
        {
            connected = false;
            clientSocket.Close();
            ConnectBtn.Text = "Connect";
        }
                
        private void ConnectBtn_Click(object sender, EventArgs e)
        {
            if (!connected)
            {
                startClient();
            }
            else
            {
                stopClient();
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
                        textBox_Status.AppendText("Disconnected from server\n");
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
