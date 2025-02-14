﻿/* meadowWiFiCom Firmware!
 * 
 * Anthony Biccum
 * ECET 230 Object Oriented Programming 
 * Camosun College
 * 
 * This is the firmware for the meadow board to communicate packets with my WPF UDP server. 
 * Program originally written by Wayne Mayes I modified it to send over wifi instead of SPI. 
 */

using Meadow;
using Meadow.Devices;
using Meadow.Foundation;
using Meadow.Foundation.Displays.TftSpi;
using Meadow.Foundation.Graphics;
using Meadow.Foundation.Leds;
using Meadow.Gateway.WiFi;
using Meadow.Hardware;
using Meadow.Units;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace meadowWiFiCom
{
    public class MeadowApp : App<F7Micro, MeadowApp>
    {
        St7735 st7735;
        GraphicsLibrary graphics;
        UdpClient udpclient;

        private bool initComplete = false;

        //WiFi Information
        private static string SSID = "Bakery204";
        private static string PASSWORD = "Electronics@204CamosunCollege";

        //Meadow UDPClient on this port 
        private const int PORT_NO = 2001;

        //UDP Server Information
        private const int TOPORT = 2002;
        private const string SERVER_IP = "172.30.32.1";

        /**********************Packet Variables*********************/
        string milliVolts00, milliVolts01, milliVolts02, milliVolts03, milliVolts04, milliVolts05;

        IAnalogInputPort analogIn00;
        IAnalogInputPort analogIn01;
        IAnalogInputPort analogIn02;
        IAnalogInputPort analogIn03;
        IAnalogInputPort analogIn04;
        IAnalogInputPort analogIn05;

        IDigitalInputPort inputPortD02;
        IDigitalInputPort inputPortD03;
        IDigitalInputPort inputPortD04;
        IDigitalInputPort inputPortD05;
        IDigitalOutputPort outputPortD06;
        IDigitalOutputPort outputPortD07;
        IDigitalOutputPort outputPortD08;
        IDigitalOutputPort outputPortD09;

        private int packetNumber;
        private int milliVolts;
        private int analogPin;
        private int digitalPin;
        private int packetLen;
        private int analogReq = 6;// set to the Max number of analog pins required
        private int digitalInReq = 8;// set to the Max number of digital input pins required
        private int checkSum = 0;
        private int index = 0;
        private int TXstate = 0;
        private String outPacket = "###0000000000000000000000000000000000rn"; //###P##AN00AN01AN02AN03AN04AN05bbbbCHKrn
        Byte[] sendBytes = new byte[39];
        private int packetTime = 500;
        /**************************************************/

        public MeadowApp()
        {
            //Initialize screen, wifi, udp connection to server, and packet variables.
            if (!initComplete) //Only want to run our inits once as we will clog up the pipes if you know what i mean
            {
                Initialize();
            }
        }

        /// <summary>
        /// Method that gets run first and organizes all our inits. 
        /// Starts sending packets once these are complete. 
        /// </summary>
        private void Initialize()
        {
            RgbLed led = new RgbLed(Device,
                                    Device.Pins.OnboardLedRed,
                                    Device.Pins.OnboardLedGreen,
                                    Device.Pins.OnboardLedBlue);
            led.SetColor(RgbLed.Colors.Red);

            InitializeDisplay();

            InitializePackets();

            InitializeWiFi().Wait();
            Thread.Sleep(100);
            InitializeUDP();
            initComplete = true;
            //Start sending packets.
            led.StartBlink(RgbLed.Colors.Green);
            SendLoop();
        }

        private void InitializeDisplay()
        {
            var config = new SpiClockConfiguration(6000, SpiClockConfiguration.Mode.Mode3);
            st7735 = new St7735
            (
                device: Device,
                spiBus: Device.CreateSpiBus(Device.Pins.SCK,
                                            Device.Pins.MOSI,
                                            Device.Pins.MISO,
                                            config),
                chipSelectPin: Device.Pins.D02,
                dcPin: Device.Pins.D01,
                resetPin: Device.Pins.D00,
                width: 128,
                height: 160,
                St7735.DisplayType.ST7735R_BlackTab
            );
            graphics = new GraphicsLibrary(st7735);

            graphics.Clear(true);
            graphics.CurrentFont = new Font8x12();
            graphics.DrawText(5, 20, "Welcome");
            graphics.DrawText(15, 32, "To");
            graphics.DrawText(25, 44, "Meadow");
            graphics.DrawText(35, 56, "with WiFi!");
            graphics.Show();
            Thread.Sleep(100);
        }

        async Task InitializeWiFi()
        {
            graphics.Clear(true);
            graphics.DrawText(5, 20, "Connecting...");
            graphics.Show();

            Device.WiFiAdapter.WiFiConnected += WiFiAdapter_ConnectionCompleted;     //Do this event once we are connected to WiFi

            var connectionResult = await Device.WiFiAdapter.Connect(SSID, PASSWORD); //Connect to WiFi, and save result

            if (connectionResult.ConnectionStatus != ConnectionStatus.Success)       //If not connected will throw error
            {
                graphics.Clear(true);
                graphics.DrawText(5, 5, "Problem Connecting.");
                graphics.Show();

                throw new Exception($"Cannot connect to network: {connectionResult.ConnectionStatus}");
            }
        }

        private void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e)
        {
            graphics.Clear(true);
            graphics.DrawText(5, 20, "Connected.");
            graphics.Show();
        }

        private void InitializeUDP()
        {
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient?view=net-6.0
            udpclient = new UdpClient(PORT_NO);
            try
            {
                //Connect to our WPF server
                udpclient.Connect(SERVER_IP, TOPORT);
                //Send a message that we have connected
                Thread.Sleep(100);
                Byte[] sendBytes = Encoding.ASCII.GetBytes("Meadow has connected!");
                udpclient.Send(sendBytes, sendBytes.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void InitializePackets()
        {
            analogIn00 = Device.CreateAnalogInputPort(Device.Pins.A00);
            analogIn01 = Device.CreateAnalogInputPort(Device.Pins.A01);
            analogIn02 = Device.CreateAnalogInputPort(Device.Pins.A02);
            analogIn03 = Device.CreateAnalogInputPort(Device.Pins.A03);
            analogIn04 = Device.CreateAnalogInputPort(Device.Pins.A04);
            analogIn05 = Device.CreateAnalogInputPort(Device.Pins.A05);
            inputPortD02 = Device.CreateDigitalInputPort(Device.Pins.D02);
            inputPortD03 = Device.CreateDigitalInputPort(Device.Pins.D03);
            inputPortD04 = Device.CreateDigitalInputPort(Device.Pins.D04);
            inputPortD05 = Device.CreateDigitalInputPort(Device.Pins.D05);
            outputPortD06 = Device.CreateDigitalOutputPort(Device.Pins.D06, false);
            outputPortD07 = Device.CreateDigitalOutputPort(Device.Pins.D07, false);
            outputPortD08 = Device.CreateDigitalOutputPort(Device.Pins.D08, false);
            outputPortD09 = Device.CreateDigitalOutputPort(Device.Pins.D09, false);
            Thread.Sleep(200);
            outputPortD06.State = true;
            Thread.Sleep(200);
            outputPortD08.State = true;
            Thread.Sleep(200);
            outputPortD07.State = true;
            Thread.Sleep(200);
            outputPortD09.State = true;
            //serialPort = Device.CreateSerialPort(Device.SerialPortNames.Com1, 115200);
            //serialPort.Open();
            //serialPort.DataReceived += SerialPort_DataReceived;
            analogIn00.Updated += AnalogIn00_Updated;
            analogIn01.Updated += AnalogIn01_Updated;
            analogIn02.Updated += AnalogIn02_Updated;
            analogIn03.Updated += AnalogIn03_Updated;
            analogIn04.Updated += AnalogIn04_Updated;
            analogIn05.Updated += AnalogIn05_Updated;
            int timeSpanInMilliSec = 100;
            analogIn00.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn01.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn02.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn03.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn04.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn05.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn05.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
        }

        /// <summary>
        /// Methods to collect all of our analog data. 
        /// Converts them to a voltage as well. 
        /// </summary>
        private void AnalogIn05_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts05 = miliVolts.ToString("D4");
        }

        private void AnalogIn04_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts04 = miliVolts.ToString("D4");
        }

        private void AnalogIn03_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts03 = miliVolts.ToString("D4");
        }

        private void AnalogIn02_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts02 = miliVolts.ToString("D4");
        }

        private void AnalogIn01_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts01 = miliVolts.ToString("D4");
        }

        private void AnalogIn00_Updated(object sender, IChangeResult<Voltage> e)
        {
            int milliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts00 = milliVolts.ToString("D4");
        }

        /// <summary>
        /// Our main sending loop that builds the packets and then sends them over UDP.
        /// </summary>
        private void SendLoop()
        {
            while (true)
            {
                //Packet creation state machine format: "###P##AN00AN01AN02AN03AN04AN05bbbbCHKrn"
                switch (TXstate)
                {
                    case 0:
                        {
                            outPacket = "###";
                            checkSum = 0;
                            index = 0;
                            analogPin = 0;
                            digitalPin = digitalInReq - 1;              //Start with most significant pin 
                            outPacket += packetNumber++.ToString("D3"); //Increment packet number and add to outPacket string
                            packetNumber %= 1000;                       //Packetnumber rollover
                            TXstate = 1; //Move to next state
                            break;
                        }

                    case 1:
                        {
                            //Add our analog read values to the outpacket string
                            outPacket += milliVolts00 + milliVolts01 + milliVolts02 + milliVolts03 + milliVolts04 + milliVolts05;
                            TXstate = 2; //Move to next state
                            break;
                        }

                    case 2:
                        {
                            bool[] currentState = new bool[4];
                            currentState[0] = inputPortD02.State;
                            currentState[1] = inputPortD03.State;
                            currentState[2] = inputPortD04.State;
                            currentState[3] = inputPortD05.State;

                            foreach (bool state in currentState)
                            {
                                string outString = " ";
                                if (state)
                                {
                                    outString = "1";
                                }
                                else
                                {
                                    outString = "0";
                                }
                                outPacket += outString;
                            }
                            TXstate = 3; //Move to next state
                            break;
                        }

                    case 3:
                        {
                            for (int i = 3; i < outPacket.Length; i++)
                            {
                                checkSum += (byte)outPacket[i];     //Calculate the check sum
                            }
                            checkSum %= 1000;                       //Truncate the check sum to 3 digits
                            outPacket += checkSum.ToString("D3");   //Add it to the end of outpacket
                            outPacket += "\r\n";                    //Add a carriage return, and line feed
                            packetLen = outPacket.Length;           //Set packet length to send
                            TXstate = 4; //Move to next state
                            break;
                        }

                    case 4:
                        {
                            sendBytes = Encoding.ASCII.GetBytes(outPacket); //Encode in ASCII to send UDP

                            udpclient.Send(sendBytes, sendBytes.Length);           //Send the bytes to the connected UDP server
                            graphics.Clear(true);
                            graphics.DrawText(5, 70, "Sending Stuff!");
                            graphics.Show();
                            //if (sendBytes.Length > 38)
                            //{
                            //    udpclient.Send(sendBytes, sendBytes.Length);           //Send the bytes to the connected UDP server
                            //    graphics.Clear(true);
                            //    graphics.DrawText(5, 70, "Sending Stuff!");
                            //    graphics.Show();
                            //}
                            Thread.Sleep(packetTime);
                            TXstate = 0; //Reset to state 0
                            break;
                        }
                }
            }
        }
    }
}