using System;
using System.Collections.Generic;
using System.Text;

namespace meadowSolarGUI
{
    class SolarCalc
    {
        //Field 
        private static double ResistorValue;
        private const int numberOfSamples = 5;
        private static int currentIndex;
        public double[] analogVoltage = new double[6];
        private double[,] slidingWindowVoltage = new double[6, numberOfSamples];


        //Constructor that takes no argument
        public SolarCalc()
        {
            ResistorValue = 100.0;
        }

        //Methods
        public void ParseSolarData(string newPacket)
        {
            for (int i = 0; i < 6; i++)
            {
                analogVoltage[i] = Convert.ToDouble(newPacket.Substring(6 + (i * 4), 4));
                //analogVoltage[i] = Convert.ToDouble(parseSubString.parseString(newPacket, 4));
                analogVoltage[i] = averageVoltage(analogVoltage[i], i);
            }
        }

        private double averageVoltage(double voltageToAverage, int indexOfAnalog)
        {
            double sum;

            if (currentIndex >= numberOfSamples)
            {
                currentIndex = 0;
            }
            slidingWindowVoltage[indexOfAnalog, currentIndex] = voltageToAverage;
            sum = 0;
            for (int i = 0; i < numberOfSamples; i++)
            {
                sum += slidingWindowVoltage[indexOfAnalog, i];
            }
            if (indexOfAnalog == 5)
            {
                currentIndex++;
            }
            return sum / numberOfSamples;
        }

        public string GetVoltage(double value)
        {
            double analogValue = value / 1000.0;
            return analogValue.ToString(" 0.0 V");
        }
        public string GetCurrent(double analogOne, double shuntResistor)
        {
            double temp = analogOne - shuntResistor;
            double shuntVoltage = temp / ResistorValue;
            return shuntVoltage.ToString(" 0.0 mA; -0.0 mA; 0.0 mA"); //If posisitive format like first, negative like second, zero like 3rd
        }

        public string LEDCurrent(double analogOne, double shuntResistor)
        {
            double value = (analogOne - shuntResistor) / ResistorValue;
            if (value < 0)
            {
                value = 0;
            }
            return value.ToString(" 0.0 mA; -0.0 mA; 0.0 mA"); //If posisitive format like first, negative like second, zero like 3rd
        }
    }
}