using System;
using System.ServiceProcess;
using System.Text;
using System.IO.Ports;
using System.Net;
using System.IO;
using System.Threading;

namespace sps
{
    public class SerialPortExchange
    {
        private SerialPort port;
        string InputData = String.Empty;

        public bool Run(int Number, int Speed)
        {
            port = new SerialPort();
            port.DataReceived += new SerialDataReceivedEventHandler(DataReceived);

            port.PortName = "COM" + Number.ToString();
            port.BaudRate = Speed;
            try
            {
                port.Open();
                port.DataReceived += new SerialDataReceivedEventHandler(DataReceived);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public void WriteSignal()
        {
            byte[] byteBuffer = new byte[1];
            byteBuffer[0] = 0x0A;
            port.Write(byteBuffer, 0, 1);
        }

        public void Close()
        {
            if (port != null)
            {
                if (port.IsOpen)
                    port.Close();
                port = null;
            }
        }

        public string LastData()
        {
            return InputData;
        }

        public void ResetData()
        {
            InputData = "0";
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            InputData = "0";
            if (port != null)
            {
                if (port.IsOpen)
                {
                    try
                    {
                        var data = port.ReadExisting();
                        if (data.Trim().Length != 0)
                        {
                            InputData = data.Trim();
                        }
                    }
                    catch
                    {
                        InputData = "0";
                    }
                }
            }
        }
    }

    public partial class ServiceScale : ServiceBase
    {
        private HttpListener listener;
        private SerialPortExchange serial;
        private Thread httptrade;
        private bool stop = false;

        public ServiceScale()
        {
            InitializeComponent();
        }

        string OpenPort(string data)
        {
            // ожидается строка "4;9600" - "Номер порта;Скорость соединения"
            if (data.Length == 0)
                return bool.FalseString;
            string[] param = data.Split(';');
            if (serial != null)
                serial.Close();

            string res = "";

            try
            {
                serial = new SerialPortExchange();
                bool result = serial.Run(Int32.Parse(param[0]), Int32.Parse(param[1]));
                res = result.ToString();
            }
            catch
            {
                res = bool.FalseString;
            }

            return res;
        }

        string ClosePort()
        {
            if (serial != null)
            {
                serial.Close();
                serial = null;
            }
            return bool.TrueString;
        }

        string GetSerialData()
        {
            if (serial != null)
                return serial.LastData();
            else
                return "0";
        }

        string Reset()
        {
            if (serial != null)
                serial.ResetData();
            return bool.TrueString;
        }        

        protected void Go()
        {
            while (true)
            {
                if (stop)
                    break;

                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;

                string body = string.Empty;
                using (Stream receiveStream = request.InputStream)
                {
                    using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
                    {
                        body = readStream.ReadToEnd();
                    }
                }

                string result = string.Empty;

                try
                {                    
                    switch (request.Url.AbsolutePath.ToString())
                    {
                        case "/start":
                            result = OpenPort(body);
                            break;
                        case "/stop":
                            result = ClosePort();
                            break;
                        case "/reset":
                            result = Reset();
                            break;
                        case "/get":
                            result = GetSerialData();
                            break;
                        case "/quit":
                            result = "quit";
                            break;
                    }
                }
                catch
                {
                    result = "0";
                }

                if (result == "quit")
                    break;

                HttpListenerResponse response = context.Response;
                string responseString = result;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
        }

        protected override void OnStart(string[] args)
        {
            serial = null;
            stop = false;

            listener = new HttpListener();
            listener.Prefixes.Add("http://*:8119/");
            listener.Start();

            httptrade = new Thread(Go);
            httptrade.Start();          
        }

        protected override void OnStop()
        {
            stop = false;
            Thread.Sleep(1000);

            if (listener != null)
            {
                listener.Stop();
                listener.Close();
            }

            if (httptrade.ThreadState == System.Threading.ThreadState.Running)
                httptrade.Abort();

            ClosePort();
        }
    }
}
