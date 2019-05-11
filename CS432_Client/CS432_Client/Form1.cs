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
        bool connected = false;
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        String serverRSApublicKey;
        String clientUsername;
        String clientPassword;

        String verificationKey;

        bool isAuthenticated = false;

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
                    log("Encryption keys are read from file system");
                }

                using (System.IO.StreamReader fileReader =
                new System.IO.StreamReader(@"C:\\Users\\oranc\\Desktop\\Course stuff\\cs432 prroject 1\\server_signing_verification_pub.txt"))
                {
                    verificationKey = fileReader.ReadLine();
                    log("Verification keys are read from file system");
                }
            }
            catch
            {
                MessageBox.Show(this, "Error reading the key file", "Failure", MessageBoxButtons.OK);
            }
        }

        private void log(String message)
        {
            if (InvokeRequired)
                Invoke(new Action<string>(textBox_Status.AppendText), message + "\n");
            else
                textBox_Status.AppendText(message + "\n");
        }

        // Networking
        private void parseMessage(String message)
        {
            char flag = message[0];
            String content = message.Substring(2);

            switch (flag)
            {
                case 'm': // incoming message
                    break;
                case 'd': // disconnection request
                    destroyServerConnection();
                    break;
                case 'a': // authentication challenge
                    solveChallenge(content);
                    break;
                case 'b': // authentication acknowledgment
                    acknowledgeAuth(content);
                    break;
                case 'e': // enrollment verification/failure
                    finalizeEnrollment(content);
                    break;
            }
        }

        private void destroyServerConnection()
        {
            connected = false;
            transmitClear(null, "d");
            clientSocket.Disconnect(true);
            log("Disconnected from server");
        }

        private void transmitClear(Byte[] message, String flag)
        {
            Byte[] transmissionData = Encoding.Default.GetBytes(flag + "|");
            if (message != null)
            {
                clientSocket.Send(transmissionData.Concat(message).ToArray());
            }
            else
            {
                clientSocket.Send(transmissionData);
            }
        }

        private void createServerConnection(String ip, Int32 port)
        {
            clientSocket.Connect(ip, port);
            connected = true;
            log("Connected to server");
        }

        //private bool enrollmentVerified()
        //{
        //    byte[] buffer = new Byte[2048];
        //    int recievedbytes = clientSocket.Receive(buffer);
        //    if (recievedbytes == 0)
        //    {
        //        MessageBox.Show(this, "Error during verification.", "Failure", MessageBoxButtons.OK);
        //    }
        //    buffer = buffer.Take(recievedbytes).ToArray();
        //    byte[] sign = buffer.Take(384).ToArray();
        //    byte[] message = buffer.Skip(384).ToArray();
        //    string messagefirstParam = Encoding.UTF8.GetString(message, 0, message.Length);

        //    string verKey;
        //    using (System.IO.StreamReader fileReader =
        //    new System.IO.StreamReader(@"C:\\Users\\oranc\\Desktop\\Course stuff\\cs432 prroject 1\\server_signing_verification_pub.txt"))
        //    {
        //        verKey = fileReader.ReadLine();
        //    }
        //    if (verifyWithRSA(messagefirstParam, 3072, verKey, sign))
        //    {
        //        if (messagefirstParam == "success")
        //        {
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }
        //    else
        //    {
        //        MessageBox.Show(this, "Server signature verification failed", "Failure", MessageBoxButtons.OK);
        //        return false;
        //    }
        //}

        private void enrollToServer()
        {
            try
            {
                // 1 - parse input values
                String username, password, ip;
                Int32 port;

                Tuple<String, String, String, Int32> fieldValues = validateAndGetInputs();

                username = fieldValues.Item1;
                clientUsername = username;
                password = fieldValues.Item2;
                clientPassword = password;
                ip = fieldValues.Item3;
                port = fieldValues.Item4;

                createServerConnection(ip, port);

                // 2 - enrollment
                // prepare the enrollment data
                byte[] passwordHash = hashWithSHA256(clientPassword).Skip(16).ToArray();
                byte[] encodedUsername = Encoding.Default.GetBytes(username);
                byte[] sendBytes = passwordHash.Concat(encodedUsername).ToArray();

                // encrypt the data
                string mes = Encoding.Default.GetString(sendBytes, 0, sendBytes.Length);
                byte[] encryptedRSA = encryptWithRSA3072(mes);

                // send the data & listen to incoming transmissions
                Thread serverListener = new Thread(new ThreadStart(listenServer));
                serverListener.IsBackground = true;
                serverListener.Start();

                transmitClear(encryptedRSA, "e");
            }
            catch
            {
                MessageBox.Show("Exception catched during enrollment request");
            }
        }

        private void finalizeEnrollment(String message)
        {
            // parse the message
            Byte[] rawMessage = Encoding.Default.GetBytes(message);
            Byte[] signature = rawMessage.Take(384).ToArray();
            String status = Encoding.Default.GetString(rawMessage.Skip(384).ToArray());

            // verify the signature
            if (verifyWithRSA(status, 3072, verificationKey, signature))
            {
                if (status == "success")
                {
                    log("Successfully enrolled to server");
                }
                else if (status == "error")
                {
                    log("Username already exists");
                }
                else
                {
                    log("Unknown signed message received");
                }
            }
            else
            {
                MessageBox.Show(this, "Server signature verification failed", "Failure", MessageBoxButtons.OK);
            }
            destroyServerConnection();
        }
        
        private void sendAuthenticationRequest()
        {
            transmitClear(Encoding.Default.GetBytes(clientUsername), "a");
        }

        private void listenServer()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[8192];
                    int receivedBytes = clientSocket.Receive(buffer);
                    buffer = buffer.Take(receivedBytes).ToArray();

                    String decodedMessage = (Encoding.Default.GetString(buffer));

                    parseMessage(decodedMessage);
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, e.Message, "Failure", MessageBoxButtons.OK);
                }
            }
            log("Stopped listening to server");
        }

        private void solveChallenge(String challenge)
        {
            Byte[] upperHashPw = hashWithSHA256(clientPassword).Skip(16).ToArray();
            Byte[] challengeResponse = applyHMACwithSHA256(challenge, upperHashPw);
            byte[] finalBytes = Encoding.Default.GetBytes("h|").Concat(challengeResponse).ToArray();
            clientSocket.Send(finalBytes);
        }

        private void acknowledgeAuth(String message)
        {
            if (message == "ack_positive")
            {
                log("Successfully authenticated to the server");
                isAuthenticated = true;
            }
            else if (message == "ack_negative")
            {
                log("Wrong password!");
            }
            else
            {
                MessageBox.Show(this, "Unknown token received from server", "Failure", MessageBoxButtons.OK);
            }
        }

        // GUI Events
        private void EnrollBtn_Click(object sender, EventArgs e)
        {
            enrollToServer();
        }

        private void LoginBtn_Click(object sender, EventArgs e)
        {
            try
            {
                // 1 - create connection to server
                String username, password, ip;
                Int32 port;

                Tuple<String, String, String, Int32> fieldValues = validateAndGetInputs();

                username = fieldValues.Item1;
                clientUsername = username;
                password = fieldValues.Item2;
                clientPassword = password;
                ip = fieldValues.Item3;
                port = fieldValues.Item4;

                createServerConnection(ip, port);

                Thread serverListener = new Thread(new ThreadStart(listenServer));
                serverListener.IsBackground = true;
                serverListener.Start();

                // 2 - send authentication request
                sendAuthenticationRequest();
            }
            catch (Exception exc)
            {
                MessageBox.Show(this, exc.Message, "Failure", MessageBoxButtons.OK);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            destroyServerConnection();
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

        bool verifyWithRSA(string input, int algoLength, string xmlString, byte[] signature)
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
                MessageBox.Show(this, e.Message, "Failure", MessageBoxButtons.OK);
            }

            return result;
        }

        byte[] encryptWithRSA3072(string input)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(3072);
            // set RSA object with xml string

            byte[] result = null;

            try
            {
                rsaObject.FromXmlString(serverRSApublicKey);
                //true flag is set to perform direct RSA encryption using OAEP padding
                result = rsaObject.Encrypt(byteInput, true);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Failure", MessageBoxButtons.OK);
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