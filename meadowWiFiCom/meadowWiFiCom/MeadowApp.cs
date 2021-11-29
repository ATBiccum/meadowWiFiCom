/*
 * 
 * Goal; get TCP protocol working over WiFi and receiving in a WPF application. 
 * Debug using ST7735 Display. 
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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace meadowWiFiCom
{
    public class MeadowApp : App<F7Micro, MeadowApp>
    {
        St7735 st7735;
        GraphicsLibrary graphics;

        //WiFi Information
        string SSID = "";
        string PASSWORD = "";

        public MeadowApp()
        {
            Initialize();
        }
        void Initialize()
        {
            var led = new RgbLed(Device,
                                Device.Pins.OnboardLedRed,
                                Device.Pins.OnboardLedGreen,
                                Device.Pins.OnboardLedBlue);
            led.SetColor(RgbLed.Colors.Red);            
            
            InitializeDisplay();
            InitializeWiFi().Wait();

            led.SetColor(RgbLed.Colors.Green);
        }

        void InitializeDisplay()
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
        }

        async Task InitializeWiFi()
        {
            graphics.DrawText(0, 0, "Connecting to WiFi...");
            
            Device.WiFiAdapter.WiFiConnected += WiFiAdapter_ConnectionCompleted;

            var connectionResult = await Device.WiFiAdapter.Connect(SSID, PASSWORD);

            if (connectionResult.ConnectionStatus != ConnectionStatus.Success)
            {
                throw new Exception($"Cannot connect to network: {connectionResult.ConnectionStatus}");
            }
        }

        private void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e)
        {
            graphics.DrawText(0, 0, "Connected.");
        }

        private void TCPclient(string message)
        {
        https://stackoverflow.com/questions/10182751/server-client-send-receive-simple-text
            const int PORT_NO = 5000;
            const string SERVER_IP = "127.0.0.1";
            static void Main(string[] args)
            {
                //---data to send to the server---
                string textToSend = DateTime.Now.ToString();

                //---create a TCPClient object at the IP and port no.---
                TcpClient client = new TcpClient(SERVER_IP, PORT_NO);
                NetworkStream nwStream = client.GetStream();
                byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(textToSend);

                //---send the text---
                Console.WriteLine("Sending : " + textToSend);
                nwStream.Write(bytesToSend, 0, bytesToSend.Length);

                //---read back the text---
                byte[] bytesToRead = new byte[client.ReceiveBufferSize];
                int bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
                Console.WriteLine("Received : " + Encoding.ASCII.GetString(bytesToRead, 0, bytesRead));
                Console.ReadLine();
                client.Close();
            }
    }
}
