using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.Reflection;

using System.Diagnostics;

namespace sample_http_server
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();

            Console.WriteLine("Sample Http Server 啟動...\n");

            while (true)
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                new Thread(new clinet_thread(tcpClient).start).Start();
            }

        }
        public class clinet_thread
        {
            TcpClient tcc;
            string basedir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string version = "HTTP/1.1";
            string HeadersString = "";
            string host = "";
            bool with_Slash = false;

            public bool enable_list_dir = true; //設定是否能夠列出目錄內容

            public clinet_thread(TcpClient tc)
            {
                tcc = tc;
            }

            public void start()
            {
                List<byte> request_send_str = new List<byte>();

                while (tcc.GetStream().DataAvailable) //把對方要求一次全抓的方式不是很好,最好分抓header跟抓body兩個部份處理.否則對方傳巨大的body會暴掉
                    request_send_str.Add((byte)tcc.GetStream().ReadByte());

                string request_str = Encoding.Default.GetString(request_send_str.ToArray());

                if (request_str == "") //不知道為何瀏覽器有時後會送空的request ??
                {
                    tcc.GetStream().Close(); // 不加這兩行有時候會卡死 ??
                    tcc.Close();
                    return;
                }

                Console.WriteLine(request_str); //列印requet send過來的內容   

                List<string> request_header = request_str.Split(new char[] { '\n' }).ToList(); // 如果對方有傳資料長度極長的body,這方法不好
                foreach (string i in request_header)
                {
                    if (i.Length > 7)
                        if (i.Substring(0, 5) == "Host:")
                        {
                            host = i;
                            break;
                        }
                }
                List<string> request_url = request_header[0].Split(new char[] { ' ' }).ToList();

                string method = request_url[0]; // <-- 請求的方式,目前僅對應get,在這裡可以視個人需求擴充

                if (method != "GET")
                {
                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                string res_target = Uri.UnescapeDataString(request_url[1]);
                byte[] body = null;

                FileInfo fi = null;
                try
                {

                    res_target = res_target.Replace("//", "/");
                    if (Directory.Exists(basedir + "/wwwroot" + res_target)) //res_target == "/")
                    {

                        if (res_target[res_target.Length - 1] != '/')
                            res_target = res_target + "/";
                        else with_Slash = true;

                        string index_page = "";
                        if (File.Exists(basedir + "/wwwroot" + res_target + "index.html"))
                            index_page = basedir + "/wwwroot" + res_target + "index.html"; //開啟預設首頁
                        else if (File.Exists(basedir + "/wwwroot" + res_target + "index.htm"))
                            index_page = basedir + "/wwwroot" + res_target + "index.htm";
                        else if (File.Exists(basedir + "/wwwroot" + res_target + "default.htm"))
                            index_page = basedir + "/wwwroot" + res_target + "default.htm";

                        body = File.ReadAllBytes(index_page);
                        fi = new FileInfo(index_page);
                    }
                    else
                    {
                        body = File.ReadAllBytes(basedir + "/wwwroot" + res_target);
                        fi = new FileInfo(basedir + "/wwwroot" + res_target);
                    }
                }
                catch
                {
                    if (!Directory.Exists(basedir + "/wwwroot" + res_target))
                        body = Encoding.Default.GetBytes("\"" + res_target + "\" Not Find");
                }

                HeadersString = version + " " + "200 Ok" + "\n"; //<--預設都成功,可視處理狀況,自行更換回應狀態

                if (fi != null)
                {
                    if (fi.Extension.ToLower() == ".htm" || fi.Extension.ToLower() == ".html")
                        HeadersString += "Content-Type: text/html\n"; //<--告訴瀏覽器資料內容為何,讓瀏覽器自行去做對應處理,預設範例為文字網頁,可自行擴充
                    else if (fi.Extension.ToLower() == ".png")
                        HeadersString += "Content-type: image/png";
                    else if (fi.Extension.ToLower() == ".jpg")
                        HeadersString += "Content-type: image/jpeg";
                    else if (fi.Extension.ToLower() == ".gif")
                        HeadersString += "Content-type: image/gif";
                    else
                        HeadersString += "Content-type: application/octet-stream";
                }
                else
                {
                    HeadersString += "Content-Type: text/html\n";

                    if (!Directory.Exists(basedir + "/wwwroot" + res_target) && !File.Exists(basedir + "/wwwroot" + res_target))
                        body = Encoding.Default.GetBytes("\"" + res_target + "\" Not Find");
                    else if (enable_list_dir == false)
                        body = Encoding.Default.GetBytes("No default Page !");
                    else
                        body = Encoding.Default.GetBytes(list_dir(basedir + "/wwwroot" + res_target));
                }

                HeadersString += "Connection: close\n";
                HeadersString += "\n"; //header資訊區已 \n\n為結尾,如果出現連續兩個\n,代表下面內容為body
                byte[] bHeadersString = Encoding.ASCII.GetBytes(HeadersString);
                tcc.GetStream().Write(bHeadersString, 0, bHeadersString.Length);
                tcc.GetStream().Write(body, 0, body.Length);
                tcc.GetStream().Close(); //一般的普遍request最後就斷開,不會保持連現
                tcc.Close();
            }

            public string list_dir(string dirpath)
            {
                if (with_Slash == false) //列出目錄的時候,如果目錄沒有以 / 結尾,需要重新導向 ex. http://localhost/test  ->  http://localhost/test/
                {
                    //建立永久轉向  http://sofree.cc/301-moved-permanently/
                    host = host.Replace("\r", "").Replace(" ", "").Substring(5);
                    HeadersString = HeadersString.Substring((version + " " + "200 Ok" + "\n").Length); //把原本 200 OK 的header 置換掉
                    HeadersString = version + " " + "301 Moved Permanently\n" + HeadersString; //送出導向命令header
                    HeadersString = HeadersString + "Location: http://" + host + dirpath.Substring((basedir + "/wwwroot").Length) + "\n\n";
                    return "";
                }

                string str = "";

                List<string> dirs = Directory.GetDirectories(dirpath).ToList();
                List<string> files = Directory.GetFiles(dirpath).ToList();

                foreach (string i in dirs)
                {
                    string item = i.Substring(dirpath.Length);
                    str += "<p style=\"margin-top: 0; margin-bottom: 0\"><a href=\"" + "" + item + "/\">" + item + "</a></p>\n";
                }
                foreach (string i in files)
                {
                    string item = i.Substring(dirpath.Length);
                    str += "<p style=\"margin-top: 0; margin-bottom: 0\"><a href=\"" + "" + item + "\">" + item + "</a></p>\n";
                }
                return str;
            }
        }
    }
}
