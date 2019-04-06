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


                    //RSA ENCRYPTION

                    try
                    {
                        byte[] sha256 = hashWithSHA256(textBox_Password.Text);
                        byte[] halfsha256 = sha256.Take(16).ToArray();
                        byte[] userbytes = Encoding.ASCII.GetBytes(str);
                        byte[] sendbytes = halfsha256.Concat(userbytes).ToArray();

                        string key;
                        using (System.IO.StreamReader fileReader =
                        new System.IO.StreamReader(@"C:\Users\Vixie\Documents\Visual Studio 2012\Projects\repos\CS432_Client\server_enc_dec_pub.txt"))
                        {
                            key = fileReader.ReadLine();
                        }
                        string mes = Encoding.UTF8.GetString(sendbytes, 0, sendbytes.Length);
                        byte[] encryptedRSA = encryptWithRSA(mes, 3072, key);
                        clientSocket.Send(encryptedRSA);
                    }
                    catch
                    {
                        MessageBox.Show(this, "RSA Encryption Failed", "Failure", MessageBoxButtons.OK);
                    }

                   
                   //SIGN VERIFICATION

                    try
                    {
                        byte[] buffer = new Byte[1024];
                        clientSocket.Receive(buffer);
                        byte[] sign = buffer.Take(384).ToArray();
                        byte[] message = buffer.Skip(384).Take(buffer.Length - 384).ToArray();
                        string messagefirstParam = Encoding.UTF8.GetString(message, 0, message.Length);

                        string verKey;
                        using (System.IO.StreamReader fileReader =
                        new System.IO.StreamReader(@"C:\Users\Vixie\Documents\Visual Studio 2012\Projects\repos\CS432_Client\server_signing_verification_pub.txt"))
                        {
                            verKey = fileReader.ReadLine();
                        }
                        if (verifyWithRSA(messagefirstParam, 3072, verKey, sign))
                        {
                            //enroll
                        }
                        else
                        {
                            return;
                        }
                    }
                    catch
                    {
                        MessageBox.Show(this, "Sign Verification Failed", "Failure", MessageBoxButtons.OK);
                    }
                    
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
            clientSocket.Disconnect(false);
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
                    clientSocket.Disconnect(false);
                    clientSocket.Close();
                    connected = false;
                }
            }
        }

        private void SendBtn_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                string str1 = textBox_deneme.Text;
                string str2 = "m|";
                string newstr = str2 + str1;
                byte[] denemebytes = Encoding.ASCII.GetBytes(newstr);
                clientSocket.Send(denemebytes);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        static byte[] decryptWithRSA(string input, int algoLength, string xmlStringKey)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlStringKey);
            byte[] result = null;

            try
            {
                result = rsaObject.Decrypt(byteInput, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }


        static bool verifyWithRSA(string input, int algoLength, string xmlString, byte[] signature)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlString);
            bool result = false;

            try
            {
                result = rsaObject.VerifyData(byteInput, "SHA256", signature);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }


        static byte[] encryptWithRSA(string input, int algoLength, string xmlStringKey)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlStringKey);
            byte[] result = null;

            try
            {
                //true flag is set to perform direct RSA encryption using OAEP padding
                result = rsaObject.Encrypt(byteInput, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
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

        public static string generateHexStringFromByteArray(byte[] input)
        {
            string hexString = BitConverter.ToString(input);
            return hexString.Replace("-", "");
        }

        public static byte[] hexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}



