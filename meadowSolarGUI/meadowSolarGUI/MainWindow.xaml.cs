/* meadowSolarGUI Project
 * 
 * ECET 230
 * Camosun College
 * Tony Biccum
 *
 * This project creates a GUI for solar panel data received from the meadow board.
 * It communicates through packets and can send packets to the meadow to turn on LEDs over UDP. 
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
        private int checkSumError = 0;
        private int checkSumCalculated = 0;
        private int oldPacketNumber = -1;
        private int newPacketNumber = 0;
        private int lostPacketCount = 0;
        private int packetRollover = 0;
        private bool _backgroundworker = false;
        string temp;
        byte[] bytes = new byte[39];

        //Receiving IP Address (and IPEndPoint)
        private static string IPraw = "000.000.000.000";
        private readonly IPAddress IP = IPAddress.Parse(IPraw);
        //Receiving Port 2002
        private int PORT;
        //IPEndPoint Port
        private int PORTipep = 2003;
        /*******************************************/

        private readonly BackgroundWorker worker = new BackgroundWorker();
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
            text_checkSumError.Text = "0";
            text_packetRollover.Text = "0";
            text_packetLost.Text = "0";

            worker.DoWork += worker_DoWork;                     //Do work evetn, runs in the background
            worker.ProgressChanged += Worker_ProgressChanged;   //Progress changed event can be called within Do Work to report values
            worker.WorkerReportsProgress = true;                //Enables above event
            worker.WorkerSupportsCancellation = true;
        }

        /// <summary>
        /// Background worker tasks that allow UI to run seperately from
        /// receiving messages. 
        /// </summary>
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            //Run background tasks within this; this is where we handle UDP receiving
            UdpClient listener = new UdpClient(PORT);
            IPEndPoint groupEP = new IPEndPoint(IP, PORTipep);

            try
            {
                while (_backgroundworker)
                {
                    bytes = listener.Receive(ref groupEP);   //Listen for a message; this blocks until a message is received
                    temp = Encoding.ASCII.GetString(bytes);         //Convert to string to be used
                    worker.ReportProgress(1, temp);               //Send our value to the report progress event to update 
                    //System.Diagnostics.Debug.WriteLine(temp);     //Use for testing received message
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

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateUI(e.UserState.ToString());   //Receives the UDP packet from do work event
        }

        /// <summary>
        /// Event that receives the packet to be parsed and 
        /// where we update the UI with the parsed values. 
        /// </summary>
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

        /// <summary>
        /// Method to utalize the solarcalc class to calculate the solar values
        /// and display them in the UI.
        /// </summary>
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

        /// <summary>
        /// Button methods; most important is open/close that allows us to start and
        /// stop listening for a UDP message. (start/stop backgroundworker).
        /// Clear clears the packet received window.
        /// The two bit button correspond to LED's and will send a packet 
        /// to turn on a LED when pressed. 
        /// IP and PORT handlers for recording the inputed address. 
        /// </summary>
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

        private void text_IP_TextChanged(object sender, TextChangedEventArgs e)
        {
            IPraw = text_IP.Text.ToString();
        }

        private void text_PORT_TextChanged(object sender, TextChangedEventArgs e)
        {
            PORT = Convert.ToInt32(text_PORT.Text);
        }
    }

    /// <summary>
    /// Class for parsing and returning values of a string. 
    /// </summary>
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
