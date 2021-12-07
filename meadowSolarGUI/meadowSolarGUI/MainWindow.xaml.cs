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
 * Need a bool for listening and not listening upon clicking the open button.
 * Need event handler that activates when received a packet through IPEndPoint
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
using System.ComponentModel;

namespace meadowSolarGUI
{
    public partial class MainWindow : Window
    {
        /*****************Variables*****************/
        private bool bPortOpen = false;
        public string text;
        private int checkSumError = 0;
        private int checkSumCalculated = 0;
        private int oldPacketNumber = -1;
        private int newPacketNumber = 0;
        private int lostPacketCount = 0;
        private int packetRollover = 0;
        private int txCheckSum;
        private bool _backgroundworker = false;

        //Receiving IP Address (and IPEndPoint)
        private static string IPraw = "000.000.000.000";
        private readonly IPAddress IP = IPAddress.Parse(IPraw);
        //Receiving Port 2002
        private int PORT;
        //IPEndPoint Port
        private int PORTipep = 2003;
        /*******************************************/

        private readonly BackgroundWorker worker = new BackgroundWorker();
        private StringBuilder stringBuilder = new StringBuilder("###1111196");
        SolarCalc solarcalc = new SolarCalc();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }
        /// <summary>
        /// Runs when the main window is loaded and 
        /// we do some initialization of values and the backgroundworker.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Initialize values within the text boxes for formatting
            text_packetReceived.Text = "###0000000000000000000000000000000000";
            text_Send.Text = "###0000000";
            text_checkSumError.Text = "0";
            text_packetRollover.Text = "0";
            text_packetLost.Text = "0";

            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateUI(e.UserState.ToString());
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            //Run background tasks within this
            string temp;
            UdpClient listener = new UdpClient(PORT);
            IPEndPoint groupEP = new IPEndPoint(IP, PORTipep);
            
            try
            {
                while (_backgroundworker)
                {
                    byte[] bytes = listener.Receive(ref groupEP);
                    temp = Encoding.ASCII.GetString(bytes);
                    worker.ReportProgress(100, temp);
                    //System.Diagnostics.Debug.WriteLine(temp); //Use for testing received message
                }
            }
            catch (SocketException ef)
            {
                Console.WriteLine(ef);
            }
            finally
            {
                listener.Close();

            }
        }

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
                worker.RunWorkerAsync();                    //Start our background worker to start listening for UDP
                butt_OpenClose.Content = "Close";           //Rename the content in the button to close because it is open now
                bPortOpen = true;
                _backgroundworker = true;
            }
            else
            {
                worker.CancelAsync();
                butt_OpenClose.Content = "Open";            //Change button content to open now that its closed
                bPortOpen = false;
                _backgroundworker = false;
            }
        }

        private void butt_Clear_Click(object sender, RoutedEventArgs e)
        {
            text_packetReceived.Text = "###0000000000000000000000000000000000";
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

        private void text_IP_TextChanged(object sender, TextChangedEventArgs e)
        {
            IPraw = text_IP.Text.ToString();
        }

        private void text_PORT_TextChanged(object sender, TextChangedEventArgs e)
        {
            PORT = Convert.ToInt32(text_PORT.Text);
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

