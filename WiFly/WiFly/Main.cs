using System;
using System.IO;
using System.Threading;
using System.Text;
//using System.Collections;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.System;
using GHIElectronics.NETMF.Hardware;

using GHIElectronics.NETMF.FEZ;

namespace WiFly
{
    public class Shield
    {
        private static SPI spiInterface;
        private static InterruptPort irqPort;

        // Known WiFly Shield crystals
        public enum ClockRate
        {
            Xtal_12Mhz,
            Xtal_14MHz
        };

        // Available baud rates on the SC16C750
        public static int[] BaudRate =
        {
            1200,
            2400,
            4800,
            9600,
            19200,
            38400,
            57600,
            230400,
            460800,
            921600
        };

        #region Register Handlers
        enum Register
        {
            // General register set
            THR = 0x00 << 3,
            RHR = 0x00 << 3,
            IER = 0x01 << 3,
            FCR = 0x02 << 3,
            IIR = 0x02 << 3,
            LCR = 0x03 << 3,
            MCR = 0x04 << 3,
            LSR = 0x05 << 3,
            MSR = 0x06 << 3,
            SPR = 0x07 << 3,
            TCR = 0x06 << 3,
            TLR = 0x07 << 3,
            TXFIFO = 0x08 << 3,
            RXFIFO = 0x09 << 3,
            IODIR = 0x0A << 3,
            IOSTATE = 0x0B << 3,
            IOINTMSK = 0x0C << 3,
            IOCTRL = 0x0E << 3,
            EFCR = 0x0F << 3,
            // Special register set
            DLL = 0x00 << 3,
            DLH = 0x01 << 3,
            // Enhanced register set
            EFR = 0x02 << 3,
            XON1 = 0x04 << 3,
            XON2 = 0x05 << 3,
            XOFF1 = 0x06 << 3,
            XOFF2 = 0x07 << 3,
        }

        private static byte[] bytex2 = new byte[2];

        private static void WriteRegister(Register reg, byte b)
        {
            bytex2[0] = (byte)reg;  // Register address byte
            bytex2[1] = b;
            spiInterface.Write(bytex2);
        }

        private static byte ReadRegister(Register reg)
        {
            bytex2[0] = (byte)((byte)reg | 0x80);   // Top bit toggles reading
            bytex2[1] = 0;
            spiInterface.WriteRead(bytex2, bytex2);
            return bytex2[1];
        }
        #endregion

        #region Command handlers

        private static void WriteArray(byte[] ba)
        {
            for (int i = 0; i < ba.Length; i++)
                WriteRegister(Register.THR, ba[i]);
        }

        public static void SendString(string str)
        {
            byte[] ba = Encoding.UTF8.GetBytes(str);
            WriteArray(ba);
            Debug.Print(str);
        }

        public static void SendCommand(string command)
        {
            SendString(command);
            WriteRegister(Register.THR, (byte)'\r');
        }

        public static string ReadResponse()
        {
            string str = "";
/*
            // Await for something in the RX FIFO
            byte rxLvl = 0;
            do
            {
                rxLvl = ReadRegister(Register.RXFIFO);
            } while (rxLvl == 0);
*/
            // Read out of the RX FIFO
            while ((ReadRegister(Register.LSR) & 0x01) > 0)
            {
                byte b;
                b = ReadRegister(Register.RHR);
                if (b >= 0x20)
                    str += ((char)b);
            }
            return str;
        }

        #endregion

        #region IRQ Handlers
        private static Int32[] irqCase;

        private static void irqOnInterrupt(uint port, uint state, DateTime time)
        {
            byte[] iir = new byte [2];
            iir[0] = (byte)((byte)Register.IIR | 0x80);
            iir[1] = 0;
            spiInterface.WriteRead(iir, iir);

            Int32 code = iir[1] & 0x1f;
            if (code == irqCase[0]) { }
            else
            if (code == irqCase[1]) { }
            else
            if (code == irqCase[2]) { }
            else
            if (code == irqCase[3]) { }
            else
            if (code == irqCase[4]) { }
            else
            if (code == irqCase[5]) { }
            else
            if (code == irqCase[6]) { }
            else
            if (code == irqCase[7]) { }
            else
            if (code == irqCase[8]) { }

            // TODO Read appropriate register to acknowledge the IRQ
        }
        #endregion

        private static string currentError;

        public static string GetError()
        {
            return currentError;
        }

