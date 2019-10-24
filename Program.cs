using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;

namespace BALANCA
{
    class Program
    {

        static TcpClient client;
        static Thread weightThread;
        static SerialPort mySerialPort;
        static string ip = "127.0.0.1";
        static int port = 80;
        static string balanceResult;
        static NetworkStream stream;


        static void Test() {
            Thread cur = Thread.CurrentThread;
            Console.WriteLine("TESTANDO TRHEAD {0}, {1}", cur.Name, cur.IsAlive);
        }

        static void WebSocket()
        {
            TcpListener server = new TcpListener(IPAddress.Parse(ip), port);
            server.Start();
            Console.WriteLine("Server listening at {0}:{1}", ip, port);
            client = server.AcceptTcpClient();
            Console.WriteLine("A client connected.");

            stream = client.GetStream();

            while (true)
            {
                while (!stream.DataAvailable) ;
                while (client.Available < 3) ;

                byte[] bytes = new byte[client.Available];
                stream.Read(bytes, 0, client.Available);
                string s = Encoding.UTF8.GetString(bytes);

                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                {
                    Console.WriteLine("=====Handshaking from client=====\n{0}", s);

                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                    // 3. Compute SHA-1 and Base64 hash of the new value
                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                    string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    bool fin = (bytes[0] & 0b10000000) != 0,
                        mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                    int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                        msglen = bytes[1] - 128, // & 0111 1111
                        offset = 2;

                    if (msglen == 126)
                    {
                        // was ToUInt16(bytes, offset) but the result is incorrect
                        msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                        offset = 4;
                    }
                    else if (msglen == 127)
                    {
                        Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                        // i don't really know the byte order, please edit this
                        // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                        // offset = 10;
                    }

                    if (msglen == 0)
                        Console.WriteLine("msglen == 0");
                    else if (mask)
                    {
                        byte[] decoded = new byte[msglen];
                        byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                        offset += 4;

                        for (int i = 0; i < msglen; ++i)
                            decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                        string text = Encoding.UTF8.GetString(decoded);
                        Console.WriteLine("{0}", text);

                        try {
                            mySerialPort.Close();
                        }catch(Exception e) {
                            Console.WriteLine("Falha ao fechar porta serial");
                            Console.WriteLine(e.ToString());
                            Console.WriteLine(e.StackTrace);
                        }

                        weightThread = new Thread(ReadWeight);
                        weightThread.Start();
                        Console.WriteLine("Resultado da balança: {0}", balanceResult);
                        // Cria uma nova thread
                        // Console.WriteLine("{0} com status: {1}", weightThread.Name, weightThread.ThreadState);
                       
                        // Thread w = new Thread(ReadWeight);
                        // w.Start();
                        // w.Abort();

                        //  Console.WriteLine("Is thread {1} is alive : {0}",
                        //     weightThread.IsAlive, weightThread.Name);

                        
                        // weightThread.Start();
                        // weightThread.Suspend();
                        

                        // string greeting = "000001231";
                        // byte[] payload = Encoding.UTF8.GetBytes(greeting);

                        // List<byte> bytesSend = new List<byte>();
                        // bytesSend.Add(129);
                        // bytesSend.Add(9); // 0000 0110
                        // bytesSend.AddRange(payload);
                        // var arr = bytesSend.ToArray();
                        // stream.Write(arr, 0, arr.Length);

                        // ReadWeight();

                    }
                    else
                        Console.WriteLine("mask bit not set");
                    // client.Close();
                    // stream.Close();

                    Console.WriteLine();
                }
            }


        }


        static void Main(string[] args)
        {

            Thread webSocketThread = new Thread(WebSocket);
            webSocketThread.Name = "Websocket Thread";
            webSocketThread.Start();

            // WebSocket();



            // ReadWeight();


        }

        static void ReadWeight()
        {

            mySerialPort = new SerialPort("COM2");

            mySerialPort.BaudRate = 2400;
            mySerialPort.Parity = Parity.None;
            mySerialPort.StopBits = StopBits.One;
            mySerialPort.DataBits = 8;
            mySerialPort.Handshake = Handshake.None;
            mySerialPort.Open();
            //Receita do pó mágico
            byte[] buf = new byte[] {
                5,
                13
            };
            //Jogando o pó mágico õõõõ
            mySerialPort.Write(buf, 0, buf.Length);
            mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);


            // Thread.CurrentThread.Abort();

            // Console.WriteLine("Press any key to continue...");
            // Console.WriteLine();
            // Console.ReadKey();
            // mySerialPort.Close();
            
        }

        private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;


            string indata = sp.ReadExisting();
            Console.WriteLine("Data Received: {0}", indata);
            balanceResult = indata.Substring(1, indata.Length - 2);
            //Mostrando o resultado, apagando os delimitadores
            Console.WriteLine(indata.Substring(1, indata.Length - 2));
            double peso = Int32.Parse(indata.Substring(1, indata.Length - 2)) / 1000.0;
            Console.WriteLine("peso: {0}", peso);

            string greeting = balanceResult.PadLeft(9, '0');
            byte[] payload = Encoding.UTF8.GetBytes(greeting);

            List<byte> bytesSend = new List<byte>();
            bytesSend.Add(129);
            bytesSend.Add(9); // 0000 0110
            bytesSend.AddRange(payload);
            var arr = bytesSend.ToArray();
            stream.Write(arr, 0, arr.Length);

            // Console.ReadKey();
        }
    }
}