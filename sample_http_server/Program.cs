using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.Reflection;

namespace sample_http_server
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();

            while (true)
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                clinet_deal_thread(tcpClient);
            }

        }
        static public void clinet_deal_thread(TcpClient tc)
        {
            clinet_thread clt = new clinet_thread(tc);
            Thread c = new Thread(clt.start);
            c.Start();
        }

        public class clinet_thread
        {
            
            TcpClient tcc;
            string basedir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string version = "HTTP/1.1";

            public clinet_thread(TcpClient tc)
            {
                tcc = tc;
            }

            public void start()
            {

                List<byte> request_send_str = new List<byte>();
                do
                {
                    request_send_str.Add((byte)tcc.GetStream().ReadByte());
                } while (tcc.GetStream().DataAvailable);

                string request_str = Encoding.Default.GetString(request_send_str.ToArray() ) ;

                if (request_str == "")
                    return;

                Console.WriteLine ( "新的 Request : \n" + request_str );   

                //分析要求,目前僅對應get
                List<string> request_header = request_str.Split(new char[] { '\n' }).ToList();
                Console.WriteLine(request_header[0]);
                List<string> request_url = request_header[0].Split(new char[] { ' ' }).ToList();
                
                string method = request_url[0]; // <-- 請求的方式,目前僅對應get

                if (method != "GET")
                    return;

                string res_target = Uri.UnescapeDataString(request_url[1]);
                //
               
                byte[] body = null;

                try
                {
                    if (res_target == "/")
                        body = File.ReadAllBytes(basedir + "/wwwroot/index.html"); //開啟預設首頁
                    else
                        body = File.ReadAllBytes(basedir + "/wwwroot" + res_target);
                }
                catch
                {
                    body = Encoding.Default.GetBytes ( "File Not Find" ) ;
                }

             
                string HeadersString = version + " " + "200 Ok" + "\n"; //<--預設都成功,可視處理狀況,自行更換回應狀態
                
                HeadersString += "Content-Type: text/html\n"; //<--告訴瀏覽器資料內容為何,讓瀏覽器自行去做對應處理,預設範例為文字網頁,可自行擴充
                HeadersString += "\n"; //header資訊區已 \n\n為結尾,如果出現連續兩個\n,代表下面內容為body

                byte[] bHeadersString = Encoding.ASCII.GetBytes(HeadersString);

                tcc.GetStream().Write(bHeadersString, 0, bHeadersString.Length);
                tcc.GetStream().Write(body, 0, body.Length);
                tcc.GetStream().Close(); //一般的普遍request最後就斷開,不會保持連現
            }
        }
    }
}
