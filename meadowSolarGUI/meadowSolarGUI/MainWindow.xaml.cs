/* meadowSolarGUI Project
 * 
 * ECET 230
 * Camosun College
 * Tony Biccum
 *
 * 
 * This project creates a GUI for solar panel data received from the meadow board.
 * It communicates through packets and can send packets to the meadow to turn on LEDs.
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

namespace meadowSolarGUI
{
    public partial class MainWindow : Window
    {
        //Initialize variables
        private bool bPortOpen = false;
        private string text;
        //private IPAddress address;
        private int checkSumError = 0;
        private int checkSumCalculated = 0;
        private int oldPacketNumber = -1;
        private int newPacketNumber = 0;
        private int lostPacketCount = 0;
        private int packetRollover = 0;
        private int txCheckSum;

        private StringBuilder stringBuilder = new StringBuilder("###1111196");
        SolarCalc solarcalc = new SolarCalc();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Initialize some values so we dont start the program empty
            text_packetReceived.Text = "###0000000000000000000000000000000000";
            text_Send.Text = "###0000000";
            text_checkSumError.Text = "0";
            text_packetRollover.Text = "0";
            text_packetLost.Text = "0";
        }

        //private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    try
        //    {
        //        //Need to save string from packet into text variable 
        //        text = text.ToString();

        //        if (text_packetReceived.Dispatcher.CheckAccess())     //If we have access to the thread then update the ui
        //        {
        //            UpdateUI(text);
        //        }
        //        else
        //        {                                               //If we do not have access to the thread
        //            text_packetReceived.Dispatcher.Invoke(() => { UpdateUI(text); });
        //        }
        //    }
        //    catch (TimeoutException) { }                        //If no data is received then timeout error
        //}

        private void UpdateUI(string text)
        {
            checkSumCalculated = 0; //Reset the calculated checksum otherwise it will keep addin

            text_packetReceived.Text = text + text_packetReceived.Text; //Show the received text
            text_packetLength.Text = text.Length.ToString();

            //Note that paseSubString will increment over the number of chars that we pass to it
            parseSubString parseSubString = new parseSubString();

            if (text.Substring(0, 3) == "###" && text.Length > 37) //Are we receiving a real packet? 
            {
                //If a real packet then save the corresponding bytes:
                string placeholder = parseSubString.parseString(text, 3); //Place holds our 3 hashtags to parse correctly 
                text_packetNumber.Text = parseSubString.parseString(text, 3);
                newPacketNumber = Convert.ToInt32(text_packetNumber.Text);
                text_A0.Text = parseSubString.parseString(text, 4);
                text_A1.Text = parseSubString.parseString(text, 4);
                text_A2.Text = parseSubString.parseString(text, 4);
                text_A3.Text = parseSubString.parseString(text, 4);
                text_A4.Text = parseSubString.parseString(text, 4);
                text_A5.Text = parseSubString.parseString(text, 4);
                text_Binary.Text = parseSubString.parseString(text, 4);
                text_checkSumReceived.Text = parseSubString.parseString(text, 3);

                //CHECK SUM BUSINESS
                for (int i = 3; i < 34; i++)
                {
                    checkSumCalculated += (byte)text[i]; //Sum all the bytes within text variable (received from data_received method)
                }
                checkSumCalculated %= 1000;
                text_checkSumCalculated.Text = checkSumCalculated.ToString(); //Put the calculated check sum into the box

                //Calculate error if there is any, if no error just display the solar values
                var checkSumReceived = Convert.ToInt32(text_checkSumReceived.Text);
                if (checkSumReceived == checkSumCalculated)
                {
                    displaySolarData(text);                                 //Display solar data if there is no error
                }
                else
                {
                    checkSumError++;                                        //Record an error
                    text_checkSumError.Text = checkSumError.ToString();     //Print the amount of errors into the text box
                }

                if (oldPacketNumber > -1)
                {
                    if (newPacketNumber == 0)                               //New packet value receives value from packetNumber text
                    {                                                       //So when firmware rolls packet over we enter here
                        packetRollover++;
                        text_packetRollover.Text = packetRollover.ToString();
                        if (oldPacketNumber != 999)
                        {
                            lostPacketCount++;
                            text_packetLost.Text = lostPacketCount.ToString();
                        }

                    }
                    else
                    {                                                       //If there is no rollover check if we lost a packet 
                        if (newPacketNumber != oldPacketNumber + 1)         //Keep track of previous packet and if it's diff from new lost packet
                        {
                            lostPacketCount++;
                            text_packetLost.Text = lostPacketCount.ToString();
                        }
                    }
                }
                oldPacketNumber = newPacketNumber;
            }
        }

        private void displaySolarData(string text)
        {
            //Display data for solar
            solarcalc.ParseSolarData(text);
            text_solarVoltage.Text = solarcalc.GetVoltage(solarcalc.analogVoltage[0]);
            text_batteryVoltage.Text = solarcalc.GetVoltage(solarcalc.analogVoltage[2]);
            text_batteryCurrent.Text = solarcalc.GetCurrent(solarcalc.analogVoltage[1], solarcalc.analogVoltage[2]);
            text_ledCurrent1.Text = solarcalc.LEDCurrent(solarcalc.analogVoltage[1], solarcalc.analogVoltage[4]);
            text_ledCurrent2.Text = solarcalc.LEDCurrent(solarcalc.analogVoltage[1], solarcalc.analogVoltage[3]);
        }

        private void butt_OpenClose_Click(object sender, RoutedEventArgs e)
        {
            if (!bPortOpen)
            {
                //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient.receive?view=net-6.0
                //Creates a UdpClient for reading incoming data.
                UdpClient udpclient = new UdpClient(11000);

                //Creates an IPEndPoint to record the IP Address and port number of the sender.
                // The IPEndPoint will allow you to read datagrams sent from any source.
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpclient.Receive(ref RemoteIpEndPoint);

                    text = Encoding.ASCII.GetString(receiveBytes);

                    //Need to save string from packet into text variable 
                    text = text.ToString();

                    if (text_packetReceived.Dispatcher.CheckAccess())     //If we have access to the thread then update the ui
                    {
                        UpdateUI(text);
                    }
                    else
                    {                                               //If we do not have access to the thread
                        text_packetReceived.Dispatcher.Invoke(() => { UpdateUI(text); });
                    }

                    text_IP.Text = RemoteIpEndPoint.Address.ToString();
                    text_PORT.Text = RemoteIpEndPoint.Port.ToString();
                }
                catch (Exception ef)
                {
                    Console.WriteLine(ef.ToString());
                }

                butt_OpenClose.Content = "Close";           //Rename the content in the button to close because it is open now
                bPortOpen = true;                           //Restate our bool to true now
            }
            else
            {
                               //Close the port using seriaport
                butt_OpenClose.Content = "Open";            //Change button content to open now that its closed
                bPortOpen = false;                          //Restate our bool 
            }
        }

        public static void Parse(string ipAddress)
        {
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.ipaddress.parse?view=net-6.0
            try
            {
                // Create an instance of IPAddress for the specified address string (in
                // dotted-quad, or colon-hexadecimal notation).
                IPAddress address = IPAddress.Parse(ipAddress);

                // Display the address in standard notation.
                Console.WriteLine("Parsing your input string: " + "\"" + ipAddress + "\"" + " produces this address (shown in its standard notation): " + address.ToString());
            }

            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }

            catch (FormatException e)
            {
                Console.WriteLine("FormatException caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }

            catch (Exception e)
            {
                Console.WriteLine("Exception caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }
        }

        private void butt_Clear_Click(object sender, RoutedEventArgs e)
        {
            text_packetReceived.Text = ""; //Clear the packet to nothing
        }

        private void butt_Send_Click(object sender, RoutedEventArgs e)
        {
            sendPacket();
            text_Send.Text = "Sending it";
        }

        private void sendPacket()
        {
            try
            {
                for (int i = 3; i < 7; i++)
                {
                    txCheckSum += (byte)stringBuilder[i];                   //Add up the bytes that were passed to string builder
                }
                txCheckSum %= 1000;

                stringBuilder.Remove(7, 3);                                 //Remove the check sum at index 7 size 3
                stringBuilder.Insert(7, txCheckSum.ToString("D3"));         //Add D3 to make sure theres the right number of digits
                text_Send.Text = stringBuilder.ToString();

                string messageOut = stringBuilder.ToString();               //Read the packet in the box
                messageOut += "\r\n";                                       //Add a carriage and line return to message
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageOut);   //Convert to a byte array
                //serialport.Write(messageBytes, 0, messageBytes.Length);     //Write the bytes to the serial port to send
                txCheckSum = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);                            //Throw an exception instead of crashing
            }
        }

        private void buttonClicked(int i)
        {
            Button[] butt_bit = new Button[] { butt_bit0, butt_bit1, butt_bit2, butt_bit3 };
            if (butt_bit[i].Content.ToString() == "0")
            {
                butt_bit[i].Content = "1";
                stringBuilder[i + 3] = '1';
            }
            else
            {
                butt_bit[i].Content = "0";
                stringBuilder[i + 3] = '0';
            }
            sendPacket();
        }
        private void butt_bit0_Click(object sender, RoutedEventArgs e)
        {
            buttonClicked(0);
        }
        private void butt_bit1_Click(object sender, RoutedEventArgs e)
        {
            buttonClicked(1);
        }
        private void butt_bit2_Click(object sender, RoutedEventArgs e)
        {
            buttonClicked(2);
        }
        private void butt_bit3_Click(object sender, RoutedEventArgs e)
        {
            buttonClicked(3);
        }
    }

    public class parseSubString
    {
        private int subStringLocation { get; set; }

        public parseSubString()
        {
            subStringLocation = 0; //Set the inital location within the string[]
        }

        public string parseString(string stringToParse, int numberOfChars)
        {   //Fill returnString with a subString of stringToParse, using a byte usually(numberofChars)
            string returnString = stringToParse.Substring(subStringLocation, numberOfChars);
            subStringLocation += numberOfChars; //Increment subStringLocation by the numberofchars each pass
            return returnString;
        }
    }
}

