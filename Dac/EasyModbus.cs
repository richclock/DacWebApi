using System.Runtime.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using System.Net.NetworkInformation;

namespace EasyModbus
{
    public partial class ModbusClient
    {
        public enum RegisterOrder { LowHigh = 0, HighLow = 1 };
        private bool debug = false;
        private TcpClient tcpClient;
        private string ipAddress = "127.0.0.1";
        private int port = 502;
        private uint transactionIdentifierInternal = 0;
        private byte[] transactionIdentifier = new byte[2];
        private byte[] protocolIdentifier = new byte[2];
        private byte[] crc = new byte[2];
        private byte[] length = new byte[2];
        private byte unitIdentifier = 0x01;
        private byte functionCode;
        private byte[] startingAddress = new byte[2];
        private byte[] quantity = new byte[2];
        private bool udpFlag = false;
        private int portOut;
        private int baudRate = 9600;
        private int connectTimeout = 1000;
        public byte[] receiveData;
        public byte[] sendData;
        private SerialPort serialport;
        private Parity parity = Parity.Even;
        private StopBits stopBits = StopBits.One;
        private bool connected = false;
        public int NumberOfRetries { get; set; } = 3;
        private int countRetries = 0;

        public delegate void ReceiveDataChangedHandler(object sender);
        public event ReceiveDataChangedHandler ReceiveDataChanged;

        public delegate void SendDataChangedHandler(object sender);
        public event SendDataChangedHandler SendDataChanged;

        public delegate void ConnectedChangedHandler(object sender);
        public event ConnectedChangedHandler ConnectedChanged;

        NetworkStream stream;

        /// <summary>
        /// Constructor which determines the Master ip-address and the Master Port.
        /// </summary>
        /// <param name="ipAddress">IP-Address of the Master device</param>
        /// <param name="port">Listening port of the Master device (should be 502)</param>
        public ModbusClient(string ipAddress, int port)
        {
            if (debug) StoreLogData.Instance.Store("EasyModbus library initialized for Modbus-TCP, IPAddress: " + ipAddress + ", Port: " + port, System.DateTime.Now);
#if (!COMMERCIAL)
            //Console.WriteLine("EasyModbus Client Library Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            //Console.WriteLine("Copyright (c) Stefan Rossmann Engineering Solutions");
            //Console.WriteLine();
#endif
            this.ipAddress = ipAddress;
            this.port = port;
        }