        public static bool Setup(
                                    Cpu.Pin         chipSelect, 
                                    SPI.SPI_module  spiModule, 
                                    ClockRate       clockRate, 
                                    short           baudRateIndex,
                                    bool            useIRQ)
        {
            if (baudRateIndex < 0 || baudRateIndex > 9)
            {
                currentError = "Invalid baud rate!!";
                return false;
            }

            #region SPI Init
            try
            {
                // Initialise SDI
                SPI.Configuration spiConfig = new SPI.Configuration(
                    chipSelect, // Chip select pin
                    false,      // Active state
                    10,         // Setup time (ms)
                    10,         // Hold time (ms)
                    false,      // Clock idle (low)
                    true,       // Edge (rising)
                    1024,       // Clock rate (KHz)
                    spiModule); // SPI module

                spiInterface = new SPI(spiConfig);
            }
            catch (Exception e)
            {
                currentError = "SPI Init Error (" + e.Message +")";
                return false;
            }
            #endregion

            #region SPI to WiFly UART Init
            // Initialise the WiFly shield SPI<->UART chip

            // Make DLL and DLH accessible
            WriteRegister(Register.LCR, 0x80);  // 0x80 to program baudrate
            if (clockRate == ClockRate.Xtal_12Mhz)
            {
                // A 12MHz clock can not do 921600bps
                if (BaudRate[baudRateIndex] == 921600)
                    baudRateIndex--;

                int divisor = 12288000 / (BaudRate[baudRateIndex] * 16);
                WriteRegister(Register.DLH, (byte)((divisor & 0x0000ff00) >> 8));
                WriteRegister(Register.DLL, (byte)((divisor & 0x000000ff) >> 0));
            }
            else
            {
                int divisor = 14745600 / (BaudRate[baudRateIndex] * 16);
                WriteRegister(Register.DLH, (byte)((divisor & 0x0000ff00) >> 8));
                WriteRegister(Register.DLL, (byte)((divisor & 0x000000ff) >> 0));
            }

            // Note: No HW flow control via MCR register on SC16C750, have to use EFR to enable it
            // Make EFR accessible
            WriteRegister(Register.LCR, 0xBF);  // access EFR register
            WriteRegister(Register.EFR, 0x10);  // enable enhanced functions (IER[7:4], FCR[5:4], and MCR[7:5])

            // Finally setup LCR
            WriteRegister(Register.LCR, 0x03);  // no parity, 1 stop bit, 8 data bit
            WriteRegister(Register.FCR, 0x06);  // reset TX and RX FIFO
            WriteRegister(Register.FCR, 0x01);  // enable FIFO mode

            // Perform read/write test of scratchpad register to check if UART is working
            WriteRegister(Register.SPR, 0x55);
            byte data = ReadRegister(Register.SPR);

            if (data != 0x55)
            {
                currentError = "Failed to init SPI<->UART chip";
                return false;
            }

            if (useIRQ)
            {
                try
                {
                    // Di7 connects to the /IRQ pin on the SC16C750
                    irqPort = new InterruptPort(
                        (Cpu.Pin)FEZ_Pin.Interrupt.Di7,
                        true,
                        Port.ResistorMode.PullUp,
                        Port.InterruptMode.InterruptEdgeLevelLow);

                    // Defined in order of priority
                    irqCase = new Int32[9];
                    irqCase[0] = Convert.ToInt32("000001", 2);  // None
                    irqCase[1] = Convert.ToInt32("000110", 2);  // RX FIFO error
                    irqCase[2] = Convert.ToInt32("001100", 2);  // RX time-out
                    irqCase[3] = Convert.ToInt32("000100", 2);  // RHR interrupt
                    irqCase[4] = Convert.ToInt32("000010", 2);  // THR interrupt
                    irqCase[5] = Convert.ToInt32("000000", 2);  // Modem status
                    irqCase[6] = Convert.ToInt32("110000", 2);  // I/O pins
                    irqCase[7] = Convert.ToInt32("010000", 2);  // Xoff interrupt
                    irqCase[8] = Convert.ToInt32("100000", 2);  // /CTS or /RTS

                    irqPort.OnInterrupt += new NativeEventHandler(irqOnInterrupt);
                }
                catch (Exception e)
                {
                    currentError = "IRQ Init Error (" + e.Message + ")";
                    return false;
                }
            }
            #endregion

            // Enter command mode

            //"$$$" responds with "CMD" and wait 250ms before sending more data
            SendString("$$$");
            Thread.Sleep(300);//ms

            //"reboot" wait for response to clear
            SendCommand("reboot");
            while (true)
            {
                string response = ReadResponse();

                if (response != "")
                  Debug.Print(response);
            }
            Thread.Sleep(600);

            currentError = "";
            return true;
        }

    }
}
