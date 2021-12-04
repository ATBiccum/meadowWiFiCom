/* meadowWiFiCom Firmware
 * 
 * Anthony Biccum
 * ECET 230 Object Oriented Programming 
 * Camosun College
 * 
 * This is the firmware for the meadow board to communicate packets with my WPF UDP server. 
 * 
 * Read analog pins and create packets to be sent.
 * Connect to WiFi and then to UDP server. 
 * Setup meadow board as UDP client to send and receive packets.
 * Parse received packets to turn on led's and graphics data.
 * 
 * 
 * 
 */



using Meadow;
using Meadow.Devices;
using Meadow.Foundation;
using Meadow.Foundation.Displays.TftSpi;
using Meadow.Foundation.Graphics;
using Meadow.Foundation.Leds;
using Meadow.Gateway.WiFi;
using Meadow.Hardware;
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

        //WiFi Information
        private static string SSID = "TELUS0108";
        private static string PASSWORD = "kz9s7yhs3v";

        //UDP Server Information
        private const int PORT_NO = 0;
        private const string SERVER_IP = "";

        public MeadowApp()
        {
            //Initialize screen, wifi, and udp connection to server.
            Initialize();

            //Packet creation

        }
        private void Initialize()
        {
            RgbLed led = new RgbLed(Device,
                                    Device.Pins.OnboardLedRed,
                                    Device.Pins.OnboardLedGreen,
                                    Device.Pins.OnboardLedBlue);
            led.SetColor(RgbLed.Colors.Red);

            InitializeDisplay();

            InitializeWiFi().Wait();

            InitializeUDP();

            led.StartBlink(RgbLed.Colors.Green);
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
            graphics.DrawText(5, 20, "Welcome To");
            graphics.DrawText(5, 20, "Meadow WiFi!");
            graphics.Show();
        }

        async Task InitializeWiFi()
        {
            //Display debug message on display and change board LED to blue
            graphics.Clear(true);
            graphics.DrawText(5, 5, "Connecting...");
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
            graphics.DrawText(5, 5, "Connected.");
            graphics.Show();
        }

        private void InitializeUDP()
        {
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient?view=net-6.0
            udpclient = new UdpClient(PORT_NO);
            try
            {
                //Connect to our WPF server
                udpclient.Connect(SERVER_IP, PORT_NO);
                //Send a message that we have connected
                Byte[] sendBytes = Encoding.ASCII.GetBytes("Meadow has connected!");
                udpclient.Send(sendBytes, sendBytes.Length);

                graphics.Clear(true);
                graphics.DrawText(5, 5, "Connected To");
                graphics.DrawText(5, 5, "UDP Server");
                graphics.Show();

                //// Sends a message to the host to which you have connected.
                //Byte[] sendBytes = Encoding.ASCII.GetBytes("Meadow has connected!");

                //udpclient.Send(sendBytes, sendBytes.Length);

                ////IPEndPoint object will allow us to read datagrams sent from any source.
                //IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                //// Blocks until a message returns on this socket from a remote host.
                //Byte[] receiveBytes = udpclient.Receive(ref RemoteIpEndPoint);
                //string returnData = Encoding.ASCII.GetString(receiveBytes);

                //// Uses the IPEndPoint object to determine which of these two hosts responded.
                //Console.WriteLine("This is the message you received " +
                //                             returnData.ToString());

                //udpclient.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