        /// <summary>
        /// Constructor which determines the Serial-Port
        /// </summary>
        /// <param name="serialPort">Serial-Port Name e.G. "COM1"</param>
        public ModbusClient(string serialPort)
        {
            if (debug) StoreLogData.Instance.Store("EasyModbus library initialized for Modbus-RTU, COM-Port: " + serialPort, System.DateTime.Now);
#if (!COMMERCIAL)
            //Console.WriteLine("EasyModbus Client Library Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            //Console.WriteLine("Copyright (c) Stefan Rossmann Engineering Solutions");
            //Console.WriteLine();
#endif
            this.serialport = new SerialPort();
            serialport.PortName = serialPort;
            serialport.BaudRate = baudRate;
            serialport.Parity = parity;
            serialport.StopBits = stopBits;
            serialport.WriteTimeout = 10000;
            serialport.ReadTimeout = connectTimeout;

            serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public ModbusClient()
        {
            if (debug) StoreLogData.Instance.Store("EasyModbus library initialized for Modbus-TCP", System.DateTime.Now);
#if (!COMMERCIAL)
            //Console.WriteLine("EasyModbus Client Library Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            //Console.WriteLine("Copyright (c) Stefan Rossmann Engineering Solutions");
            //Console.WriteLine();
#endif
        }

        /// <summary>
        /// Establish connection to Master device in case of Modbus TCP. Opens COM-Port in case of Modbus RTU
        /// </summary>
        public void Connect()
        {
            if (serialport != null)
            {
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("Open Serial port " + serialport.PortName, System.DateTime.Now);
                    serialport.BaudRate = baudRate;
                    serialport.Parity = parity;
                    serialport.StopBits = stopBits;
                    serialport.WriteTimeout = 10000;
                    serialport.ReadTimeout = connectTimeout;
                    serialport.Open();
                    connected = true;


                }
                if (ConnectedChanged != null)
                    try
                    {
                        ConnectedChanged(this);
                    }
                    catch
                    {

                    }
                return;
            }
            if (!udpFlag)
            {
                if (debug) StoreLogData.Instance.Store("Open TCP-Socket, IP-Address: " + ipAddress + ", Port: " + port, System.DateTime.Now);
                tcpClient = new TcpClient();
                var result = tcpClient.BeginConnect(ipAddress, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(connectTimeout);
                if (!success)
                {
                    throw new EasyModbus.Exceptions.ConnectionException("connection timed out");
                }
                tcpClient.EndConnect(result);

                //tcpClient = new TcpClient(ipAddress, port);
                stream = tcpClient.GetStream();
                stream.ReadTimeout = connectTimeout;
                connected = true;
            }
            else
            {
                tcpClient = new TcpClient();
                connected = true;
            }
            if (ConnectedChanged != null)
                try
                {
                    ConnectedChanged(this);
                }
                catch
                {

                }
        }

        /// <summary>
        /// Establish connection to Master device in case of Modbus TCP.
        /// </summary>
        public void Connect(string ipAddress, int port)
        {
            if (!udpFlag)
            {
                if (debug) StoreLogData.Instance.Store("Open TCP-Socket, IP-Address: " + ipAddress + ", Port: " + port, System.DateTime.Now);
                tcpClient = new TcpClient();
                var result = tcpClient.BeginConnect(ipAddress, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(connectTimeout);
                if (!success)
                {
                    throw new EasyModbus.Exceptions.ConnectionException("connection timed out");
                }
                tcpClient.EndConnect(result);

                //tcpClient = new TcpClient(ipAddress, port);
                stream = tcpClient.GetStream();
                stream.ReadTimeout = connectTimeout;
                connected = true;
            }
            else
            {
                tcpClient = new TcpClient();
                connected = true;
            }

            if (ConnectedChanged != null)
                ConnectedChanged(this);
        }

        /// <summary>
        /// Converts two ModbusRegisters to Float - Example: EasyModbus.ModbusClient.ConvertRegistersToFloat(modbusClient.ReadHoldingRegisters(19,2))
        /// </summary>
        /// <param name="registers">Two Register values received from Modbus</param>
        /// <returns>Connected float value</returns>
        public static float ConvertRegistersToFloat(int[] registers)
        {
            if (registers.Length != 2)
                throw new ArgumentException("Input Array length invalid - Array langth must be '2'");
            int highRegister = registers[1];
            int lowRegister = registers[0];
            byte[] highRegisterBytes = BitConverter.GetBytes(highRegister);
            byte[] lowRegisterBytes = BitConverter.GetBytes(lowRegister);
            byte[] floatBytes = {
                                    lowRegisterBytes[0],
                                    lowRegisterBytes[1],
                                    highRegisterBytes[0],
                                    highRegisterBytes[1]
                                };
            return BitConverter.ToSingle(floatBytes, 0);
        }

        /// <summary>
        /// Converts two ModbusRegisters to Float, Registers can by swapped
        /// </summary>
        /// <param name="registers">Two Register values received from Modbus</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Connected float value</returns>
        public static float ConvertRegistersToFloat(int[] registers, RegisterOrder registerOrder)
        {
            int[] swappedRegisters = { registers[0], registers[1] };
            if (registerOrder == RegisterOrder.HighLow)
                swappedRegisters = new int[] { registers[1], registers[0] };
            return ConvertRegistersToFloat(swappedRegisters);
        }

        /// <summary>
        /// Converts two ModbusRegisters to 32 Bit Integer value
        /// </summary>
        /// <param name="registers">Two Register values received from Modbus</param>
        /// <returns>Connected 32 Bit Integer value</returns>
        public static Int32 ConvertRegistersToInt(int[] registers)
        {
            if (registers.Length != 2)
                throw new ArgumentException("Input Array length invalid - Array langth must be '2'");
            int highRegister = registers[1];
            int lowRegister = registers[0];
            byte[] highRegisterBytes = BitConverter.GetBytes(highRegister);
            byte[] lowRegisterBytes = BitConverter.GetBytes(lowRegister);
            byte[] doubleBytes = {
                                    lowRegisterBytes[0],
                                    lowRegisterBytes[1],
                                    highRegisterBytes[0],
                                    highRegisterBytes[1]
                                };
            return BitConverter.ToInt32(doubleBytes, 0);
        }

        /// <summary>
        /// Converts two ModbusRegisters to 32 Bit Integer Value - Registers can be swapped
        /// </summary>
        /// <param name="registers">Two Register values received from Modbus</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Connecteds 32 Bit Integer value</returns>
        public static Int32 ConvertRegistersToInt(int[] registers, RegisterOrder registerOrder)
        {
            int[] swappedRegisters = { registers[0], registers[1] };
            if (registerOrder == RegisterOrder.HighLow)
                swappedRegisters = new int[] { registers[1], registers[0] };
            return ConvertRegistersToInt(swappedRegisters);
        }

        /// <summary>
        /// Convert four 16 Bit Registers to 64 Bit Integer value Register Order "LowHigh": Reg0: Low Word.....Reg3: High Word, "HighLow": Reg0: High Word.....Reg3: Low Word
        /// </summary>
        /// <param name="registers">four Register values received from Modbus</param>
        /// <returns>64 bit value</returns>
        public static Int64 ConvertRegistersToLong(int[] registers)
        {
            if (registers.Length != 4)
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            int highRegister = registers[3];
            int highLowRegister = registers[2];
            int lowHighRegister = registers[1];
            int lowRegister = registers[0];
            byte[] highRegisterBytes = BitConverter.GetBytes(highRegister);
            byte[] highLowRegisterBytes = BitConverter.GetBytes(highLowRegister);
            byte[] lowHighRegisterBytes = BitConverter.GetBytes(lowHighRegister);
            byte[] lowRegisterBytes = BitConverter.GetBytes(lowRegister);
            byte[] longBytes = {
                                    lowRegisterBytes[0],
                                    lowRegisterBytes[1],
                                    lowHighRegisterBytes[0],
                                    lowHighRegisterBytes[1],
                                    highLowRegisterBytes[0],
                                    highLowRegisterBytes[1],
                                    highRegisterBytes[0],
                                    highRegisterBytes[1]
                                };
            return BitConverter.ToInt64(longBytes, 0);
        }

        /// <summary>
        /// Convert four 16 Bit Registers to 64 Bit Integer value - Registers can be swapped
        /// </summary>
        /// <param name="registers">four Register values received from Modbus</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Connected 64 Bit Integer value</returns>
        public static Int64 ConvertRegistersToLong(int[] registers, RegisterOrder registerOrder)
        {
            if (registers.Length != 4)
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            int[] swappedRegisters = { registers[0], registers[1], registers[2], registers[3] };
            if (registerOrder == RegisterOrder.HighLow)
                swappedRegisters = new int[] { registers[3], registers[2], registers[1], registers[0] };
            return ConvertRegistersToLong(swappedRegisters);
        }

        /// <summary>
        /// Convert four 16 Bit Registers to 64 Bit double prec. value Register Order "LowHigh": Reg0: Low Word.....Reg3: High Word, "HighLow": Reg0: High Word.....Reg3: Low Word
        /// </summary>
        /// <param name="registers">four Register values received from Modbus</param>
        /// <returns>64 bit value</returns>
        public static double ConvertRegistersToDouble(int[] registers)
        {
            if (registers.Length != 4)
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            int highRegister = registers[3];
            int highLowRegister = registers[2];
            int lowHighRegister = registers[1];
            int lowRegister = registers[0];
            byte[] highRegisterBytes = BitConverter.GetBytes(highRegister);
            byte[] highLowRegisterBytes = BitConverter.GetBytes(highLowRegister);
            byte[] lowHighRegisterBytes = BitConverter.GetBytes(lowHighRegister);
            byte[] lowRegisterBytes = BitConverter.GetBytes(lowRegister);
            byte[] longBytes = {
                                    lowRegisterBytes[0],
                                    lowRegisterBytes[1],
                                    lowHighRegisterBytes[0],
                                    lowHighRegisterBytes[1],
                                    highLowRegisterBytes[0],
                                    highLowRegisterBytes[1],
                                    highRegisterBytes[0],
                                    highRegisterBytes[1]
                                };
            return BitConverter.ToDouble(longBytes, 0);
        }

        /// <summary>
        /// Convert four 16 Bit Registers to 64 Bit double prec. value - Registers can be swapped
        /// </summary>
        /// <param name="registers">four Register values received from Modbus</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Connected double prec. float value</returns>
        public static double ConvertRegistersToDouble(int[] registers, RegisterOrder registerOrder)
        {
            if (registers.Length != 4)
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            int[] swappedRegisters = { registers[0], registers[1], registers[2], registers[3] };
            if (registerOrder == RegisterOrder.HighLow)
                swappedRegisters = new int[] { registers[3], registers[2], registers[1], registers[0] };
            return ConvertRegistersToDouble(swappedRegisters);
        }

        /// <summary>
        /// Converts float to two ModbusRegisters - Example:  modbusClient.WriteMultipleRegisters(24, EasyModbus.ModbusClient.ConvertFloatToTwoRegisters((float)1.22));
        /// </summary>
        /// <param name="floatValue">Float value which has to be converted into two registers</param>
        /// <returns>Register values</returns>
        public static int[] ConvertFloatToRegisters(float floatValue)
        {
            byte[] floatBytes = BitConverter.GetBytes(floatValue);
            byte[] highRegisterBytes =
            {
                floatBytes[2],
                floatBytes[3],
                0,
                0
            };
            byte[] lowRegisterBytes =
            {

                floatBytes[0],
                floatBytes[1],
                0,
                0
            };
            int[] returnValue =
            {
                BitConverter.ToInt32(lowRegisterBytes,0),
                BitConverter.ToInt32(highRegisterBytes,0)
            };
            return returnValue;
        }

        /// <summary>
        /// Converts float to two ModbusRegisters Registers - Registers can be swapped
        /// </summary>
        /// <param name="floatValue">Float value which has to be converted into two registers</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Register values</returns>
        public static int[] ConvertFloatToRegisters(float floatValue, RegisterOrder registerOrder)
        {
            int[] registerValues = ConvertFloatToRegisters(floatValue);
            int[] returnValue = registerValues;
            if (registerOrder == RegisterOrder.HighLow)
                returnValue = new Int32[] { registerValues[1], registerValues[0] };
            return returnValue;
        }

        /// <summary>
        /// Converts 32 Bit Value to two ModbusRegisters
        /// </summary>
        /// <param name="intValue">Int value which has to be converted into two registers</param>
        /// <returns>Register values</returns>
        public static int[] ConvertIntToRegisters(Int32 intValue)
        {
            byte[] doubleBytes = BitConverter.GetBytes(intValue);
            byte[] highRegisterBytes =
            {
                doubleBytes[2],
                doubleBytes[3],
                0,
                0
            };
            byte[] lowRegisterBytes =
            {

                doubleBytes[0],
                doubleBytes[1],
                0,
                0
            };
            int[] returnValue =
            {
                BitConverter.ToInt32(lowRegisterBytes,0),
                BitConverter.ToInt32(highRegisterBytes,0)
            };
            return returnValue;
        }

        /// <summary>
        /// Converts 32 Bit Value to two ModbusRegisters Registers - Registers can be swapped
        /// </summary>
        /// <param name="intValue">Double value which has to be converted into two registers</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Register values</returns>
        public static int[] ConvertIntToRegisters(Int32 intValue, RegisterOrder registerOrder)
        {
            int[] registerValues = ConvertIntToRegisters(intValue);
            int[] returnValue = registerValues;
            if (registerOrder == RegisterOrder.HighLow)
                returnValue = new Int32[] { registerValues[1], registerValues[0] };
            return returnValue;
        }

        /// <summary>
        /// Converts 64 Bit Value to four ModbusRegisters
        /// </summary>
        /// <param name="longValue">long value which has to be converted into four registers</param>
        /// <returns>Register values</returns>
        public static int[] ConvertLongToRegisters(Int64 longValue)
        {
            byte[] longBytes = BitConverter.GetBytes(longValue);
            byte[] highRegisterBytes =
            {
                longBytes[6],
                longBytes[7],
                0,
                0
            };
            byte[] highLowRegisterBytes =
            {
                longBytes[4],
                longBytes[5],
                0,
                0
            };
            byte[] lowHighRegisterBytes =
            {
                longBytes[2],
                longBytes[3],
                0,
                0
            };
            byte[] lowRegisterBytes =
            {

                longBytes[0],
                longBytes[1],
                0,
                0
            };
            int[] returnValue =
            {
                BitConverter.ToInt32(lowRegisterBytes,0),
                BitConverter.ToInt32(lowHighRegisterBytes,0),
                BitConverter.ToInt32(highLowRegisterBytes,0),
                BitConverter.ToInt32(highRegisterBytes,0)
            };
            return returnValue;
        }

        /// <summary>
        /// Converts 64 Bit Value to four ModbusRegisters - Registers can be swapped
        /// </summary>
        /// <param name="longValue">long value which has to be converted into four registers</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Register values</returns>
        public static int[] ConvertLongToRegisters(Int64 longValue, RegisterOrder registerOrder)
        {
            int[] registerValues = ConvertLongToRegisters(longValue);
            int[] returnValue = registerValues;
            if (registerOrder == RegisterOrder.HighLow)
                returnValue = new int[] { registerValues[3], registerValues[2], registerValues[1], registerValues[0] };
            return returnValue;
        }

        /// <summary>
        /// Converts 64 Bit double prec Value to four ModbusRegisters
        /// </summary>
        /// <param name="doubleValue">double value which has to be converted into four registers</param>
        /// <returns>Register values</returns>
        public static int[] ConvertDoubleToRegisters(double doubleValue)
        {
            byte[] doubleBytes = BitConverter.GetBytes(doubleValue);
            byte[] highRegisterBytes =
            {
                doubleBytes[6],
                doubleBytes[7],
                0,
                0
            };
            byte[] highLowRegisterBytes =
            {
                doubleBytes[4],
                doubleBytes[5],
                0,
                0
            };
            byte[] lowHighRegisterBytes =
            {
                doubleBytes[2],
                doubleBytes[3],
                0,
                0
            };
            byte[] lowRegisterBytes =
            {

                doubleBytes[0],
                doubleBytes[1],
                0,
                0
            };
            int[] returnValue =
            {
                BitConverter.ToInt32(lowRegisterBytes,0),
                BitConverter.ToInt32(lowHighRegisterBytes,0),
                BitConverter.ToInt32(highLowRegisterBytes,0),
                BitConverter.ToInt32(highRegisterBytes,0)
            };
            return returnValue;
        }

        /// <summary>
        /// Converts 64 Bit double prec. Value to four ModbusRegisters - Registers can be swapped
        /// </summary>
        /// <param name="doubleValue">double value which has to be converted into four registers</param>
        /// <param name="registerOrder">Desired Word Order (Low Register first or High Register first</param>
        /// <returns>Register values</returns>
        public static int[] ConvertDoubleToRegisters(double doubleValue, RegisterOrder registerOrder)
        {
            int[] registerValues = ConvertDoubleToRegisters(doubleValue);
            int[] returnValue = registerValues;
            if (registerOrder == RegisterOrder.HighLow)
                returnValue = new int[] { registerValues[3], registerValues[2], registerValues[1], registerValues[0] };
            return returnValue;
        }

        /// <summary>
        /// Converts 16 - Bit Register values to String
        /// </summary>
        /// <param name="registers">Register array received via Modbus</param>
        /// <param name="offset">First Register containing the String to convert</param>
        /// <param name="stringLength">number of characters in String (must be even)</param>
        /// <returns>Converted String</returns>
        public static string ConvertRegistersToString(int[] registers, int offset, int stringLength)
        {
            byte[] result = new byte[stringLength];
            byte[] registerResult = new byte[2];

            for (int i = 0; i < stringLength / 2; i++)
            {
                registerResult = BitConverter.GetBytes(registers[offset + i]);
                result[i * 2] = registerResult[0];
                result[i * 2 + 1] = registerResult[1];
            }
            return System.Text.Encoding.Default.GetString(result);
        }

        /// <summary>
        /// Converts a String to 16 - Bit Registers
        /// </summary>
        /// <param name="registers">Register array received via Modbus</param>
        /// <returns>Converted String</returns>
        public static int[] ConvertStringToRegisters(string stringToConvert)
        {
            byte[] array = System.Text.Encoding.ASCII.GetBytes(stringToConvert);
            int[] returnarray = new int[stringToConvert.Length / 2 + stringToConvert.Length % 2];
            for (int i = 0; i < returnarray.Length; i++)
            {
                returnarray[i] = array[i * 2];
                if (i * 2 + 1 < array.Length)
                {
                    returnarray[i] = returnarray[i] | ((int)array[i * 2 + 1] << 8);
                }
            }
            return returnarray;
        }


        /// <summary>
        /// Calculates the CRC16 for Modbus-RTU
        /// </summary>
        /// <param name="data">Byte buffer to send</param>
        /// <param name="numberOfBytes">Number of bytes to calculate CRC</param>
        /// <param name="startByte">First byte in buffer to start calculating CRC</param>
        public static UInt16 calculateCRC(byte[] data, UInt16 numberOfBytes, int startByte)
        {
            byte[] auchCRCHi = {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
            0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
            0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81,
            0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
            0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
            0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
            0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
            0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
            0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
            0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40
            };

            byte[] auchCRCLo = {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4,
            0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
            0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD,
            0x1D, 0x1C, 0xDC, 0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
            0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7,
            0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
            0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE,
            0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
            0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2,
            0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
            0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB,
            0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
            0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0, 0x50, 0x90, 0x91,
            0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
            0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88,
            0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80,
            0x40
            };
            UInt16 usDataLen = numberOfBytes;
            byte uchCRCHi = 0xFF;
            byte uchCRCLo = 0xFF;
            int i = 0;
            int uIndex;
            while (usDataLen > 0)
            {
                usDataLen--;
                if ((i + startByte) < data.Length)
                {
                    uIndex = uchCRCLo ^ data[i + startByte];
                    uchCRCLo = (byte)(uchCRCHi ^ auchCRCHi[uIndex]);
                    uchCRCHi = auchCRCLo[uIndex];
                }
                i++;
            }
            return (UInt16)((UInt16)uchCRCHi << 8 | uchCRCLo);
        }

        private bool dataReceived = false;
        private bool receiveActive = false;
        private byte[] readBuffer = new byte[256];
        private int bytesToRead = 0;
        private int akjjjctualPositionToRead = 0;
        DateTime dateTimeLastRead;
        /*
                private void DataReceivedHandler(object sender,
                                SerialDataReceivedEventArgs e)
                {
                    long ticksWait = TimeSpan.TicksPerMillisecond * 2000;
                    SerialPort sp = (SerialPort)sender;

                    if (bytesToRead == 0 || sp.BytesToRead == 0)
                    {
                        actualPositionToRead = 0;
                        sp.DiscardInBuffer();
                        dataReceived = false;
                        receiveActive = false;
                        return;
                    }

                    if (actualPositionToRead == 0 && !dataReceived)
                        readBuffer = new byte[256];

                    //if ((DateTime.Now.Ticks - dateTimeLastRead.Ticks) > ticksWait)
                    //{
                    //    readBuffer = new byte[256];
                    //    actualPositionToRead = 0;
                    //}
                    int numberOfBytesInBuffer = sp.BytesToRead;
                    sp.Read(readBuffer, actualPositionToRead, ((numberOfBytesInBuffer + actualPositionToRead) > readBuffer.Length) ? 0 : numberOfBytesInBuffer);
                    actualPositionToRead = actualPositionToRead + numberOfBytesInBuffer;
                    //sp.DiscardInBuffer();
                    //if (DetectValidModbusFrame(readBuffer, (actualPositionToRead < readBuffer.Length) ? actualPositionToRead : readBuffer.Length) | bytesToRead <= actualPositionToRead)
                    if (actualPositionToRead >= bytesToRead)
                    {

                            dataReceived = true;
                            bytesToRead = 0;
                            actualPositionToRead = 0;
                            if (debug) StoreLogData.Instance.Store("Received Serial-Data: " + BitConverter.ToString(readBuffer), System.DateTime.Now);

                    }


                    //dateTimeLastRead = DateTime.Now;
                }
         */


        private void DataReceivedHandler(object sender,
                        SerialDataReceivedEventArgs e)
        {
            serialport.DataReceived -= DataReceivedHandler;

            //while (receiveActive | dataReceived)
            //	System.Threading.Thread.Sleep(10);
            receiveActive = true;

            const long ticksWait = TimeSpan.TicksPerMillisecond * 2000;//((40*10000000) / this.baudRate);


            SerialPort sp = (SerialPort)sender;
            if (bytesToRead == 0)
            {
                sp.DiscardInBuffer();
                receiveActive = false;
                serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                return;
            }
            readBuffer = new byte[256];
            int numbytes = 0;
            int actualPositionToRead = 0;
            DateTime dateTimeLastRead = DateTime.Now;
            do
            {
                try
                {
                    dateTimeLastRead = DateTime.Now;
                    while ((sp.BytesToRead) == 0)
                    {
                        System.Threading.Thread.Sleep(10);
                        if ((DateTime.Now.Ticks - dateTimeLastRead.Ticks) > ticksWait)
                            break;
                    }
                    numbytes = sp.BytesToRead;


                    byte[] rxbytearray = new byte[numbytes];
                    sp.Read(rxbytearray, 0, numbytes);
                    Array.Copy(rxbytearray, 0, readBuffer, actualPositionToRead, (actualPositionToRead + rxbytearray.Length) <= bytesToRead ? rxbytearray.Length : bytesToRead - actualPositionToRead);

                    actualPositionToRead = actualPositionToRead + rxbytearray.Length;

                }
                catch (Exception)
                {

                }

                if (bytesToRead <= actualPositionToRead)
                    break;

                if (DetectValidModbusFrame(readBuffer, (actualPositionToRead < readBuffer.Length) ? actualPositionToRead : readBuffer.Length) | bytesToRead <= actualPositionToRead)
                    break;
            }
            while ((DateTime.Now.Ticks - dateTimeLastRead.Ticks) < ticksWait);

            //10.000 Ticks in 1 ms

            receiveData = new byte[actualPositionToRead];
            Array.Copy(readBuffer, 0, receiveData, 0, (actualPositionToRead < readBuffer.Length) ? actualPositionToRead : readBuffer.Length);
            if (debug) StoreLogData.Instance.Store("Received Serial-Data: " + BitConverter.ToString(readBuffer), System.DateTime.Now);
            bytesToRead = 0;




            dataReceived = true;
            receiveActive = false;
            serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            if (ReceiveDataChanged != null)
            {

                ReceiveDataChanged(this);

            }

            //sp.DiscardInBuffer();
        }

        public static bool DetectValidModbusFrame(byte[] readBuffer, int length)
        {
            // minimum length 6 bytes
            if (length < 6)
                return false;
            //SlaveID correct
            if ((readBuffer[0] < 1) | (readBuffer[0] > 247))
                return false;
            //CRC correct?
            byte[] crc = new byte[2];
            crc = BitConverter.GetBytes(calculateCRC(readBuffer, (ushort)(length - 2), 0));
            if (crc[0] != readBuffer[length - 2] | crc[1] != readBuffer[length - 1])
                return false;
            return true;
        }



        /// <summary>
        /// Read Discrete Inputs from Server device (FC2).
        /// </summary>
        /// <param name="startingAddress">First discrete input to read</param>
        /// <param name="quantity">Number of discrete Inputs to read</param>
        /// <returns>Boolean Array which contains the discrete Inputs</returns>
        public bool[] ReadDiscreteInputs(int startingAddress, int quantity)
        {
            if (debug) StoreLogData.Instance.Store("FC2 (Read Discrete Inputs from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity, System.DateTime.Now);
            transactionIdentifierInternal++;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            if (startingAddress > 65535 | quantity > 2000)
            {
                if (debug) StoreLogData.Instance.Store("ArgumentException Throwed", System.DateTime.Now);
                throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
            }
            bool[] response;
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)0x0006);
            this.functionCode = 0x02;
            this.startingAddress = BitConverter.GetBytes(startingAddress);
            this.quantity = BitConverter.GetBytes(quantity);
            Byte[] data = new byte[]
                            {
                            this.transactionIdentifier[1],
                            this.transactionIdentifier[0],
                            this.protocolIdentifier[1],
                            this.protocolIdentifier[0],
                            this.length[1],
                            this.length[0],
                            this.unitIdentifier,
                            this.functionCode,
                            this.startingAddress[1],
                            this.startingAddress[0],
                            this.quantity[1],
                            this.quantity[0],
                            this.crc[0],
                            this.crc[1]
                            };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];

            if (serialport != null)
            {
                dataReceived = false;
                if (quantity % 8 == 0)
                    bytesToRead = 5 + quantity / 8;
                else
                    bytesToRead = 6 + quantity / 8;
                //               serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, 8);
                if (debug)
                {
                    byte[] debugData = new byte[8];
                    Array.Copy(data, 6, debugData, 0, 8);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[8];
                    Array.Copy(data, 6, sendData, 0, 8);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;


                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);
                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x82 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x82 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x82 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x82 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
                if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        return ReadDiscreteInputs(startingAddress, quantity);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");
                    }
                    else
                    {
                        countRetries++;
                        return ReadDiscreteInputs(startingAddress, quantity);
                    }
                }
            }
            response = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                int intData = data[9 + i / 8];
                int mask = Convert.ToInt32(Math.Pow(2, (i % 8)));
                response[i] = Convert.ToBoolean((intData & mask) / mask);
            }
            return (response);
        }


        /// <summary>
        /// Read Coils from Server device (FC1).
        /// </summary>
        /// <param name="startingAddress">First coil to read</param>
        /// <param name="quantity">Numer of coils to read</param>
        /// <returns>Boolean Array which contains the coils</returns>
        public bool[] ReadCoils(int startingAddress, int quantity)
        {
            if (debug) StoreLogData.Instance.Store("FC1 (Read Coils from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity, System.DateTime.Now);
            transactionIdentifierInternal++;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            if (startingAddress > 65535 | quantity > 2000)
            {
                if (debug) StoreLogData.Instance.Store("ArgumentException Throwed", System.DateTime.Now);
                throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
            }
            bool[] response;
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)0x0006);
            this.functionCode = 0x01;
            this.startingAddress = BitConverter.GetBytes(startingAddress);
            this.quantity = BitConverter.GetBytes(quantity);
            Byte[] data = new byte[]{
                            this.transactionIdentifier[1],
                            this.transactionIdentifier[0],
                            this.protocolIdentifier[1],
                            this.protocolIdentifier[0],
                            this.length[1],
                            this.length[0],
                            this.unitIdentifier,
                            this.functionCode,
                            this.startingAddress[1],
                            this.startingAddress[0],
                            this.quantity[1],
                            this.quantity[0],
                            this.crc[0],
                            this.crc[1]
            };

            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                if (quantity % 8 == 0)
                    bytesToRead = 5 + quantity / 8;
                else
                    bytesToRead = 6 + quantity / 8;
                //               serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, 8);
                if (debug)
                {
                    byte[] debugData = new byte[8];
                    Array.Copy(data, 6, debugData, 0, 8);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[8];
                    Array.Copy(data, 6, sendData, 0, 8);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];

                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send MocbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);

                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x81 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x81 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x81 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x81 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
                if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        return ReadCoils(startingAddress, quantity);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");
                    }
                    else
                    {
                        countRetries++;
                        return ReadCoils(startingAddress, quantity);
                    }
                }
            }
            response = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                int intData = data[9 + i / 8];
                int mask = Convert.ToInt32(Math.Pow(2, (i % 8)));
                response[i] = Convert.ToBoolean((intData & mask) / mask);
            }
            return (response);
        }


        /// <summary>
        /// Read Holding Registers from Master device (FC3).
        /// </summary>
        /// <param name="startingAddress">First holding register to be read</param>
        /// <param name="quantity">Number of holding registers to be read</param>
        /// <returns>Int Array which contains the holding registers</returns>
        public int[] ReadHoldingRegisters(int startingAddress, int quantity)
        {
            if (debug) StoreLogData.Instance.Store("FC3 (Read Holding Registers from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity, System.DateTime.Now);
            transactionIdentifierInternal++;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            if (startingAddress > 65535 | quantity > 125)
            {
                if (debug) StoreLogData.Instance.Store("ArgumentException Throwed", System.DateTime.Now);
                throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 125");
            }
            int[] response;
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)0x0006);
            this.functionCode = 0x03;
            this.startingAddress = BitConverter.GetBytes(startingAddress);
            this.quantity = BitConverter.GetBytes(quantity);
            Byte[] data = new byte[]{   this.transactionIdentifier[1],
                            this.transactionIdentifier[0],
                            this.protocolIdentifier[1],
                            this.protocolIdentifier[0],
                            this.length[1],
                            this.length[0],
                            this.unitIdentifier,
                            this.functionCode,
                            this.startingAddress[1],
                            this.startingAddress[0],
                            this.quantity[1],
                            this.quantity[0],
                            this.crc[0],
                            this.crc[1]
            };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 5 + 2 * quantity;
                //                serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, 8);
                if (debug)
                {
                    byte[] debugData = new byte[8];
                    Array.Copy(data, 6, debugData, 0, 8);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[8];
                    Array.Copy(data, 6, sendData, 0, 8);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];

                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);

                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);

                    }
                    data = new Byte[256];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x83 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x83 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x83 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x83 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
                if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        return ReadHoldingRegisters(startingAddress, quantity);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");
                    }
                    else
                    {
                        countRetries++;
                        return ReadHoldingRegisters(startingAddress, quantity);
                    }


                }
            }
            response = new int[quantity];
            for (int i = 0; i < quantity; i++)
            {
                byte lowByte;
                byte highByte;
                highByte = data[9 + i * 2];
                lowByte = data[9 + i * 2 + 1];

                data[9 + i * 2] = lowByte;
                data[9 + i * 2 + 1] = highByte;

                response[i] = BitConverter.ToInt16(data, (9 + i * 2));
            }
            return (response);
        }



        /// <summary>
        /// Read Input Registers from Master device (FC4).
        /// </summary>
        /// <param name="startingAddress">First input register to be read</param>
        /// <param name="quantity">Number of input registers to be read</param>
        /// <returns>Int Array which contains the input registers</returns>
        public int[] ReadInputRegisters(int startingAddress, int quantity)
        {

            if (debug) StoreLogData.Instance.Store("FC4 (Read Input Registers from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity, System.DateTime.Now);
            transactionIdentifierInternal++;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            if (startingAddress > 65535 | quantity > 125)
            {
                if (debug) StoreLogData.Instance.Store("ArgumentException Throwed", System.DateTime.Now);
                throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 125");
            }
            int[] response;
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)0x0006);
            this.functionCode = 0x04;
            this.startingAddress = BitConverter.GetBytes(startingAddress);
            this.quantity = BitConverter.GetBytes(quantity);
            Byte[] data = new byte[]{   this.transactionIdentifier[1],
                            this.transactionIdentifier[0],
                            this.protocolIdentifier[1],
                            this.protocolIdentifier[0],
                            this.length[1],
                            this.length[0],
                            this.unitIdentifier,
                            this.functionCode,
                            this.startingAddress[1],
                            this.startingAddress[0],
                            this.quantity[1],
                            this.quantity[0],
                            this.crc[0],
                            this.crc[1]
            };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 5 + 2 * quantity;


                //               serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, 8);
                if (debug)
                {
                    byte[] debugData = new byte[8];
                    Array.Copy(data, 6, debugData, 0, 8);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[8];
                    Array.Copy(data, 6, sendData, 0, 8);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;

                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }

                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);
                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }

                }
            }
            if (data[7] == 0x84 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x84 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x84 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x84 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
                if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        return ReadInputRegisters(startingAddress, quantity);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");

                    }
                    else
                    {
                        countRetries++;
                        return ReadInputRegisters(startingAddress, quantity);
                    }

                }
            }
            response = new int[quantity];
            for (int i = 0; i < quantity; i++)
            {
                byte lowByte;
                byte highByte;
                highByte = data[9 + i * 2];
                lowByte = data[9 + i * 2 + 1];

                data[9 + i * 2] = lowByte;
                data[9 + i * 2 + 1] = highByte;

                response[i] = BitConverter.ToInt16(data, (9 + i * 2));
            }
            return (response);
        }


        /// <summary>
        /// Write single Coil to Master device (FC5).
        /// </summary>
        /// <param name="startingAddress">Coil to be written</param>
        /// <param name="value">Coil Value to be written</param>
        public void WriteSingleCoil(int startingAddress, bool value)
        {

            if (debug) StoreLogData.Instance.Store("FC5 (Write single coil to Master device), StartingAddress: " + startingAddress + ", Value: " + value, System.DateTime.Now);
            transactionIdentifierInternal++;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            byte[] coilValue = new byte[2];
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)0x0006);
            this.functionCode = 0x05;
            this.startingAddress = BitConverter.GetBytes(startingAddress);
            if (value == true)
            {
                coilValue = BitConverter.GetBytes((int)0xFF00);
            }
            else
            {
                coilValue = BitConverter.GetBytes((int)0x0000);
            }
            Byte[] data = new byte[]{   this.transactionIdentifier[1],
                            this.transactionIdentifier[0],
                            this.protocolIdentifier[1],
                            this.protocolIdentifier[0],
                            this.length[1],
                            this.length[0],
                            this.unitIdentifier,
                            this.functionCode,
                            this.startingAddress[1],
                            this.startingAddress[0],
                            coilValue[1],
                            coilValue[0],
                            this.crc[0],
                            this.crc[1]
                            };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 8;
                //               serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, 8);
                if (debug)
                {
                    byte[] debugData = new byte[8];
                    Array.Copy(data, 6, debugData, 0, 8);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[8];
                    Array.Copy(data, 6, sendData, 0, 8);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }

                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;

            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);

                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x85 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x85 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x85 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x85 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
                if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        WriteSingleCoil(startingAddress, value);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");

                    }
                    else
                    {
                        countRetries++;
                        WriteSingleCoil(startingAddress, value);
                    }
                }
            }
        }


        /// <summary>
        /// Write single Register to Master device (FC6).
        /// </summary>
        /// <param name="startingAddress">Register to be written</param>
        /// <param name="value">Register Value to be written</param>
        public void WriteSingleRegister(int startingAddress, int value)
        {
            if (debug) StoreLogData.Instance.Store("FC6 (Write single register to Master device), StartingAddress: " + startingAddress + ", Value: " + value, System.DateTime.Now);
            transactionIdentifierInternal++;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            byte[] registerValue = new byte[2];
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)0x0006);
            this.functionCode = 0x06;
            this.startingAddress = BitConverter.GetBytes(startingAddress);
            registerValue = BitConverter.GetBytes((int)value);

            Byte[] data = new byte[]{   this.transactionIdentifier[1],
                            this.transactionIdentifier[0],
                            this.protocolIdentifier[1],
                            this.protocolIdentifier[0],
                            this.length[1],
                            this.length[0],
                            this.unitIdentifier,
                            this.functionCode,
                            this.startingAddress[1],
                            this.startingAddress[0],
                            registerValue[1],
                            registerValue[0],
                            this.crc[0],
                            this.crc[1]
                            };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 8;
                //                serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, 8);
                if (debug)
                {
                    byte[] debugData = new byte[8];
                    Array.Copy(data, 6, debugData, 0, 8);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[8];
                    Array.Copy(data, 6, sendData, 0, 8);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);

                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x86 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x86 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x86 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x86 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
                if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        WriteSingleRegister(startingAddress, value);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");

                    }
                    else
                    {
                        countRetries++;
                        WriteSingleRegister(startingAddress, value);
                    }
                }
            }
        }

        /// <summary>
        /// Write multiple coils to Master device (FC15).
        /// </summary>
        /// <param name="startingAddress">First coil to be written</param>
        /// <param name="values">Coil Values to be written</param>
        public void WriteMultipleCoils(int startingAddress, bool[] values)
        {
            string debugString = "";
            for (int i = 0; i < values.Length; i++)
                debugString = debugString + values[i] + " ";
            if (debug) StoreLogData.Instance.Store("FC15 (Write multiple coils to Master device), StartingAddress: " + startingAddress + ", Values: " + debugString, System.DateTime.Now);
            transactionIdentifierInternal++;
            byte byteCount = (byte)((values.Length % 8 != 0 ? values.Length / 8 + 1 : (values.Length / 8)));
            byte[] quantityOfOutputs = BitConverter.GetBytes((int)values.Length);
            byte singleCoilValue = 0;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)(7 + (byteCount)));
            this.functionCode = 0x0F;
            this.startingAddress = BitConverter.GetBytes(startingAddress);



            Byte[] data = new byte[14 + 2 + (values.Length % 8 != 0 ? values.Length / 8 : (values.Length / 8) - 1)];
            data[0] = this.transactionIdentifier[1];
            data[1] = this.transactionIdentifier[0];
            data[2] = this.protocolIdentifier[1];
            data[3] = this.protocolIdentifier[0];
            data[4] = this.length[1];
            data[5] = this.length[0];
            data[6] = this.unitIdentifier;
            data[7] = this.functionCode;
            data[8] = this.startingAddress[1];
            data[9] = this.startingAddress[0];
            data[10] = quantityOfOutputs[1];
            data[11] = quantityOfOutputs[0];
            data[12] = byteCount;
            for (int i = 0; i < values.Length; i++)
            {
                if ((i % 8) == 0)
                    singleCoilValue = 0;
                byte CoilValue;
                if (values[i] == true)
                    CoilValue = 1;
                else
                    CoilValue = 0;


                singleCoilValue = (byte)((int)CoilValue << (i % 8) | (int)singleCoilValue);

                data[13 + (i / 8)] = singleCoilValue;
            }
            crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data.Length - 8), 6));
            data[data.Length - 2] = crc[0];
            data[data.Length - 1] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 8;
                //               serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, data.Length - 6);
                if (debug)
                {
                    byte[] debugData = new byte[data.Length - 6];
                    Array.Copy(data, 6, debugData, 0, data.Length - 6);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[data.Length - 6];
                    Array.Copy(data, 6, sendData, 0, data.Length - 6);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);

                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x8F & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x8F & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x8F & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x8F & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
                if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        WriteMultipleCoils(startingAddress, values);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");

                    }
                    else
                    {
                        countRetries++;
                        WriteMultipleCoils(startingAddress, values);
                    }
                }
            }
        }

        /// <summary>
        /// Write multiple registers to Master device (FC16).
        /// </summary>
        /// <param name="startingAddress">First register to be written</param>
        /// <param name="values">register Values to be written</param>
        public void WriteMultipleRegisters(int startingAddress, int[] values)
        {
            string debugString = "";
            for (int i = 0; i < values.Length; i++)
                debugString = debugString + values[i] + " ";
            if (debug) StoreLogData.Instance.Store("FC16 (Write multiple Registers to Server device), StartingAddress: " + startingAddress + ", Values: " + debugString, System.DateTime.Now);
            transactionIdentifierInternal++;
            byte byteCount = (byte)(values.Length * 2);
            byte[] quantityOfOutputs = BitConverter.GetBytes((int)values.Length);
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)(7 + values.Length * 2));
            this.functionCode = 0x10;
            this.startingAddress = BitConverter.GetBytes(startingAddress);

            Byte[] data = new byte[13 + 2 + values.Length * 2];
            data[0] = this.transactionIdentifier[1];
            data[1] = this.transactionIdentifier[0];
            data[2] = this.protocolIdentifier[1];
            data[3] = this.protocolIdentifier[0];
            data[4] = this.length[1];
            data[5] = this.length[0];
            data[6] = this.unitIdentifier;
            data[7] = this.functionCode;
            data[8] = this.startingAddress[1];
            data[9] = this.startingAddress[0];
            data[10] = quantityOfOutputs[1];
            data[11] = quantityOfOutputs[0];
            data[12] = byteCount;
            for (int i = 0; i < values.Length; i++)
            {
                byte[] singleRegisterValue = BitConverter.GetBytes((int)values[i]);
                data[13 + i * 2] = singleRegisterValue[1];
                data[14 + i * 2] = singleRegisterValue[0];
            }
            crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data.Length - 8), 6));
            data[data.Length - 2] = crc[0];
            data[data.Length - 1] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 8;
                //                serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, data.Length - 6);

                if (debug)
                {
                    byte[] debugData = new byte[data.Length - 6];
                    Array.Copy(data, 6, debugData, 0, data.Length - 6);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[data.Length - 6];
                    Array.Copy(data, 6, sendData, 0, data.Length - 6);
                    SendDataChanged(this);

                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);
                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x90 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x90 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x90 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x90 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            if (serialport != null)
            {
                crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
                if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("CRCCheckFailedException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
                    }
                    else
                    {
                        countRetries++;
                        WriteMultipleRegisters(startingAddress, values);
                    }
                }
                else if (!dataReceived)
                {
                    if (debug) StoreLogData.Instance.Store("TimeoutException Throwed", System.DateTime.Now);
                    if (NumberOfRetries <= countRetries)
                    {
                        countRetries = 0;
                        throw new TimeoutException("No Response from Modbus Slave");

                    }
                    else
                    {
                        countRetries++;
                        WriteMultipleRegisters(startingAddress, values);
                    }
                }
            }
        }

        /// <summary>
        /// Read/Write Multiple Registers (FC23).
        /// </summary>
        /// <param name="startingAddressRead">First input register to read</param>
        /// <param name="quantityRead">Number of input registers to read</param>
        /// <param name="startingAddressWrite">First input register to write</param>
        /// <param name="values">Values to write</param>
        /// <returns>Int Array which contains the Holding registers</returns>
        public int[] ReadWriteMultipleRegisters(int startingAddressRead, int quantityRead, int startingAddressWrite, int[] values)
        {

            string debugString = "";
            for (int i = 0; i < values.Length; i++)
                debugString = debugString + values[i] + " ";
            if (debug) StoreLogData.Instance.Store("FC23 (Read and Write multiple Registers to Server device), StartingAddress Read: " + startingAddressRead + ", Quantity Read: " + quantityRead + ", startingAddressWrite: " + startingAddressWrite + ", Values: " + debugString, System.DateTime.Now);
            transactionIdentifierInternal++;
            byte[] startingAddressReadLocal = new byte[2];
            byte[] quantityReadLocal = new byte[2];
            byte[] startingAddressWriteLocal = new byte[2];
            byte[] quantityWriteLocal = new byte[2];
            byte writeByteCountLocal = 0;
            if (serialport != null)
                if (!serialport.IsOpen)
                {
                    if (debug) StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", System.DateTime.Now);
                    throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                }
            if (tcpClient == null & !udpFlag & serialport == null)
            {
                if (debug) StoreLogData.Instance.Store("ConnectionException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ConnectionException("connection error");
            }
            if (startingAddressRead > 65535 | quantityRead > 125 | startingAddressWrite > 65535 | values.Length > 121)
            {
                if (debug) StoreLogData.Instance.Store("ArgumentException Throwed", System.DateTime.Now);
                throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
            }
            int[] response;
            this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            this.length = BitConverter.GetBytes((int)11 + values.Length * 2);
            this.functionCode = 0x17;
            startingAddressReadLocal = BitConverter.GetBytes(startingAddressRead);
            quantityReadLocal = BitConverter.GetBytes(quantityRead);
            startingAddressWriteLocal = BitConverter.GetBytes(startingAddressWrite);
            quantityWriteLocal = BitConverter.GetBytes(values.Length);
            writeByteCountLocal = Convert.ToByte(values.Length * 2);
            Byte[] data = new byte[17 + 2 + values.Length * 2];
            data[0] = this.transactionIdentifier[1];
            data[1] = this.transactionIdentifier[0];
            data[2] = this.protocolIdentifier[1];
            data[3] = this.protocolIdentifier[0];
            data[4] = this.length[1];
            data[5] = this.length[0];
            data[6] = this.unitIdentifier;
            data[7] = this.functionCode;
            data[8] = startingAddressReadLocal[1];
            data[9] = startingAddressReadLocal[0];
            data[10] = quantityReadLocal[1];
            data[11] = quantityReadLocal[0];
            data[12] = startingAddressWriteLocal[1];
            data[13] = startingAddressWriteLocal[0];
            data[14] = quantityWriteLocal[1];
            data[15] = quantityWriteLocal[0];
            data[16] = writeByteCountLocal;

            for (int i = 0; i < values.Length; i++)
            {
                byte[] singleRegisterValue = BitConverter.GetBytes((int)values[i]);
                data[17 + i * 2] = singleRegisterValue[1];
                data[18 + i * 2] = singleRegisterValue[0];
            }
            crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data.Length - 8), 6));
            data[data.Length - 2] = crc[0];
            data[data.Length - 1] = crc[1];
            if (serialport != null)
            {
                dataReceived = false;
                bytesToRead = 5 + 2 * quantityRead;
                //               serialport.ReceivedBytesThreshold = bytesToRead;
                serialport.Write(data, 6, data.Length - 6);
                if (debug)
                {
                    byte[] debugData = new byte[data.Length - 6];
                    Array.Copy(data, 6, debugData, 0, data.Length - 6);
                    if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                }
                if (SendDataChanged != null)
                {
                    sendData = new byte[data.Length - 6];
                    Array.Copy(data, 6, sendData, 0, data.Length - 6);
                    SendDataChanged(this);
                }
                data = new byte[2100];
                readBuffer = new byte[256];
                DateTime dateTimeSend = DateTime.Now;
                byte receivedUnitIdentifier = 0xFF;
                while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                {
                    while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
                        System.Threading.Thread.Sleep(1);
                    data = new byte[2100];
                    Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
                    receivedUnitIdentifier = data[6];
                }
                if (receivedUnitIdentifier != this.unitIdentifier)
                    data = new byte[2100];
                else
                    countRetries = 0;
            }
            else if (tcpClient.Client.Connected | udpFlag)
            {
                if (udpFlag)
                {
                    UdpClient udpClient = new UdpClient();
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
                    udpClient.Send(data, data.Length - 2, endPoint);
                    portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = 5000;
                    endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
                    data = udpClient.Receive(ref endPoint);
                }
                else
                {
                    stream.Write(data, 0, data.Length - 2);
                    if (debug)
                    {
                        byte[] debugData = new byte[data.Length - 2];
                        Array.Copy(data, 0, debugData, 0, data.Length - 2);
                        if (debug) StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                    }
                    if (SendDataChanged != null)
                    {
                        sendData = new byte[data.Length - 2];
                        Array.Copy(data, 0, sendData, 0, data.Length - 2);
                        SendDataChanged(this);

                    }
                    data = new Byte[2100];
                    int NumberOfBytes = stream.Read(data, 0, data.Length);
                    if (ReceiveDataChanged != null)
                    {
                        receiveData = new byte[NumberOfBytes];
                        Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                        if (debug) StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData), System.DateTime.Now);
                        ReceiveDataChanged(this);
                    }
                }
            }
            if (data[7] == 0x97 & data[8] == 0x01)
            {
                if (debug) StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
            }
            if (data[7] == 0x97 & data[8] == 0x02)
            {
                if (debug) StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
            }
            if (data[7] == 0x97 & data[8] == 0x03)
            {
                if (debug) StoreLogData.Instance.Store("QuantityInvalidException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
            }
            if (data[7] == 0x97 & data[8] == 0x04)
            {
                if (debug) StoreLogData.Instance.Store("ModbusException Throwed", System.DateTime.Now);
                throw new EasyModbus.Exceptions.ModbusException("error reading");
            }
            response = new int[quantityRead];
            for (int i = 0; i < quantityRead; i++)
            {
                byte lowByte;
                byte highByte;
                highByte = data[9 + i * 2];
                lowByte = data[9 + i * 2 + 1];

                data[9 + i * 2] = lowByte;
                data[9 + i * 2 + 1] = highByte;

                response[i] = BitConverter.ToInt16(data, (9 + i * 2));
            }
            return (response);
        }

        /// <summary>
        /// Close connection to Master Device.
        /// </summary>
        public void Disconnect()
        {
            if (debug) StoreLogData.Instance.Store("Disconnect", System.DateTime.Now);
            if (serialport != null)
            {
                if (serialport.IsOpen & !this.receiveActive)
                    serialport.Close();
                if (ConnectedChanged != null)
                    ConnectedChanged(this);
                return;
            }
            if (stream != null)
                stream.Close();
            if (tcpClient != null)
                tcpClient.Close();
            connected = false;
            if (ConnectedChanged != null)
                ConnectedChanged(this);

        }

        /// <summary>
        /// Destructor - Close connection to Master Device.
        /// </summary>
		~ModbusClient()
        {
            if (debug) StoreLogData.Instance.Store("Destructor called - automatically disconnect", System.DateTime.Now);
            if (serialport != null)
            {
                if (serialport.IsOpen)
                    serialport.Close();
                return;
            }
            if (tcpClient != null & !udpFlag)
            {
                if (stream != null)
                    stream.Close();
                tcpClient.Close();
            }
        }

        /// <summary>
        /// Returns "TRUE" if Client is connected to Server and "FALSE" if not. In case of Modbus RTU returns if COM-Port is opened
        /// </summary>
		public bool Connected
        {
            get
            {
                if (serialport != null)
                {
                    return (serialport.IsOpen);
                }

                if (udpFlag & tcpClient != null)
                    return true;
                if (tcpClient == null)
                    return false;
                else
                {
                    return connected;

                }

            }
        }

        public bool Available(int timeout)
        {
            // Ping's the local machine.
            System.Net.NetworkInformation.Ping pingSender = new System.Net.NetworkInformation.Ping();
            IPAddress address = System.Net.IPAddress.Parse(ipAddress);

            // Create a buffer of 32 bytes of data to be transmitted.
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(data);

            // Wait 10 seconds for a reply.
            System.Net.NetworkInformation.PingReply reply = pingSender.Send(address, timeout, buffer);

            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Gets or Sets the IP-Address of the Server.
        /// </summary>
		public string IPAddress
        {
            get
            {
                return ipAddress;
            }
            set
            {
                ipAddress = value;
            }
        }

        /// <summary>
        /// Gets or Sets the Port were the Modbus-TCP Server is reachable (Standard is 502).
        /// </summary>
		public int Port
        {
            get
            {
                return port;
            }
            set
            {
                port = value;
            }
        }

        /// <summary>
        /// Gets or Sets the UDP-Flag to activate Modbus UDP.
        /// </summary>
        public bool UDPFlag
        {
            get
            {
                return udpFlag;
            }
            set
            {
                udpFlag = value;
            }
        }

        /// <summary>
        /// Gets or Sets the Unit identifier in case of serial connection (Default = 0)
        /// </summary>
        public byte UnitIdentifier
        {
            get
            {
                return unitIdentifier;
            }
            set
            {
                unitIdentifier = value;
            }
        }


        /// <summary>
        /// Gets or Sets the Baudrate for serial connection (Default = 9600)
        /// </summary>
        public int Baudrate
        {
            get
            {
                return baudRate;
            }
            set
            {
                baudRate = value;
            }
        }

        /// <summary>
        /// Gets or Sets the of Parity in case of serial connection
        /// </summary>
        public Parity Parity
        {
            get
            {
                if (serialport != null)
                    return parity;
                else
                    return Parity.Even;
            }
            set
            {
                if (serialport != null)
                    parity = value;
            }
        }


        /// <summary>
        /// Gets or Sets the number of stopbits in case of serial connection
        /// </summary>
        public StopBits StopBits
        {
            get
            {
                if (serialport != null)
                    return stopBits;
                else
                    return StopBits.One;
            }
            set
            {
                if (serialport != null)
                    stopBits = value;
            }
        }

        /// <summary>
        /// Gets or Sets the connection Timeout in case of ModbusTCP connection
        /// </summary>
        public int ConnectionTimeout
        {
            get
            {
                return connectTimeout;
            }
            set
            {
                connectTimeout = value;
            }
        }

        /// <summary>
        /// Gets or Sets the serial Port
        /// </summary>
        public string SerialPort
        {
            get
            {

                return serialport.PortName;
            }
            set
            {
                if (value == null)
                {
                    serialport = null;
                    return;
                }
                if (serialport != null)
                    serialport.Close();
                this.serialport = new SerialPort();
                this.serialport.PortName = value;
                serialport.BaudRate = baudRate;
                serialport.Parity = parity;
                serialport.StopBits = stopBits;
                serialport.WriteTimeout = 10000;
                serialport.ReadTimeout = connectTimeout;
                serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            }
        }

        /// <summary>
        /// Gets or Sets the Filename for the LogFile
        /// </summary>
        public string LogFileFilename
        {
            get
            {
                return StoreLogData.Instance.Filename;
            }
            set
            {
                StoreLogData.Instance.Filename = value;
                if (StoreLogData.Instance.Filename != null)
                    debug = true;
                else
                    debug = false;
            }
        }

    }
    public sealed class StoreLogData
    {
        private String filename = null;
        private static volatile StoreLogData instance;
        private static object syncObject = new Object();

        /// <summary>
        /// Private constructor; Ensures the access of the class only via "instance"
        /// </summary>
        private StoreLogData()
        {
        }

        /// <summary>
        /// Returns the instance of the class (singleton)
        /// </summary>
        /// <returns>instance (Singleton)</returns>
        public static StoreLogData Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncObject)
                    {
                        if (instance == null)
                            instance = new StoreLogData();
                    }
                }

                return instance;
            }
        }

        /// <summary>
        /// Store message in Log-File
        /// </summary>
        /// <param name="message">Message to append to the Log-File</param>
        public void Store(String message)
        {
            if (this.filename == null)
                return;

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(Filename, true))
            {
                file.WriteLine(message);
            }
        }

        /// <summary>
        /// Store message in Log-File including Timestamp
        /// </summary>
        /// <param name="message">Message to append to the Log-File</param>
        /// <param name="timestamp">Timestamp to add to the same Row</param>
        public void Store(String message, DateTime timestamp)
        {
            try
            {
                using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(Filename, true))
                {
                    file.WriteLine(timestamp.ToString("dd.MM.yyyy H:mm:ss.ff ") + message);
                }
            }
            catch (Exception e)
            {

            }
        }

        /// <summary>
        /// Gets or Sets the Filename to Store Strings in a File
        /// </summary>
        public string Filename
        {
            get
            {
                return filename;
            }
            set
            {
                filename = value;
            }
        }
    }
    public class ModbusProtocol
    {
        public enum ProtocolType { ModbusTCP = 0, ModbusUDP = 1, ModbusRTU = 2 };
        public DateTime timeStamp;
        public bool request;
        public bool response;
        public UInt16 transactionIdentifier;
        public UInt16 protocolIdentifier;
        public UInt16 length;
        public byte unitIdentifier;
        public byte functionCode;
        public UInt16 startingAdress;
        public UInt16 startingAddressRead;
        public UInt16 startingAddressWrite;
        public UInt16 quantity;
        public UInt16 quantityRead;
        public UInt16 quantityWrite;
        public byte byteCount;
        public byte exceptionCode;
        public byte errorCode;
        public UInt16[] receiveCoilValues;
        public UInt16[] receiveRegisterValues;
        public Int16[] sendRegisterValues;
        public bool[] sendCoilValues;
        public UInt16 crc;
    }


    #region structs
    struct NetworkConnectionParameter
    {
        public NetworkStream stream;        //For TCP-Connection only
        public Byte[] bytes;
        public int portIn;                  //For UDP-Connection only
        public IPAddress ipAddressIn;       //For UDP-Connection only
    }
    #endregion

    #region TCPHandler class
    internal class TCPHandler
    {
        public delegate void DataChanged(object networkConnectionParameter);
        public event DataChanged dataChanged;

        public delegate void NumberOfClientsChanged();
        public event NumberOfClientsChanged numberOfClientsChanged;

        TcpListener server = null;


        private List<Client> tcpClientLastRequestList = new List<Client>();

        public int NumberOfConnectedClients { get; set; }

        public string ipAddress = null;

        /// When making a server TCP listen socket, will listen to this IP address.
        public IPAddress LocalIPAddress
        {
            get { return localIPAddress; }
        }
        private IPAddress localIPAddress = IPAddress.Any;

        /// <summary>
        /// Listen to all network interfaces.
        /// </summary>
        /// <param name="port">TCP port to listen</param>
        public TCPHandler(int port)
        {
            server = new TcpListener(LocalIPAddress, port);
            server.Start();
            server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
        }

        /// <summary>
        /// Listen to a specific network interface.
        /// </summary>
        /// <param name="localIPAddress">IP address of network interface to listen</param>
        /// <param name="port">TCP port to listen</param>
        public TCPHandler(IPAddress localIPAddress, int port)
        {
            this.localIPAddress = localIPAddress;
            server = new TcpListener(LocalIPAddress, port);
            server.Start();
            server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
        }


        private void AcceptTcpClientCallback(IAsyncResult asyncResult)
        {
            TcpClient tcpClient = new TcpClient();
            try
            {
                tcpClient = server.EndAcceptTcpClient(asyncResult);
                tcpClient.ReceiveTimeout = 4000;
                if (ipAddress != null)
                {
                    string ipEndpoint = tcpClient.Client.RemoteEndPoint.ToString();
                    ipEndpoint = ipEndpoint.Split(':')[0];
                    if (ipEndpoint != ipAddress)
                    {
                        tcpClient.Client.Disconnect(false);
                        return;
                    }
                }
            }
            catch (Exception) { }
            try
            {
                server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
                Client client = new Client(tcpClient);
                NetworkStream networkStream = client.NetworkStream;
                networkStream.ReadTimeout = 4000;
                networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);
            }
            catch (Exception) { }
        }

        private int GetAndCleanNumberOfConnectedClients(Client client)
        {
            lock (this)
            {
                int i = 0;
                bool objetExists = false;
                foreach (Client clientLoop in tcpClientLastRequestList)
                {
                    if (client.Equals(clientLoop))
                        objetExists = true;
                }
                try
                {
                    tcpClientLastRequestList.RemoveAll(delegate (Client c)
                    {
                        return ((DateTime.Now.Ticks - c.Ticks) > 40000000);
                    }

                        );
                }
                catch (Exception) { }
                if (!objetExists)
                    tcpClientLastRequestList.Add(client);


                return tcpClientLastRequestList.Count;
            }
        }

        private void ReadCallback(IAsyncResult asyncResult)
        {
            NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
            Client client = asyncResult.AsyncState as Client;
            client.Ticks = DateTime.Now.Ticks;
            NumberOfConnectedClients = GetAndCleanNumberOfConnectedClients(client);
            if (numberOfClientsChanged != null)
                numberOfClientsChanged();
            if (client != null)
            {
                int read;
                NetworkStream networkStream = null;
                try
                {
                    networkStream = client.NetworkStream;

                    read = networkStream.EndRead(asyncResult);
                }
                catch (Exception ex)
                {
                    return;
                }


                if (read == 0)
                {
                    //OnClientDisconnected(client.TcpClient);
                    //connectedClients.Remove(client);
                    return;
                }
                byte[] data = new byte[read];
                Buffer.BlockCopy(client.Buffer, 0, data, 0, read);
                networkConnectionParameter.bytes = data;
                networkConnectionParameter.stream = networkStream;
                if (dataChanged != null)
                    dataChanged(networkConnectionParameter);
                try
                {
                    networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);
                }
                catch (Exception)
                {
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                foreach (Client clientLoop in tcpClientLastRequestList)
                {
                    clientLoop.NetworkStream.Close(00);
                }
            }
            catch (Exception) { }
            server.Stop();

        }


        internal class Client
        {
            private readonly TcpClient tcpClient;
            private readonly byte[] buffer;
            public long Ticks { get; set; }

            public Client(TcpClient tcpClient)
            {
                this.tcpClient = tcpClient;
                int bufferSize = tcpClient.ReceiveBufferSize;
                buffer = new byte[bufferSize];
            }

            public TcpClient TcpClient
            {
                get { return tcpClient; }
            }

            public byte[] Buffer
            {
                get { return buffer; }
            }

            public NetworkStream NetworkStream
            {
                get
                {

                    return tcpClient.GetStream();

                }
            }
        }
    }
    #endregion

    /// <summary>
    /// Modbus TCP Server.
    /// </summary>
    public class ModbusServer
    {
        private bool debug = false;
        Int32 port = 502;
        ModbusProtocol receiveData;
        ModbusProtocol sendData = new ModbusProtocol();
        Byte[] bytes = new Byte[2100];
        //public Int16[] _holdingRegisters = new Int16[65535];
        public HoldingRegisters holdingRegisters;
        public InputRegisters inputRegisters;
        public Coils coils;
        public DiscreteInputs discreteInputs;
        private int numberOfConnections = 0;
        private bool udpFlag;
        private bool serialFlag;
        private int baudrate = 9600;
        private System.IO.Ports.Parity parity = Parity.Even;
        private System.IO.Ports.StopBits stopBits = StopBits.One;
        private string serialPort = "COM1";
        private SerialPort serialport;
        private byte unitIdentifier = 1;
        private int portIn;
        private IPAddress ipAddressIn;
        private UdpClient udpClient;
        private IPEndPoint iPEndPoint;
        private TCPHandler tcpHandler;
        Thread listenerThread;
        Thread clientConnectionThread;
        private ModbusProtocol[] modbusLogData = new ModbusProtocol[100];
        public bool FunctionCode1Disabled { get; set; }
        public bool FunctionCode2Disabled { get; set; }
        public bool FunctionCode3Disabled { get; set; }
        public bool FunctionCode4Disabled { get; set; }
        public bool FunctionCode5Disabled { get; set; }
        public bool FunctionCode6Disabled { get; set; }
        public bool FunctionCode15Disabled { get; set; }
        public bool FunctionCode16Disabled { get; set; }
        public bool FunctionCode23Disabled { get; set; }
        public bool PortChanged { get; set; }
        object lockCoils = new object();
        object lockHoldingRegisters = new object();
        private volatile bool shouldStop;

        private IPAddress localIPAddress = IPAddress.Any;

        /// <summary>
        /// When creating a TCP or UDP socket, the local IP address to attach to.
        /// </summary>
        public IPAddress LocalIPAddress
        {
            get { return localIPAddress; }
            set { if (listenerThread == null) localIPAddress = value; }
        }

        public ModbusServer()
        {
            holdingRegisters = new HoldingRegisters(this);
            inputRegisters = new InputRegisters(this);
            coils = new Coils(this);
            discreteInputs = new DiscreteInputs(this);

        }

        #region events
        public delegate void CoilsChangedHandler(int coil, int numberOfCoils);
        public event CoilsChangedHandler CoilsChanged;

        public delegate void HoldingRegistersChangedHandler(int register, int numberOfRegisters);
        public event HoldingRegistersChangedHandler HoldingRegistersChanged;

        public delegate void NumberOfConnectedClientsChangedHandler();
        public event NumberOfConnectedClientsChangedHandler NumberOfConnectedClientsChanged;

        public delegate void LogDataChangedHandler();
        public event LogDataChangedHandler LogDataChanged;
        #endregion

        public void Listen()
        {

            listenerThread = new Thread(ListenerThread);
            listenerThread.Start();
        }

        public void StopListening()
        {
            if (SerialFlag & (serialport != null))
            {
                if (serialport.IsOpen)
                    serialport.Close();
                shouldStop = true;
            }
            try
            {
                tcpHandler.Disconnect();
                listenerThread.Abort();

            }
            catch (Exception) { }
            listenerThread.Join();
            try
            {

                clientConnectionThread.Abort();
            }
            catch (Exception) { }
        }

        private void ListenerThread()
        {
            if (!udpFlag & !serialFlag)
            {
                if (udpClient != null)
                {
                    try
                    {
                        udpClient.Close();
                    }
                    catch (Exception) { }
                }
                tcpHandler = new TCPHandler(LocalIPAddress, port);
                if (debug) StoreLogData.Instance.Store($"EasyModbus Server listing for incomming data at Port {port}, local IP {LocalIPAddress}", System.DateTime.Now);
                tcpHandler.dataChanged += new TCPHandler.DataChanged(ProcessReceivedData);
                tcpHandler.numberOfClientsChanged += new TCPHandler.NumberOfClientsChanged(numberOfClientsChanged);
            }
            else if (serialFlag)
            {
                if (serialport == null)
                {
                    if (debug) StoreLogData.Instance.Store("EasyModbus RTU-Server listing for incomming data at Serial Port " + serialPort, System.DateTime.Now);
                    serialport = new SerialPort();
                    serialport.PortName = serialPort;
                    serialport.BaudRate = this.baudrate;
                    serialport.Parity = this.parity;
                    serialport.StopBits = stopBits;
                    serialport.WriteTimeout = 10000;
                    serialport.ReadTimeout = 1000;
                    serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                    serialport.Open();
                }
            }
            else
                while (!shouldStop)
                {
                    if (udpFlag)
                    {
                        if (udpClient == null | PortChanged)
                        {
                            IPEndPoint localEndoint = new IPEndPoint(LocalIPAddress, port);
                            udpClient = new UdpClient(localEndoint);
                            if (debug) StoreLogData.Instance.Store($"EasyModbus Server listing for incomming data at Port {port}, local IP {LocalIPAddress}", System.DateTime.Now);
                            udpClient.Client.ReceiveTimeout = 1000;
                            iPEndPoint = new IPEndPoint(IPAddress.Any, port);
                            PortChanged = false;
                        }
                        if (tcpHandler != null)
                            tcpHandler.Disconnect();
                        try
                        {
                            bytes = udpClient.Receive(ref iPEndPoint);
                            portIn = iPEndPoint.Port;
                            NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
                            networkConnectionParameter.bytes = bytes;
                            ipAddressIn = iPEndPoint.Address;
                            networkConnectionParameter.portIn = portIn;
                            networkConnectionParameter.ipAddressIn = ipAddressIn;
                            ParameterizedThreadStart pts = new ParameterizedThreadStart(this.ProcessReceivedData);
                            Thread processDataThread = new Thread(pts);
                            processDataThread.Start(networkConnectionParameter);
                        }
                        catch (Exception)
                        {
                        }
                    }

                }
        }

        #region SerialHandler
        private bool dataReceived = false;
        private byte[] readBuffer = new byte[2094];
        private DateTime lastReceive;
        private int nextSign = 0;
        private void DataReceivedHandler(object sender,
                        SerialDataReceivedEventArgs e)
        {
            int silence = 4000 / baudrate;
            if ((DateTime.Now.Ticks - lastReceive.Ticks) > TimeSpan.TicksPerMillisecond * silence)
                nextSign = 0;


            SerialPort sp = (SerialPort)sender;

            int numbytes = sp.BytesToRead;
            byte[] rxbytearray = new byte[numbytes];

            sp.Read(rxbytearray, 0, numbytes);

            Array.Copy(rxbytearray, 0, readBuffer, nextSign, rxbytearray.Length);
            lastReceive = DateTime.Now;
            nextSign = numbytes + nextSign;
            if (ModbusClient.DetectValidModbusFrame(readBuffer, nextSign))
            {

                dataReceived = true;
                nextSign = 0;

                NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
                networkConnectionParameter.bytes = readBuffer;
                ParameterizedThreadStart pts = new ParameterizedThreadStart(this.ProcessReceivedData);
                Thread processDataThread = new Thread(pts);
                processDataThread.Start(networkConnectionParameter);
                dataReceived = false;

            }
            else
                dataReceived = false;
        }
        #endregion

        #region Method numberOfClientsChanged
        private void numberOfClientsChanged()
        {
            numberOfConnections = tcpHandler.NumberOfConnectedClients;
            if (NumberOfConnectedClientsChanged != null)
                NumberOfConnectedClientsChanged();
        }
        #endregion

        object lockProcessReceivedData = new object();
        #region Method ProcessReceivedData
        private void ProcessReceivedData(object networkConnectionParameter)
        {
            lock (lockProcessReceivedData)
            {
                Byte[] bytes = new byte[((NetworkConnectionParameter)networkConnectionParameter).bytes.Length];
                if (debug) StoreLogData.Instance.Store("Received Data: " + BitConverter.ToString(bytes), System.DateTime.Now);
                NetworkStream stream = ((NetworkConnectionParameter)networkConnectionParameter).stream;
                int portIn = ((NetworkConnectionParameter)networkConnectionParameter).portIn;
                IPAddress ipAddressIn = ((NetworkConnectionParameter)networkConnectionParameter).ipAddressIn;


                Array.Copy(((NetworkConnectionParameter)networkConnectionParameter).bytes, 0, bytes, 0, ((NetworkConnectionParameter)networkConnectionParameter).bytes.Length);

                ModbusProtocol receiveDataThread = new ModbusProtocol();
                ModbusProtocol sendDataThread = new ModbusProtocol();

                try
                {
                    UInt16[] wordData = new UInt16[1];
                    byte[] byteData = new byte[2];
                    receiveDataThread.timeStamp = DateTime.Now;
                    receiveDataThread.request = true;
                    if (!serialFlag)
                    {
                        //Lese Transaction identifier
                        byteData[1] = bytes[0];
                        byteData[0] = bytes[1];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.transactionIdentifier = wordData[0];

                        //Lese Protocol identifier
                        byteData[1] = bytes[2];
                        byteData[0] = bytes[3];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.protocolIdentifier = wordData[0];

                        //Lese length
                        byteData[1] = bytes[4];
                        byteData[0] = bytes[5];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.length = wordData[0];
                    }

                    //Lese unit identifier
                    receiveDataThread.unitIdentifier = bytes[6 - 6 * Convert.ToInt32(serialFlag)];
                    //Check UnitIdentifier
                    if ((receiveDataThread.unitIdentifier != this.unitIdentifier) & (receiveDataThread.unitIdentifier != 0))
                        return;

                    // Lese function code
                    receiveDataThread.functionCode = bytes[7 - 6 * Convert.ToInt32(serialFlag)];

                    // Lese starting address 
                    byteData[1] = bytes[8 - 6 * Convert.ToInt32(serialFlag)];
                    byteData[0] = bytes[9 - 6 * Convert.ToInt32(serialFlag)];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.startingAdress = wordData[0];

                    if (receiveDataThread.functionCode <= 4)
                    {
                        // Lese quantity
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantity = wordData[0];
                    }
                    if (receiveDataThread.functionCode == 5)
                    {
                        receiveDataThread.receiveCoilValues = new ushort[1];
                        // Lese Value
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveCoilValues, 0, 2);
                    }
                    if (receiveDataThread.functionCode == 6)
                    {
                        receiveDataThread.receiveRegisterValues = new ushort[1];
                        // Lese Value
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, 0, 2);
                    }
                    if (receiveDataThread.functionCode == 15)
                    {
                        // Lese quantity
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantity = wordData[0];

                        receiveDataThread.byteCount = bytes[12 - 6 * Convert.ToInt32(serialFlag)];

                        if ((receiveDataThread.byteCount % 2) != 0)
                            receiveDataThread.receiveCoilValues = new ushort[receiveDataThread.byteCount / 2 + 1];
                        else
                            receiveDataThread.receiveCoilValues = new ushort[receiveDataThread.byteCount / 2];
                        // Lese Value
                        Buffer.BlockCopy(bytes, 13 - 6 * Convert.ToInt32(serialFlag), receiveDataThread.receiveCoilValues, 0, receiveDataThread.byteCount);
                    }
                    if (receiveDataThread.functionCode == 16)
                    {
                        // Lese quantity
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantity = wordData[0];

                        receiveDataThread.byteCount = bytes[12 - 6 * Convert.ToInt32(serialFlag)];
                        receiveDataThread.receiveRegisterValues = new ushort[receiveDataThread.quantity];
                        for (int i = 0; i < receiveDataThread.quantity; i++)
                        {
                            // Lese Value
                            byteData[1] = bytes[13 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            byteData[0] = bytes[14 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, i * 2, 2);
                        }

                    }
                    if (receiveDataThread.functionCode == 23)
                    {
                        // Lese starting Address Read
                        byteData[1] = bytes[8 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[9 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.startingAddressRead = wordData[0];
                        // Lese quantity Read
                        byteData[1] = bytes[10 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[11 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantityRead = wordData[0];
                        // Lese starting Address Write
                        byteData[1] = bytes[12 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[13 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.startingAddressWrite = wordData[0];
                        // Lese quantity Write
                        byteData[1] = bytes[14 - 6 * Convert.ToInt32(serialFlag)];
                        byteData[0] = bytes[15 - 6 * Convert.ToInt32(serialFlag)];
                        Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                        receiveDataThread.quantityWrite = wordData[0];

                        receiveDataThread.byteCount = bytes[16 - 6 * Convert.ToInt32(serialFlag)];
                        receiveDataThread.receiveRegisterValues = new ushort[receiveDataThread.quantityWrite];
                        for (int i = 0; i < receiveDataThread.quantityWrite; i++)
                        {
                            // Lese Value
                            byteData[1] = bytes[17 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            byteData[0] = bytes[18 + i * 2 - 6 * Convert.ToInt32(serialFlag)];
                            Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, i * 2, 2);
                        }
                    }
                }
                catch (Exception exc)
                { }
                this.CreateAnswer(receiveDataThread, sendDataThread, stream, portIn, ipAddressIn);
                //this.sendAnswer();
                this.CreateLogData(receiveDataThread, sendDataThread);

                if (LogDataChanged != null)
                    LogDataChanged();
            }
        }
        #endregion

        #region Method CreateAnswer
        private void CreateAnswer(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {

            switch (receiveData.functionCode)
            {
                // Read Coils
                case 1:
                    if (!FunctionCode1Disabled)
                        this.ReadCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }
                    break;
                // Read Input Registers
                case 2:
                    if (!FunctionCode2Disabled)
                        this.ReadDiscreteInputs(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Read Holding Registers
                case 3:
                    if (!FunctionCode3Disabled)
                        this.ReadHoldingRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Read Input Registers
                case 4:
                    if (!FunctionCode4Disabled)
                        this.ReadInputRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Write single coil
                case 5:
                    if (!FunctionCode5Disabled)
                        this.WriteSingleCoil(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Write single register
                case 6:
                    if (!FunctionCode6Disabled)
                        this.WriteSingleRegister(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Write Multiple coils
                case 15:
                    if (!FunctionCode15Disabled)
                        this.WriteMultipleCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Write Multiple registers
                case 16:
                    if (!FunctionCode16Disabled)
                        this.WriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Error: Function Code not supported
                case 23:
                    if (!FunctionCode23Disabled)
                        this.ReadWriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    else
                    {
                        sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                        sendData.exceptionCode = 1;
                        sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    }

                    break;
                // Error: Function Code not supported
                default:
                    sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                    sendData.exceptionCode = 1;
                    sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
            }
            sendData.timeStamp = DateTime.Now;
        }
        #endregion

        private void ReadCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x07D0))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))     //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                if ((receiveData.quantity % 8) == 0)
                    sendData.byteCount = (byte)(receiveData.quantity / 8);
                else
                    sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);

                sendData.sendCoilValues = new bool[receiveData.quantity];
                lock (lockCoils)
                    Array.Copy(coils.localArray, receiveData.startingAdress + 1, sendData.sendCoilValues, 0, receiveData.quantity);
            }
            if (true)
            {
                Byte[] data;

                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];

                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];
                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;

                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendCoilValues = null;
                }

                if (sendData.sendCoilValues != null)
                    for (int i = 0; i < (sendData.byteCount); i++)
                    {
                        byteData = new byte[2];
                        for (int j = 0; j < 8; j++)
                        {

                            byte boolValue;
                            if (sendData.sendCoilValues[i * 8 + j] == true)
                                boolValue = 1;
                            else
                                boolValue = 0;
                            byteData[1] = (byte)((byteData[1]) | (boolValue << j));
                            if ((i * 8 + j + 1) >= sendData.sendCoilValues.Length)
                                break;
                        }
                        data[9 + i] = byteData[1];
                    }
                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void ReadDiscreteInputs(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x07D0))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                if ((receiveData.quantity % 8) == 0)
                    sendData.byteCount = (byte)(receiveData.quantity / 8);
                else
                    sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);

                sendData.sendCoilValues = new bool[receiveData.quantity];
                Array.Copy(discreteInputs.localArray, receiveData.startingAdress + 1, sendData.sendCoilValues, 0, receiveData.quantity);
            }
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;


                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendCoilValues = null;
                }

                if (sendData.sendCoilValues != null)
                    for (int i = 0; i < (sendData.byteCount); i++)
                    {
                        byteData = new byte[2];
                        for (int j = 0; j < 8; j++)
                        {

                            byte boolValue;
                            if (sendData.sendCoilValues[i * 8 + j] == true)
                                boolValue = 1;
                            else
                                boolValue = 0;
                            byteData[1] = (byte)((byteData[1]) | (boolValue << j));
                            if ((i * 8 + j + 1) >= sendData.sendCoilValues.Length)
                                break;
                        }
                        data[9 + i] = byteData[1];
                    }

                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void ReadHoldingRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x007D))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                sendData.byteCount = (byte)(2 * receiveData.quantity);
                sendData.sendRegisterValues = new Int16[receiveData.quantity];
                lock (lockHoldingRegisters)
                    Buffer.BlockCopy(holdingRegisters.localArray, receiveData.startingAdress * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantity * 2);
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = (ushort)(0x03 + sendData.byteCount);

            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;

                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }


                if (sendData.sendRegisterValues != null)
                    for (int i = 0; i < (sendData.byteCount / 2); i++)
                    {
                        byteData = BitConverter.GetBytes((Int16)sendData.sendRegisterValues[i]);
                        data[9 + i * 2] = byteData[1];
                        data[10 + i * 2] = byteData[0];
                    }
                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void ReadInputRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            if ((receiveData.quantity < 1) | (receiveData.quantity > 0x007D))  //Invalid quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1 + receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                sendData.byteCount = (byte)(2 * receiveData.quantity);
                sendData.sendRegisterValues = new Int16[receiveData.quantity];
                Buffer.BlockCopy(inputRegisters.localArray, receiveData.startingAdress * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantity * 2);
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = (ushort)(0x03 + sendData.byteCount);

            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;


                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }


                if (sendData.sendRegisterValues != null)
                    for (int i = 0; i < (sendData.byteCount / 2); i++)
                    {
                        byteData = BitConverter.GetBytes((Int16)sendData.sendRegisterValues[i]);
                        data[9 + i * 2] = byteData[1];
                        data[10 + i * 2] = byteData[0];
                    }
                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void WriteSingleCoil(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.receiveCoilValues = receiveData.receiveCoilValues;
            if ((receiveData.receiveCoilValues[0] != 0x0000) & (receiveData.receiveCoilValues[0] != 0xFF00))  //Invalid Value
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1) > 65535) | (receiveData.startingAdress < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                if (receiveData.receiveCoilValues[0] == 0xFF00)
                {
                    lock (lockCoils)
                        coils[receiveData.startingAdress + 1] = true;
                }
                if (receiveData.receiveCoilValues[0] == 0x0000)
                {
                    lock (lockCoils)
                        coils[receiveData.startingAdress + 1] = false;
                }
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = 0x06;

            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.receiveCoilValues[0]);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (CoilsChanged != null)
                    CoilsChanged(receiveData.startingAdress + 1, 1);
            }
        }

        private void WriteSingleRegister(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.receiveRegisterValues = receiveData.receiveRegisterValues;

            if ((receiveData.receiveRegisterValues[0] < 0x0000) | (receiveData.receiveRegisterValues[0] > 0xFFFF))  //Invalid Value
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if (((receiveData.startingAdress + 1) > 65535) | (receiveData.startingAdress < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                lock (lockHoldingRegisters)
                    holdingRegisters[receiveData.startingAdress + 1] = unchecked((short)receiveData.receiveRegisterValues[0]);
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = 0x06;

            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);


                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.receiveRegisterValues[0]);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (HoldingRegistersChanged != null)
                    HoldingRegistersChanged(receiveData.startingAdress + 1, 1);
            }
        }

        private void WriteMultipleCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.quantity = receiveData.quantity;

            if ((receiveData.quantity == 0x0000) | (receiveData.quantity > 0x07B0))  //Invalid Quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if ((((int)receiveData.startingAdress + 1 + (int)receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                lock (lockCoils)
                    for (int i = 0; i < receiveData.quantity; i++)
                    {
                        int shift = i % 16;
                        /*                if ((i == receiveData.quantity - 1) & (receiveData.quantity % 2 != 0))
                                        {
                                            if (shift < 8)
                                                shift = shift + 8;
                                            else
                                                shift = shift - 8;
                                        }*/
                        int mask = 0x1;
                        mask = mask << (shift);
                        if ((receiveData.receiveCoilValues[i / 16] & (ushort)mask) == 0)

                            coils[receiveData.startingAdress + i + 1] = false;
                        else

                            coils[receiveData.startingAdress + i + 1] = true;

                    }
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = 0x06;
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.quantity);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (CoilsChanged != null)
                    CoilsChanged(receiveData.startingAdress + 1, receiveData.quantity);
            }
        }

        private void WriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.quantity = receiveData.quantity;

            if ((receiveData.quantity == 0x0000) | (receiveData.quantity > 0x07B0))  //Invalid Quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if ((((int)receiveData.startingAdress + 1 + (int)receiveData.quantity) > 65535) | (receiveData.startingAdress < 0))   //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                lock (lockHoldingRegisters)
                    for (int i = 0; i < receiveData.quantity; i++)
                    {
                        holdingRegisters[receiveData.startingAdress + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                    }
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = 0x06;
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[12 + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;



                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
                    data[8] = byteData[1];
                    data[9] = byteData[0];
                    byteData = BitConverter.GetBytes((int)receiveData.quantity);
                    data[10] = byteData[1];
                    data[11] = byteData[0];
                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (HoldingRegistersChanged != null)
                    HoldingRegistersChanged(receiveData.startingAdress + 1, receiveData.quantity);
            }
        }

        private void ReadWriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;


            if ((receiveData.quantityRead < 0x0001) | (receiveData.quantityRead > 0x007D) | (receiveData.quantityWrite < 0x0001) | (receiveData.quantityWrite > 0x0079) | (receiveData.byteCount != (receiveData.quantityWrite * 2)))  //Invalid Quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 3;
            }
            if ((((int)receiveData.startingAddressRead + 1 + (int)receiveData.quantityRead) > 65535) | (((int)receiveData.startingAddressWrite + 1 + (int)receiveData.quantityWrite) > 65535) | (receiveData.quantityWrite < 0) | (receiveData.quantityRead < 0))    //Invalid Starting adress or Starting address + quantity
            {
                sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
                sendData.exceptionCode = 2;
            }
            if (sendData.exceptionCode == 0)
            {
                sendData.sendRegisterValues = new Int16[receiveData.quantityRead];
                lock (lockHoldingRegisters)
                    Buffer.BlockCopy(holdingRegisters.localArray, receiveData.startingAddressRead * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantityRead * 2);

                lock (holdingRegisters)
                    for (int i = 0; i < receiveData.quantityWrite; i++)
                    {
                        holdingRegisters[receiveData.startingAddressWrite + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                    }
                sendData.byteCount = (byte)(2 * receiveData.quantityRead);
            }
            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = Convert.ToUInt16(3 + 2 * receiveData.quantityRead);
            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];

                Byte[] byteData = new byte[2];

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;

                //Function Code
                data[7] = sendData.functionCode;

                //ByteCount
                data[8] = sendData.byteCount;


                if (sendData.exceptionCode > 0)
                {
                    data[7] = sendData.errorCode;
                    data[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    if (sendData.sendRegisterValues != null)
                        for (int i = 0; i < (sendData.byteCount / 2); i++)
                        {
                            byteData = BitConverter.GetBytes((Int16)sendData.sendRegisterValues[i]);
                            data[9 + i * 2] = byteData[1];
                            data[10 + i * 2] = byteData[0];
                        }

                }


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }

                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
                if (HoldingRegistersChanged != null)
                    HoldingRegistersChanged(receiveData.startingAddressWrite + 1, receiveData.quantityWrite);
            }
        }

        private void sendException(int errorCode, int exceptionCode, ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;

            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;

            sendData.unitIdentifier = receiveData.unitIdentifier;
            sendData.errorCode = (byte)errorCode;
            sendData.exceptionCode = (byte)exceptionCode;

            if (sendData.exceptionCode > 0)
                sendData.length = 0x03;
            else
                sendData.length = (ushort)(0x03 + sendData.byteCount);

            if (true)
            {
                Byte[] data;
                if (sendData.exceptionCode > 0)
                    data = new byte[9 + 2 * Convert.ToInt32(serialFlag)];
                else
                    data = new byte[9 + sendData.byteCount + 2 * Convert.ToInt32(serialFlag)];
                Byte[] byteData = new byte[2];
                sendData.length = (byte)(data.Length - 6);

                //Send Transaction identifier
                byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                data[0] = byteData[1];
                data[1] = byteData[0];

                //Send Protocol identifier
                byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                data[2] = byteData[1];
                data[3] = byteData[0];

                //Send length
                byteData = BitConverter.GetBytes((int)sendData.length);
                data[4] = byteData[1];
                data[5] = byteData[0];

                //Unit Identifier
                data[6] = sendData.unitIdentifier;


                data[7] = sendData.errorCode;
                data[8] = sendData.exceptionCode;


                try
                {
                    if (serialFlag)
                    {
                        if (!serialport.IsOpen)
                            throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
                        //Create CRC
                        sendData.crc = ModbusClient.calculateCRC(data, Convert.ToUInt16(data.Length - 8), 6);
                        byteData = BitConverter.GetBytes((int)sendData.crc);
                        data[data.Length - 2] = byteData[0];
                        data[data.Length - 1] = byteData[1];
                        serialport.Write(data, 6, data.Length - 6);
                        if (debug)
                        {
                            byte[] debugData = new byte[data.Length - 6];
                            Array.Copy(data, 6, debugData, 0, data.Length - 6);
                            if (debug) StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(debugData), System.DateTime.Now);
                        }
                    }
                    else if (udpFlag)
                    {
                        //UdpClient udpClient = new UdpClient();
                        IPEndPoint endPoint = new IPEndPoint(ipAddressIn, portIn);
                        udpClient.Send(data, data.Length, endPoint);

                    }
                    else
                    {
                        stream.Write(data, 0, data.Length);
                        if (debug) StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(data), System.DateTime.Now);
                    }
                }
                catch (Exception) { }
            }
        }

        private void CreateLogData(ModbusProtocol receiveData, ModbusProtocol sendData)
        {
            for (int i = 0; i < 98; i++)
            {
                modbusLogData[99 - i] = modbusLogData[99 - i - 2];

            }
            modbusLogData[0] = receiveData;
            modbusLogData[1] = sendData;

        }



        public int NumberOfConnections
        {
            get
            {
                return numberOfConnections;
            }
        }

        public ModbusProtocol[] ModbusLogData
        {
            get
            {
                return modbusLogData;
            }
        }

        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                port = value;


            }
        }

        public bool UDPFlag
        {
            get
            {
                return udpFlag;
            }
            set
            {
                udpFlag = value;
            }
        }

        public bool SerialFlag
        {
            get
            {
                return serialFlag;
            }
            set
            {
                serialFlag = value;
            }
        }

        public int Baudrate
        {
            get
            {
                return baudrate;
            }
            set
            {
                baudrate = value;
            }
        }

        public System.IO.Ports.Parity Parity
        {
            get
            {
                return parity;
            }
            set
            {
                parity = value;
            }
        }

        public System.IO.Ports.StopBits StopBits
        {
            get
            {
                return stopBits;
            }
            set
            {
                stopBits = value;
            }
        }

        public string SerialPort
        {
            get
            {
                return serialPort;
            }
            set
            {
                serialPort = value;
                if (serialPort != null)
                    serialFlag = true;
                else
                    serialFlag = false;
            }
        }

        public byte UnitIdentifier
        {
            get
            {
                return unitIdentifier;
            }
            set
            {
                unitIdentifier = value;
            }
        }




        /// <summary>
        /// Gets or Sets the Filename for the LogFile
        /// </summary>
        public string LogFileFilename
        {
            get
            {
                return StoreLogData.Instance.Filename;
            }
            set
            {
                StoreLogData.Instance.Filename = value;
                if (StoreLogData.Instance.Filename != null)
                    debug = true;
                else
                    debug = false;
            }
        }




        public class HoldingRegisters
        {
            public Int16[] localArray = new Int16[65535];
            ModbusServer modbusServer;

            public HoldingRegisters(EasyModbus.ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public Int16 this[int x]
            {
                get { return this.localArray[x]; }
                set
                {
                    this.localArray[x] = value;

                }
            }
        }

        public class InputRegisters
        {
            public Int16[] localArray = new Int16[65535];
            ModbusServer modbusServer;

            public InputRegisters(EasyModbus.ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public Int16 this[int x]
            {
                get { return this.localArray[x]; }
                set
                {
                    this.localArray[x] = value;

                }
            }
        }

        public class Coils
        {
            public bool[] localArray = new bool[65535];
            ModbusServer modbusServer;

            public Coils(EasyModbus.ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public bool this[int x]
            {
                get { return this.localArray[x]; }
                set
                {
                    this.localArray[x] = value;

                }
            }
        }

        public class DiscreteInputs
        {
            public bool[] localArray = new bool[65535];
            ModbusServer modbusServer;

            public DiscreteInputs(EasyModbus.ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public bool this[int x]
            {
                get { return this.localArray[x]; }
                set
                {
                    this.localArray[x] = value;

                }
            }


        }
    }
}
namespace EasyModbus.Exceptions
{
    /// <summary>
    /// Exception to be thrown if serial port is not opened
    /// </summary>
    public class SerialPortNotOpenedException : ModbusException
    {
        public SerialPortNotOpenedException()
         : base()
        {
        }

        public SerialPortNotOpenedException(string message)
            : base(message)
        {
        }

        public SerialPortNotOpenedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SerialPortNotOpenedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception to be thrown if Connection to Modbus device failed
    /// </summary>
    public class ConnectionException : ModbusException
    {
        public ConnectionException()
         : base()
        {
        }

        public ConnectionException(string message)
            : base(message)
        {
        }

        public ConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ConnectionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception to be thrown if Modbus Server returns error code "Function code not supported"
    /// </summary>
    public class FunctionCodeNotSupportedException : ModbusException
    {
        public FunctionCodeNotSupportedException()
         : base()
        {
        }

        public FunctionCodeNotSupportedException(string message)
            : base(message)
        {
        }

        public FunctionCodeNotSupportedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected FunctionCodeNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception to be thrown if Modbus Server returns error code "quantity invalid"
    /// </summary>
    public class QuantityInvalidException : ModbusException
    {
        public QuantityInvalidException()
         : base()
        {
        }

        public QuantityInvalidException(string message)
            : base(message)
        {
        }

        public QuantityInvalidException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected QuantityInvalidException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception to be thrown if Modbus Server returns error code "starting adddress and quantity invalid"
    /// </summary>
    public class StartingAddressInvalidException : ModbusException
    {
        public StartingAddressInvalidException()
         : base()
        {
        }

        public StartingAddressInvalidException(string message)
            : base(message)
        {
        }

        public StartingAddressInvalidException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected StartingAddressInvalidException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception to be thrown if Modbus Server returns error code "Function Code not executed (0x04)"
    /// </summary>
    public class ModbusException : Exception
    {
        public ModbusException()
         : base()
        {
        }

        public ModbusException(string message)
            : base(message)
        {
        }

        public ModbusException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ModbusException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception to be thrown if CRC Check failed
    /// </summary>
    public class CRCCheckFailedException : ModbusException
    {
        public CRCCheckFailedException()
         : base()
        {
        }

        public CRCCheckFailedException(string message)
            : base(message)
        {
        }

        public CRCCheckFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected CRCCheckFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

}