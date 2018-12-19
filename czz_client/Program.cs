using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace czz_client
{
    class SKClient
    {
        private TcpClient tcpcz = null;

        public delegate void EVTConnectFinished(string ID);
        public event EVTConnectFinished EvtCF;

        public delegate void EVT_Command_Finished(byte header, string ID);
        public event EVT_Command_Finished EvtCommandF;

        private List<byte> Package;

        public SKClient()
        {
            tcpcz = new TcpClient();
            Package = new List<byte>();
        }

        ~SKClient()
        {
            CloseSocket();
        }

        private struct ConnectParam
        {
            public TcpClient _tcpcz;
            public string _ID;
        }

        public bool Connect(string IP, Int32 Port, string ID)
        {
            ConnectParam cp = new ConnectParam() { _tcpcz = tcpcz, _ID = ID};
            IAsyncResult ir = tcpcz.BeginConnect(IP, Port, new AsyncCallback(ConnectCallback), cp);

            return ir != null ? true : false;
        }

        NetworkStream stream;

        private void ConnectCallback(IAsyncResult ar)
        {
            ConnectParam cp = (ConnectParam)ar.AsyncState;

            try
            {
                if (cp._tcpcz.Connected)
                {
                    cp._tcpcz.EndConnect(ar);
                    stream = cp._tcpcz.GetStream();
                    EvtCF(cp._ID);
                }
            }
            catch (Exception)
            {
            }
        }
        
        public byte[] PreparePackage(byte _header, string ID)
        {
            byte[] header = { _header };
            byte[] tail = { 0 };

            if (_header == 1)
            {
                string IDFile = ID + "/ck_info.xml";
                FileStream fs = new FileStream(IDFile, FileMode.Open, FileAccess.Read, FileShare.None);
                StreamReader sr = new StreamReader(fs, Encoding.UTF8);

                string strIDFile = string.Format("{0};",ID) + sr.ReadToEnd();
                byte[] data = Encoding.UTF8.GetBytes(strIDFile);
                Int32 datalen = data.Length;

                int m = IPAddress.HostToNetworkOrder(datalen);
                byte[] len_data = BitConverter.GetBytes(m);

                byte checksum = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    checksum += data[i];
                }

                byte[] data_checksum = { checksum };

                byte[] arr_send = new byte[header.Length + len_data.Length + data.Length + data_checksum.Length + tail.Length];
                Buffer.BlockCopy(header, 0, arr_send, 0, header.Length);
                Buffer.BlockCopy(len_data, 0, arr_send, header.Length, len_data.Length);
                Buffer.BlockCopy(data, 0, arr_send, header.Length + len_data.Length, data.Length);
                Buffer.BlockCopy(data_checksum, 0, arr_send, header.Length + len_data.Length + data.Length, data_checksum.Length);
                Buffer.BlockCopy(tail, 0, arr_send, header.Length + len_data.Length + data.Length + data_checksum.Length, tail.Length);

                return arr_send;
            }
            else if (_header == 3 || _header == 5)
            {
                string strIDFile = string.Format("{0};", ID);
                byte[] data = Encoding.UTF8.GetBytes(strIDFile);
                Int32 datalen = data.Length;

                int m = IPAddress.HostToNetworkOrder(datalen);
                byte[] len_data = BitConverter.GetBytes(m);

                byte checksum = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    checksum += data[i];
                }

                byte[] data_checksum = { checksum };

                byte[] arr_send = new byte[header.Length + len_data.Length + data.Length + data_checksum.Length + tail.Length];
                Buffer.BlockCopy(header, 0, arr_send, 0, header.Length);
                Buffer.BlockCopy(len_data, 0, arr_send, header.Length, len_data.Length);
                Buffer.BlockCopy(data, 0, arr_send, header.Length + len_data.Length, data.Length);
                Buffer.BlockCopy(data_checksum, 0, arr_send, header.Length + len_data.Length + data.Length, data_checksum.Length);
                Buffer.BlockCopy(tail, 0, arr_send, header.Length + len_data.Length + data.Length + data_checksum.Length, tail.Length);

                return arr_send;
            }
            
            return null;
        }

        public void Send(byte[] package)
        {
            Package.Clear();

            stream.BeginWrite(package, 0, package.Length, new AsyncCallback(SendCallback), null);
        }

        private void SendCallback(IAsyncResult ar)
        {
            stream.EndWrite(ar);

            byte[] buffer = new byte[1024];
            stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(RecvCallBack), buffer);
        }

        private void RecvCallBack(IAsyncResult ar)
        {
            byte[] buffer = (byte[])ar.AsyncState;
            int readCount = stream.EndRead(ar);
            if (readCount > 0)
            {
                for (int i = 0; i < readCount; i++)
                {
                    Package.Add(buffer[i]);
                }
                byte[] package = Package.ToArray();
                bool bChecked = false;
                if (!CheckPackage(package, ref bChecked))
                {
                    stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(RecvCallBack), buffer);
                }
            }
        }

        private string Command(byte header, byte[] package_content)
        {
            string ret = null;

            List<char> ID = new List<char>();
            bool bIDFinished = false;
            List<char> Content = new List<char>();

            foreach (byte item in package_content)
            {
                if (!bIDFinished)
                {
                    if (item != ';')
                    {
                        ID.Add((char)item);
                    }
                    else
                    {
                        bIDFinished = true;
                    }
                }
                else
                {
                    Content.Add((char)item);
                }
            }

            string sID = new string(ID.ToArray());
            ret = sID;

            if (header == 1)
            {
                char[] content = Content.ToArray();
                string result = new string(content, 0, content.Length - 1);
            }
            else if (header == 3)
            {
                char[] content = Content.ToArray();
                string result = new string(content, 0, content.Length);

                if (result.Length == "no exist!".Length + 1)
                {
                    if (-1 != result.IndexOf("no exist!"))
                    {
                        return ret;
                    }
                }

                string IDFile = sID + "/strategy_info.xml";
                FileStream fs = new FileStream(IDFile, FileMode.Create, FileAccess.Write, FileShare.None);
                StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(false));

                sw.Write(result);
                sw.Close();
            }
            else if (header == 5)
            {
                char[] content = Content.ToArray();
                string result = new string(content, 0, content.Length);

                if (result.Length == "no exist!".Length + 1)
                {
                    if (-1 != result.IndexOf("no exist!"))
                    {
                        return ret;
                    }
                }

                string IDFile = sID + "/black_white_form_info.xml";
                FileStream fs = new FileStream(IDFile, FileMode.Create, FileAccess.Write, FileShare.None);
                StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(false));

                sw.Write(result);
                sw.Close();
            }

            return ret;
        }

        private bool CheckPackage(byte[] package, ref bool bChecked)
        {
            if (7 > package.Length)
            {
                return false;
            }
            else
            {
                if ((2 == package[0] || 4 == package[0] || 6 == package[0]) && 0 == package[package.Length - 1])
                {
                    int n = BitConverter.ToInt32(package, 1);
                    int m = IPAddress.NetworkToHostOrder(n);
                    if ((package.Length - 7) == m)
                    {
                        byte checksum = 0;
                        for(int i = 0; i < m; i++)
                        {
                            checksum += package[5 + i];
                        }
                        if (checksum == package[package.Length - 2])
                        {
                            bChecked = true;

                            byte[] content = new byte[m];
                            Buffer.BlockCopy(package, 5, content, 0, m);
                            string ID = Command((byte)(package[0] - 1), content);

                            EvtCommandF((byte)(package[0] - 1), ID);

                            return true;
                        }
                        else
                        {
                            bChecked = false;
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public void CloseSocket()
        {
            tcpcz.Close();
            tcpcz.Dispose();
        }
    };

    class Program
    {
        static SKClient cc;

        static void ConnectFinish(string ID)
        {
            byte[] arr_send = cc.PreparePackage(1, ID);
            cc.Send(arr_send);
        }

        static void CommandFinish(byte header, string ID)
        {
            if (header == 1)
            {
                byte[] arr_send = cc.PreparePackage(3, ID);
                cc.Send(arr_send);
            }
            else if (header == 3)
            {
                byte[] arr_send = cc.PreparePackage(5, ID);
                cc.Send(arr_send);
            }
        }

        static void Main(string[] args)
        {
            cc = new SKClient();
            cc.EvtCF += new SKClient.EVTConnectFinished(ConnectFinish);
            cc.EvtCommandF += new SKClient.EVT_Command_Finished(CommandFinish);
            cc.Connect("10.0.0.25", 8096, "zhangcl");

            Console.ReadKey();
        }
    }
}
