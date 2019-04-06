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
        // Class fields
        bool terminating = false;
        bool connected = false;
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        String serverRSApublicKey;

        // Class Constructors
        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
            readKey();
        }

        // Utilities
        public void Reset()
        {
            textBox_Username.Text = "";
            textBox_Password.Text = "";
            textBox_IP.Text = "";
            textBox_Port.Text = "";
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

        public Tuple< String, String, String, Int32> validateAndGetInputs()
        {
            if (textBox_Username.Text == "" || textBox_Password.Text == "" || textBox_IP.Text == "" || textBox_Port.Text == "")
            {
                Reset();
                MessageBox.Show("There are empty fields, please try again.");
                return new Tuple<String, String, String, int>("", "", "", -1);
            }

            Int32 portInt;
            portInt = Int32.Parse(textBox_Port.Text);
            return new Tuple<String, String, String, Int32>(textBox_Username.Text, textBox_Password.Text, textBox_IP.Text, portInt);
        }
        
        private void readKey()
        {
            try
            {
                using (System.IO.StreamReader fileReader =
                new System.IO.StreamReader(@"C:\\Users\\oranc\\Desktop\\Course stuff\\cs432 prroject 1\\server_enc_dec_pub.txt"))
                {
                    serverRSApublicKey = fileReader.ReadLine();
                }
            }
            catch
            {
                MessageBox.Show(this, "Couldn't open the file", "Failure", MessageBoxButtons.OK);
            }
        }

        // Networking
        private void initiateServerConnection(String ip, Int32 port)
        {
            clientSocket.Connect(ip, port);
            EnrollBtn.Text = "Disconnect";
            connected = true;
            textBox_Status.AppendText("Connected to server\n");
        }

        private void enrollToServer(String username, String password)
        {
            byte[] sha256 = hashWithSHA256(textBox_Password.Text);
            byte[] halfsha256 = sha256.Take(16).ToArray();
            byte[] userbytes = Encoding.Default.GetBytes(username);
            byte[] sendbytes = halfsha256.Concat(userbytes).ToArray();

            string mes = Encoding.Default.GetString(sendbytes, 0, sendbytes.Length);
            byte[] encryptedRSA = encryptWithRSA(mes, 3072, serverRSApublicKey);

            byte[] finalBytes = Encoding.Default.GetBytes("e|").Concat(encryptedRSA).ToArray();
            clientSocket.Send(finalBytes);
        }

        private bool enrollmentVerified()
        {
            byte[] buffer = new Byte[2048];
            int recievedbytes = clientSocket.Receive(buffer);
            if (recievedbytes == 0)
            {
                MessageBox.Show(this, "Error during verification.", "Failure", MessageBoxButtons.OK);
            }
            buffer = buffer.Take(recievedbytes).ToArray();
            byte[] sign = buffer.Take(384).ToArray();
            byte[] message = buffer.Skip(384).ToArray();
            string messagefirstParam = Encoding.UTF8.GetString(message, 0, message.Length);

            string verKey;
            using (System.IO.StreamReader fileReader =
            new System.IO.StreamReader(@"C:\\Users\\oranc\\Desktop\\Course stuff\\cs432 prroject 1\\server_signing_verification_pub.txt"))
            {
                verKey = fileReader.ReadLine();
            }
            if (verifyWithRSA(messagefirstParam, 3072, verKey, sign))
            {
                if (messagefirstParam == "success")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                MessageBox.Show(this, "Server signature verification failed", "Failure", MessageBoxButtons.OK);
                return false;
            }
        }

        private void startClient()
        {
            try
            {
                // 1 - parse input values
                String username, password, ip;
                Int32 port;

                Tuple<String, String, String, Int32> fieldValues = validateAndGetInputs();

                username = fieldValues.Item1;
                password = fieldValues.Item2;
                ip = fieldValues.Item3;
                port = fieldValues.Item4;

                // 2 - form socket connection to server for enrollment
                initiateServerConnection(ip, port);

                // 3 - enrollment
                enrollToServer(username, password);

                // 4 - server enrollment response & signature verification
                if (enrollmentVerified())
                {
                    textBox_Status.AppendText("Successfully enrolled to server");
                }
                else
                {
                    textBox_Status.AppendText("Enrollment to server failed");
                }
            }
            catch
            {
                MessageBox.Show("Something went wrong!");
            }
        }

        private void stopClient()
        {
            connected = false;
            clientSocket.Disconnect(false);
            clientSocket.Close();
            EnrollBtn.Text = "Connect";
        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[2048];
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
        // GUI Events
        private void EnrollBtn_Click(object sender, EventArgs e)
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

        private void LoginBtn_Click(object sender, EventArgs e)
        {
            try
            {
                //SEND USERNAME WITH AUTHENTICATOR
                string userstr = textBox_Username.Text;
                string autcator = "a|";
                string fstr = autcator + userstr;
                byte[] bytes = Encoding.ASCII.GetBytes(fstr);
                clientSocket.Send(bytes);

                // 128 BIT NUMBER RECEIVE & SEND
                byte[] buffer128 = new Byte[2048];
                int recievedbytes128 = clientSocket.Receive(buffer128);
                if (recievedbytes128 == 0)
                {
                    MessageBox.Show(this, "Error with getting the 128-bit number.", "Failure", MessageBoxButtons.OK);
                }
                byte[] sha256OfPass = hashWithSHA256(textBox_Password.Text);
                byte[] upperhalfsha256 = sha256OfPass.Skip(16).ToArray();
                string hashstrOf128 = Encoding.UTF8.GetString(buffer128, 0, buffer128.Length);
                byte[] hmacsha256 = applyHMACwithSHA256(hashstrOf128, upperhalfsha256);
                clientSocket.Send(hmacsha256);
            }
            catch
            {
                MessageBox.Show(this, "Something wrong with authentication", "Failure", MessageBoxButtons.OK);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        // Cryptography
        static byte[] applyHMACwithSHA256(string input, byte[] key)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create HMAC applier object from System.Security.Cryptography
            HMACSHA256 hmacSHA256 = new HMACSHA256(key);
            // get the result of HMAC operation
            byte[] result = hmacSHA256.ComputeHash(byteInput);

            return result;
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
    }
}



