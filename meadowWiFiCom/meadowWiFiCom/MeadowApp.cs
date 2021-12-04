/*
 * 
 * Goal; 
 *
 * Read analog pins and create packets to be sent.
 * Connect to WiFi and then to UDP server. 
 * Setup meadow board as UDP client to send and receive packets.
 * Parse received packets to turn on led's and graphics data.
 * 
 * 
 * Debugging 7735 screen not working now. Mono is running
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



        ////WiFi Information
        //private static string SSID = "";
        //private static string PASSWORD = "";

        ////UDP Server Information
        //private const int PORT_NO = 5000;
        //private const string SERVER_IP = "127.0.0.1";


        public MeadowApp()
        {
            Initialize();
        }
        public void Initialize()
        {
            RgbLed led = new RgbLed(Device,
                                    Device.Pins.OnboardLedRed,
                                    Device.Pins.OnboardLedGreen,
                                    Device.Pins.OnboardLedBlue);
            led.SetColor(RgbLed.Colors.Red);

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

            graphics.DrawText(5, 20, "Hello World!");

            graphics.Show();

            //InitializeWiFi().Wait();

            led.StartBlink(RgbLed.Colors.Green);
        }

        //async Task InitializeWiFi()
        //{
        //    //Display debug message on display and change board LED to blue
        //    graphics.Clear(true);
        //    graphics.DrawText(0, 5, "Connecting to WiFi...");
        //    graphics.Show();

        //    Device.WiFiAdapter.WiFiConnected += WiFiAdapter_ConnectionCompleted;     //Do this event once we are connected to WiFi

        //    var connectionResult = await Device.WiFiAdapter.Connect(SSID, PASSWORD); //Connect to WiFi, and save result

        //    if (connectionResult.ConnectionStatus != ConnectionStatus.Success)       //If not connected will throw error
        //    {
        //        graphics.Clear(true);
        //        graphics.DrawText(0, 5, "Problem Connecting.");
        //        graphics.Show();

        //        throw new Exception($"Cannot connect to network: {connectionResult.ConnectionStatus}");
        //    }
        //}

        //private void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e)
        //{
        //    graphics.Clear(true);
        //    graphics.DrawText(0, 5, "Connected.");
        //    graphics.Show();
        //}
    }
}
