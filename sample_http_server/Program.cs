#define  DynamicWebPage
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
using System.Windows.Forms;
using System.Xml;

namespace sample_http_server
{
    class Program
    {

        public static bool php_exe_exist = false;
        public static string php_Interpreter = "";

        static void Main(string[] args)
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();

            Console.WriteLine("Sample Http Server 啟動...\n\n");

            XmlDocument xml = new XmlDocument();
            xml.Load(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configure.xml");

            if (File.Exists(xml["setup"]["php_Interpreter"].InnerText))
            {
                php_exe_exist = true;
                php_Interpreter = xml["setup"]["php_Interpreter"].InnerText;
            }

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
            string version = "HTTP/1.0";
            string HeadersString = "";
            bool with_Slash = false;
            public bool enable_list_dir = true; //設定是否能夠列出目錄內容

            Dictionary<string, string> header_list = new Dictionary<string, string>();

            public clinet_thread(TcpClient tc)
            {
                tcc = tc;
            }

            public void start()
            {

                //尚未支援 KEEP ALIVE REQUEST http://ihower.tw/blog/archives/1517 所謂的持久連線
                //http://blog.darkthread.net/post-2013-04-25-weird-ie-cannot-display-webpage.aspx

                Console.WriteLine(tcc.GetHashCode().ToString() + " 新線程連入");

                List<byte> b_list = new List<byte>();

                byte[] body = null;
                byte[] bHeadersString = null;

                #region request 資料接收

                string request_str = "";

                int b_get =  0 ;
                try
                {
                    if ( tcc.Connected == true )
                        b_get = tcc.GetStream().ReadByte();
                }
                catch 
                {
                    Console.WriteLine("線程被切斷");
                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                while (b_get != -1)
                {
                    b_list.Add((byte)b_get);

                    if (tcc.GetStream().DataAvailable == true)
                        b_get = tcc.GetStream().ReadByte();
                    else
                    {
                        request_str = System.Text.Encoding.UTF8.GetString(b_list.ToArray());
                        break;
                    }
                }
                #endregion

                #region  header資料分析區


                string firstline = "";
                try
                {
                    firstline = request_str.Substring(0, request_str.IndexOf('\n') - 1);
                }
                catch
                {
                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }
                List<string> request_inf = firstline.Split(new char[] { ' ' }).ToList();

                string request_method = request_inf[0];
                string request_target = Uri.UnescapeDataString(request_inf[1]);
                string request_version = request_inf[2];

                string request_header_str = request_str.Substring(firstline.Length + 2, request_str.IndexOf("\r\n\r\n") - (firstline.Length + 2));

                foreach (string i in request_header_str.Split(new char[] { '\n' }))
                    header_list.Add(i.Substring(0, i.IndexOf(": ")), i.Remove(0, i.IndexOf(": ") + 2).Replace("\r", ""));

                #endregion

                //資料分析與資料接收兩區段code處理的方式不太好

                Console.ForegroundColor = ConsoleColor.Yellow;

                if (request_method != "PUT" && request_method != "POST")
                    Console.WriteLine(request_str); //列印requet send過來的內容   
                else
                    Console.WriteLine("PUT或POST 模式只列印header\n" + request_header_str); //因為put或是post body資料可能很長,而且常常會有特殊字碼,印出來卡很久...

                Console.ResetColor();

                //已支援的method呼叫方式 比較重要的還缺 HEAD OPTIONS 三個 PROPPATCH以及其他WEBDAB冷僻擴充method沒興趣...
                if (request_method != "GET" &&
                    request_method != "PROPFIND" &&
                    request_method != "MKCOL" &&
                    request_method != "MOVE" &&
                    request_method != "DELETE" &&
                    request_method != "COPY" &&
                    request_method != "PUT" &&
                    request_method != "POST" &&
                    request_method != "OPTIONS"
                    )
                {
                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                //--
                // 以後這邊要補header認證權限控制的程式部分下去,ok後才放行跑後面的流程
                //--

                if ( request_method == "OPTIONS")
                {
                    HeadersString = version + " " + "200 OK" + "\n";
                    HeadersString += "Content-Length: 0\n";
                    HeadersString += "Allow: GET,HEAD,OPTIONS,POST,TRACE\n";
                    HeadersString += "Content-Type: httpd/unix-directory\n\n";
                    tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
              
                    tcc.GetStream().Close();
                    tcc.Close();
                }

                if (request_method == "PUT")
                {
                    try
                    {

                        if (File.Exists(basedir + "/wwwroot" + request_target))
                            File.Delete(basedir + "/wwwroot" + request_target);

                        FileStream fs = File.Open(basedir + "/wwwroot" + request_target, FileMode.Append);

                        int start = System.Text.Encoding.UTF8.GetBytes(firstline + "\r\n" + request_header_str + "\r\n\r\n").Length;

                        if (b_list.Count != 0)
                            for (int i = start; i < b_list.Count; i++)
                                fs.WriteByte(b_list[i]);

                        fs.Close();

                        HeadersString = version + " " + "201 Created" + "\n\n";
                        tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                    }
                    catch
                    {
                        HeadersString = version + " " + "403 Forbidden" + "\n\n";
                        tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                    }

                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                if (request_method == "DELETE")
                {
                    try
                    {
                        //目錄刪除
                        if (request_target[request_target.Length - 1] == '/')
                        {
                            //非正規標準行為,整個目錄直接整個幹掉
                            Directory.Delete(basedir + "/wwwroot" + request_target.Remove(request_target.Length - 1), true);
                            HeadersString = version + " " + "201 Created" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                        else // 檔案刪除
                        {
                            File.Delete(basedir + "/wwwroot" + request_target);
                            HeadersString = version + " " + "201 Created" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                    }
                    catch
                    {
                    }

                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                if (request_method == "COPY")
                {
                    string des = header_list["Destination"];
                    string from = request_target;
                    string to = Uri.UnescapeDataString(des.Remove(0, ("http://" + header_list["Host"]).Length));
                    try
                    {
                        //處理目錄check
                        if (to[to.Length - 1] == '/')
                        {
                            //暫不支援
                            HeadersString = version + " " + "403 Forbidden" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                        else // 檔案處理
                        {
                            File.Copy(basedir + "/wwwroot" + from, basedir + "/wwwroot" + to);
                            HeadersString = version + " " + "201 Created" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                    }
                    catch
                    {
                    }

                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                if (request_method == "MOVE")
                {
                    string des = header_list["Destination"];
                    string from = request_target;
                    string to = Uri.UnescapeDataString(des.Remove(0, ("http://" + header_list["Host"]).Length));

                    try
                    {
                        //處理目錄check
                        if (to[to.Length - 1] == '/')
                        {
                            Directory.Move(basedir + "/wwwroot" + from.Remove(from.Length - 1), basedir + "/wwwroot" + to.Remove(to.Length - 1));
                            HeadersString = version + " " + "201 Created" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                        else // 檔案處理
                        {
                            File.Move(basedir + "/wwwroot" + from, basedir + "/wwwroot" + to);
                            HeadersString = version + " " + "201 Created" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                    }
                    catch
                    {
                    }

                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                if (request_method == "MKCOL")
                {
                    string dir = basedir + "/wwwroot" + request_target;
                    try
                    {
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                            HeadersString = version + " " + "201 Created" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                        else
                        {
                            HeadersString = version + " " + "403 Forbidden" + "\n\n";
                            tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                        }
                    }
                    catch
                    {
                    }

                    tcc.GetStream().Close();
                    tcc.Close();
                    return;
                }

                if (request_method == "PROPFIND")
                {

                    Stopwatch st = new Stopwatch();
                    st.Restart();

                    //--準備body start
                    request_target = request_target.Replace("//", "/");
                    string dir = basedir + "/wwwroot" + request_target;

                    List<string> dirs = Directory.GetDirectories(dir).ToList();
                    List<string> files = Directory.GetFiles(dir).ToList();

                    //其實這邊可以直接用string直接串xml str , 不過練習一下xml物件用法
                    XmlDocument xmlbody = new XmlDocument();

                    XmlDeclaration xmldecl;
                    xmldecl = xmlbody.CreateXmlDeclaration("1.0", "utf-8", "yes");

                    var stringWriter = new StringWriter();

                    XmlWriterSettings xs = new XmlWriterSettings();
                    xs.Encoding = Encoding.UTF8;

                    XmlElement multistatus = xmlbody.CreateElement("D", "multistatus", "DAV:");
                    xmlbody.AppendChild(multistatus);
                    xmlbody.DocumentElement.SetAttribute("xmlns:D", "DAV:");

                    int th = 0;

                    FileInfo fi = null;
                    foreach (string i in files)
                    {

                        fi = new FileInfo(i);

                        string dir_target = Uri.EscapeUriString(i.Remove(0, (basedir + "/wwwroot").Length));
                        xmlbody.ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "response", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].AppendChild(xmlbody.CreateElement("D", "href", "DAV:")); // 目錄位置
                        xmlbody.ChildNodes[0].ChildNodes[th]["D:href"].InnerText = dir_target; // 目錄位置
                        xmlbody.ChildNodes[0].ChildNodes[th].AppendChild(xmlbody.CreateElement("D", "propstat", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].AppendChild(xmlbody.CreateElement("D", "prop", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1]["D:prop"].SetAttribute("xmlns:R", "http://ns.example.com/boxschema/");
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].AppendChild(xmlbody.CreateElement("D", "status", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1]["D:status"].InnerText = "HTTP/1.1 200 OK";

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "creationdate", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:creationdate"].InnerText = fi.CreationTime.Year + "-" + fi.CreationTime.Month + "-" + fi.CreationTime.Day + "T" + fi.CreationTime.Hour + ":" + fi.CreationTime.Minute + ":" + fi.CreationTime.Second + "Z";

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "getlastmodified", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:getlastmodified"].InnerText = fi.LastWriteTime.Year + "-" + fi.LastWriteTime.Month + "-" + fi.LastWriteTime.Day + "T" + fi.LastWriteTime.Hour + ":" + fi.LastWriteTime.Minute + ":" + fi.LastWriteTime.Second + "Z";

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "resourcetype", "DAV:"));

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "getcontentlength", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:getcontentlength"].InnerText = fi.Length.ToString();
                        th++;
                    }
                    foreach (string i in dirs)
                    {
                        string dir_target = Uri.EscapeUriString(i.Remove(0, (basedir + "/wwwroot").Length) + "/");

                        DirectoryInfo df = new DirectoryInfo(i);

                        xmlbody.ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "response", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].AppendChild(xmlbody.CreateElement("D", "href", "DAV:")); // 目錄位置
                        xmlbody.ChildNodes[0].ChildNodes[th]["D:href"].InnerText = dir_target; // 目錄位置
                        xmlbody.ChildNodes[0].ChildNodes[th].AppendChild(xmlbody.CreateElement("D", "propstat", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].AppendChild(xmlbody.CreateElement("D", "prop", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1]["D:prop"].SetAttribute("xmlns:R", "none");
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].AppendChild(xmlbody.CreateElement("D", "status", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1]["D:status"].InnerText = "HTTP/1.1 200 OK";

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "creationdate", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:creationdate"].InnerText = df.CreationTime.Year + "-" + df.CreationTime.Month + "-" + df.CreationTime.Day + "T" + df.CreationTime.Hour + ":" + df.CreationTime.Minute + ":" + df.CreationTime.Second + "Z";

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "getlastmodified", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:getlastmodified"].InnerText = df.LastWriteTime.Year + "-" + df.LastWriteTime.Month + "-" + df.LastWriteTime.Day + "T" + df.LastWriteTime.Hour + ":" + df.LastWriteTime.Minute + ":" + df.LastWriteTime.Second + "Z";

                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "resourcetype", "DAV:"));
                        xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:resourcetype"].AppendChild(xmlbody.CreateElement("D", "collection", "DAV:"));

                       // xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0].AppendChild(xmlbody.CreateElement("D", "getcontenttype", "DAV:"));
                       // xmlbody.ChildNodes[0].ChildNodes[th].ChildNodes[1].ChildNodes[0]["D:getcontenttype"].InnerText = "httpd/unix-directory";


                        // <D:getcontenttype>httpd/unix-directory</D:getcontenttype>

                        th++;
                    }

                    XmlElement root = xmlbody.DocumentElement;
                    xmlbody.InsertBefore(xmldecl, root);

                    var xmlTextWriter = XmlWriter.Create(stringWriter, xs);
                    xmlbody.WriteTo(xmlTextWriter);
                    xmlTextWriter.Flush();

                    string xmlbody_str = stringWriter.GetStringBuilder().ToString();

                    //--準備body end
                    body = System.Text.Encoding.UTF8.GetBytes(xmlbody_str);
                    HeadersString = version + " " + "207 Multi-Status" + "\n"; //<--預設都成功,可視處理狀況,自行更換回應狀態
                    HeadersString += "Content-Type: application/xml; charset=\"utf-8\"" + "\n";
                    HeadersString += "Content-Length: " + (body.Length).ToString() + "\n";
                    HeadersString += "\n";

                    st.Stop();

                    //http://stackoverflow.com/questions/750574/how-to-get-memory-available-or-used-in-c-sharp
                    Console.WriteLine("debug info : cost " + (st.ElapsedMilliseconds / 1000.0) + " s , memory used : " + (GC.GetTotalMemory(true) / 1024) + " KB");

                    tcc.GetStream().Write(Encoding.ASCII.GetBytes(HeadersString), 0, Encoding.ASCII.GetBytes(HeadersString).Length);
                    tcc.GetStream().Write(body, 0, body.Length);
                    tcc.GetStream().Close(); //一般的普遍request最後就斷開,不會保持連現
                    tcc.Close();

                    return;
                }

                //一堆if.....
                if (request_method == "GET" || request_method == "POST")
                {

                    #region 實驗性質 CGI & PHP(採傳統CGI模式去運作) 動態頁面支援程式碼部分
#if DynamicWebPage

                    //--檢查是否符合執行條件
                    bool run_dpage = false;
                    string dpage_type = "";

                    //env 變數
                    string QUERY_STRING = "";
                    string REMOTE_ADDR = "";




                    string script = basedir + "\\wwwroot" + request_target;

                    if (script.IndexOf('?') != -1)
                    {
                        script = script.Substring(0, script.IndexOf('?'));
                        QUERY_STRING = (basedir + "\\wwwroot" + request_target).Remove(0, script.Length + 1);
                    }

                    int start = System.Text.Encoding.UTF8.GetBytes(firstline + "\r\n" + request_header_str + "\r\n\r\n").Length;

                    if (File.Exists(script))
                    {
                        if (new FileInfo(script).Extension.ToLower() == ".php" && php_exe_exist == true)
                        {
                            run_dpage = true;
                            dpage_type = "php";
                        }

                        if (new FileInfo(script).Extension.ToLower() == ".pl")
                        {
                            run_dpage = true;
                            dpage_type = "perl";
                        }
                    }

                    if (run_dpage == true)
                    {
                        Stopwatch st = new Stopwatch();
                        st.Restart();

                        string path = "";

                        List<string> p_tmp = request_target.Split(new char[]{'?'})[0].Split(new char[] { '/' }).ToList();
                        p_tmp.RemoveAt(0);
                        p_tmp.RemoveAt(p_tmp.Count - 1);

                        path = "\\"+ String.Join("\\", p_tmp);
                        Process run_page = new Process();
                        run_page.StartInfo.CreateNoWindow = true;

                        //linux上的處理概念會直接用script開頭 #!/xxx/xxx/直譯器 來指定script直譯器所在位置
                        if (dpage_type == "perl")
                            run_page.StartInfo.FileName = basedir + "\\bin\\perl.exe"; //改為自帶預設
                        //@"C:\Perl\bin\perl.exe"; //<-- 需要在安裝perl的環境,先寫死,以後可以弄成像apache config配置概念

                        if (dpage_type == "php")
                            run_page.StartInfo.FileName = php_Interpreter;

                        run_page.StartInfo.Arguments = "\"" + script + "\"";

                        if (dpage_type == "php")
                            run_page.StartInfo.Arguments = "\"" + script + "\"";

                        #region 環境變數注入 (GET 模式不會有 stdioin 轉向 )

                        //editing   ref http://www.ietf.org/rfc/rfc3875  
                        run_page.StartInfo.EnvironmentVariables.Add("QUERY_STRING", QUERY_STRING); //-- QUERY_STRING 注入可以讓CGI讀取 script.pl?aaa=11&ccc=22 後面的所帶參數


                        //REMOTE_ADDR
                        run_page.StartInfo.EnvironmentVariables.Add("REMOTE_ADDR", header_list["Host"].Split(new char[] { ':' })[0]);


                        //CONTENT_LENGTH for POST METHOD
                        int CONTENT_LENGTH = 0;
                        CONTENT_LENGTH = b_list.Count - start;
                        run_page.StartInfo.EnvironmentVariables.Add("CONTENT_LENGTH", CONTENT_LENGTH.ToString());
                        #endregion
                        run_page.StartInfo.RedirectStandardInput = true;
                        run_page.StartInfo.RedirectStandardOutput = true;
                        run_page.StartInfo.WorkingDirectory = basedir + @"\wwwroot"+path;

                        run_page.StartInfo.UseShellExecute = false;

                        run_page.Start(); //<-- CGI 技術致命傷, 每一次REQUEST,都會建立一個process,對記憶體和效能是負擔,後期有很多改善技術

                        //處理 STDIOIN 轉向
                        if (request_method == "POST")
                        {
                            if (b_list.Count != 0)
                                for (int i = start; i < b_list.Count; i++)
                                    run_page.StandardInput.BaseStream.WriteByte(b_list[i]);
                            run_page.StandardInput.Close();
                        }

                        HeadersString = version + " " + "200 Ok" + "\n";

                        //嘗試過加底下header參數,但怎樣都矯正不了 IE 的 kepp-alive 行為
                        HeadersString += "Cache-control: no-cache,max-age=0,must-revalidate\n";
                        HeadersString += "Pragma: no-cache\n";
                        /*HeadersString += "Keep-Alive: timeout=0";*/

                        HeadersString += "Connection: close\n";

                        if (dpage_type == "php")
                            HeadersString += "Content-Type: text/html\n"; //<-- CGI 輸出資料類型標頭由CGI程式輸出處理,因此不需要前置輸出

                        if (dpage_type == "php")
                            HeadersString += "\n"; //header資訊區已 \n\n為結尾,如果出現連續兩個\n,代表下面內容為body <--由CGI程式處理

                        bHeadersString = Encoding.ASCII.GetBytes(HeadersString);
                        tcc.GetStream().Write(bHeadersString, 0, bHeadersString.Length);

                        int b = run_page.StandardOutput.BaseStream.ReadByte();

                        try
                        {
                            while (b != -1)
                            {
                                tcc.GetStream().WriteByte((byte)b);
                                b = run_page.StandardOutput.BaseStream.ReadByte();
                            }
                        }
                        catch
                        {
                            return;
                        }

                        run_page.StandardOutput.BaseStream.Close();

                        tcc.GetStream().Close();
                        tcc.Close();

                        st.Stop();
                        Console.WriteLine("debug info : cgi or php cost " + (st.ElapsedMilliseconds / 1000.0).ToString() + " s");

                        return;
                    }
#endif
                    #endregion

                    //一般非動態網頁的GET處理流程
                    FileInfo fi = null;
                    try
                    {
                        request_target = request_target.Replace("//", "/");
                        if (Directory.Exists(basedir + "/wwwroot" + request_target)) //res_target == "/")
                        {
                            if (request_target[request_target.Length - 1] != '/')
                                request_target = request_target + "/";
                            else with_Slash = true;

                            string index_page = "";

                            if (File.Exists(basedir + "/wwwroot" + request_target + "index.html"))
                                index_page = basedir + "/wwwroot" + request_target + "index.html"; //開啟預設首頁
                            else if (File.Exists(basedir + "/wwwroot" + request_target + "index.htm"))
                                index_page = basedir + "/wwwroot" + request_target + "index.htm";
                            else if (File.Exists(basedir + "/wwwroot" + request_target + "default.htm"))
                                index_page = basedir + "/wwwroot" + request_target + "default.htm";

                            body = File.ReadAllBytes(index_page);
                            fi = new FileInfo(index_page);
                        }
                        else
                        {
                            body = File.ReadAllBytes(basedir + "/wwwroot" + request_target);
                            fi = new FileInfo(basedir + "/wwwroot" + request_target);
                        }
                    }
                    catch
                    {
                        if (!Directory.Exists(basedir + "/wwwroot" + request_target))
                            body = Encoding.UTF8.GetBytes("\"" + request_target + "\" Not Find");
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

                        if (!Directory.Exists(basedir + "/wwwroot" + request_target) && !File.Exists(basedir + "/wwwroot" + request_target))
                            body = Encoding.UTF8.GetBytes("\"" + request_target + "\" Not Find");
                        else if (enable_list_dir == false)
                            body = Encoding.UTF8.GetBytes("No default Page !");
                        else
                            body = Encoding.UTF8.GetBytes(list_dir(basedir + "/wwwroot" + request_target));
                    }

                    HeadersString += "Connection: close\n";
                    HeadersString += "\n"; //header資訊區已 \n\n為結尾,如果出現連續兩個\n,代表下面內容為body
                    bHeadersString = Encoding.ASCII.GetBytes(HeadersString);
                    try
                    {
                        tcc.GetStream().Write(bHeadersString, 0, bHeadersString.Length);
                        tcc.GetStream().Write(body, 0, body.Length);
                        tcc.GetStream().Close(); //一般的普遍request最後就斷開,不會保持連現
                        tcc.Close();
                    }
                    catch
                    {
                       // MessageBox.Show("*");
                        //err++;
                        //Debug.WriteLine ( "close : " + err.ToString() ); 
                       // err++;
                       // Console.WriteLine("close : " + err.ToString() );
                    }
                }
            }

            int err = 0;

            public string list_dir(string dirpath)
            {
                if (with_Slash == false) //列出目錄的時候,如果目錄沒有以 / 結尾,需要重新導向 ex. http://localhost/test  ->  http://localhost/test/
                {
                    //建立永久轉向  http://sofree.cc/301-moved-permanently/
                    HeadersString = HeadersString.Substring((version + " " + "200 Ok" + "\n").Length); //把原本 200 OK 的header 置換掉
                    HeadersString = version + " " + "301 Moved Permanently\n" + HeadersString; //送出導向命令header
                    HeadersString = HeadersString + "Location: http://" + header_list["Host"] + dirpath.Substring((basedir + "/wwwroot").Length) + "\n\n";
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