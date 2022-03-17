using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            init();
            //撈取百度新聞
            GetBaidu();
            //撈取原價屋
            await GetCoolPC2();
        }
        static void init()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        static void GetBaidu()
        {
            Console.OutputEncoding = Encoding.Unicode;
            Crawler c = new Crawler();
            RequestOptions ro = new RequestOptions();
            ro.Uri = new Uri("https://news.baidu.com/");
            ro.Method = "GET";
            string res = c.RequestAction(ro);
            
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(res);
            HtmlNodeCollection liNodes = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='pane-news']").SelectSingleNode("div[1]/ul[1]").SelectNodes("li");
            if (liNodes != null && liNodes.Count > 0)
            {
                for (int i = 0; i < liNodes.Count; i++)
                {
                    string title = liNodes[i].SelectSingleNode("strong[1]/a[1]").InnerText.Trim();
                    string href = liNodes[i].SelectSingleNode("strong[1]/a[1]").GetAttributeValue("href", "").Trim();
                    Console.WriteLine("新聞標題：" + title + ",鏈接：" + href);
                }
            }
        }
        static async Task GetCoolPC2()
        {
            Crawler c = new Crawler();
            string res = await c.GetHttpClient("https://www.coolpc.com.tw/evaluate.php");
            
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(res);
            var list_type = new List<string>();
            HtmlNodeCollection nodes = htmlDoc.DocumentNode.SelectNodes("//select[@name='n4']//optgroup");

            foreach (HtmlNode node in nodes)
            {
                var type = node.Attributes["label"].Value;
                list_type.Add(type);
            }

            List<string> list_name = htmlDoc.DocumentNode.SelectSingleNode("//select[@name='n4']").InnerText.Split('\n').ToList();

            //刪除不必要的非商品選項
            list_name.RemoveRange(0, 3);
            list_name.RemoveAt(list_name.Count - 1);

            int number = 0;
            for (int i = 0; i < list_name.Count; i++)
            {
                string type = list_type[number];
                string name = list_name[i];

                if (name == "")
                {
                    number++;
                }
                else
                {
                    Console.WriteLine("類型：{0} ,", type);
                    Console.WriteLine("名稱：{0}", name);
                }
            }
        }
    }
    class Crawler
    {
        /// <summary>
        /// 實作HttpClient
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<string> GetHttpClient(string url)
        {
            try
            {
                string responseResult = string.Empty;
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36");//設置User-Agent，偽裝成Google Chrome瀏覽器
                var responseMessage = await httpClient.GetAsync(url); //發送請求
                if (responseMessage.IsSuccessStatusCode)
                {
                    string contentType = responseMessage.Content.Headers.ContentType.CharSet ?? Encoding.UTF8.HeaderName;
                    using (var sr = new StreamReader(responseMessage.Content.ReadAsStreamAsync().Result, Encoding.GetEncoding(contentType)))
                    {
                        responseResult = sr.ReadToEnd();
                    }
                }
                return responseResult;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        /// <summary>
        /// 實作HttpWebRequest
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public string RequestAction(RequestOptions options)
        {
            string result = string.Empty;
            IWebProxy proxy = null;//GetProxy();
            var request = (HttpWebRequest)WebRequest.Create(options.Uri);
            request.Accept = options.Accept;
            //在使用curl做POST的時候, 當要POST的數據大於1024位元組的時候, curl並不會直接就發起POST請求, 而是會分為倆步,
            //發送一個請求, 包含一個Expect: 100 -continue, 詢問Server使用願意接受數據
            //接收到Server返回的100 - continue應答以後, 才把數據POST給Server
            //並不是所有的Server都會正確應答100 -continue, 比如lighttpd, 就會返回417 “Expectation Failed”, 則會造成邏輯出錯.
            request.ServicePoint.Expect100Continue = false;
            request.ServicePoint.UseNagleAlgorithm = false;//禁止Nagle演算法加快載入速度
            if (!string.IsNullOrEmpty(options.XHRParams)) { request.AllowWriteStreamBuffering = true; } else { request.AllowWriteStreamBuffering = false; }; //禁止緩衝加快載入速度
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");//定義gzip壓縮頁面支持
            request.ContentType = options.ContentType;//定義文檔類型及編碼
            request.AllowAutoRedirect = options.AllowAutoRedirect;//禁止自動跳轉
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36";//設置User-Agent，偽裝成Google Chrome瀏覽器
            request.Timeout = options.Timeout;//定義請求超時時間為5秒
            request.KeepAlive = options.KeepAlive;//啟用長連接
            if (!string.IsNullOrEmpty(options.Referer)) request.Referer = options.Referer;//返回上一級歷史鏈接
            request.Method = options.Method;//定義請求方式為GET
            if (proxy != null) request.Proxy = proxy;//設置代理伺服器IP，偽裝請求地址
            if (!string.IsNullOrEmpty(options.RequestCookies)) request.Headers[HttpRequestHeader.Cookie] = options.RequestCookies;
            request.ServicePoint.ConnectionLimit = options.ConnectionLimit;//定義最大連接數
            if (options.WebHeader != null && options.WebHeader.Count > 0) request.Headers.Add(options.WebHeader);//添加頭部信息
            if (!string.IsNullOrEmpty(options.XHRParams))//如果是POST請求，加入POST數據
            {
                byte[] buffer = Encoding.UTF8.GetBytes(options.XHRParams);
                if (buffer != null)
                {
                    request.ContentLength = buffer.Length;
                    request.GetRequestStream().Write(buffer, 0, buffer.Length);
                }
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                ////獲取請求響應
                //foreach (Cookie cookie in response.Cookies)
                //    options.CookiesContainer.Add(cookie);//將Cookie加入容器，保存登錄狀態
                string contentType = response.CharacterSet ?? Encoding.UTF8.HeaderName;
                if (response.ContentEncoding.ToLower().Contains("gzip"))//解壓
                {
                    using (GZipStream stream = new GZipStream(response.GetResponseStream(), CompressionMode.Decompress))
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding(contentType)))
                        {
                            result = reader.ReadToEnd();
                        }
                    }
                }
                else if (response.ContentEncoding.ToLower().Contains("deflate"))//解壓
                {
                    using (DeflateStream stream = new DeflateStream(response.GetResponseStream(), CompressionMode.Decompress))
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding(contentType)))
                        {
                            result = reader.ReadToEnd();
                        }
                    }
                }
                else
                {
                    using (Stream stream = response.GetResponseStream())//原始
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            result = reader.ReadToEnd();
                        }
                    }
                }
            }
            request.Abort();
            return result;
        }
        /// <summary>
        /// 代理介接
        /// </summary>
        /// <returns></returns>
        private System.Net.WebProxy GetProxy()
        {
            System.Net.WebProxy webProxy = null;
            try
            {
                // 代理鏈接地址加埠
                string proxyHost = "192.168.1.1";
                string proxyPort = "9030";

                // 代理身份驗證的帳號跟密碼
                //string proxyUser = "xxx";
                //string proxyPass = "xxx";

                // 設置代理伺服器
                webProxy = new System.Net.WebProxy();
                // 設置代理地址加埠
                webProxy.Address = new Uri(string.Format("{0}:{1}", proxyHost, proxyPort));
                // 如果只是設置代理IP加埠，例如192.168.1.1:80，這裡直接註釋該段代碼，則不需要設置提交給代理伺服器進行身份驗證的帳號跟密碼。
                //webProxy.Credentials = new System.Net.NetworkCredential(proxyUser, proxyPass);
            }
            catch (Exception ex)
            {
                Console.WriteLine("獲取代理信息異常", DateTime.Now.ToString(), ex.Message);
            }
            return webProxy;
        }
    }
    class RequestOptions
    {
        /// <summary>
        /// 請求方式，GET或POST
        /// </summary>
        public string Method { get; set; }
        /// <summary>
        /// URL
        /// </summary>
        public Uri Uri { get; set; }
        /// <summary>
        /// 上一級歷史記錄鏈接
        /// </summary>
        public string Referer { get; set; }
        /// <summary>
        /// 超時時間（毫秒）
        /// </summary>
        public int Timeout = 15000;
        /// <summary>
        /// 啟用長連接
        /// </summary>
        public bool KeepAlive = true;
        /// <summary>
        /// 禁止自動跳轉
        /// </summary>
        public bool AllowAutoRedirect = false;
        /// <summary>
        /// 定義最大連接數
        /// </summary>
        public int ConnectionLimit = int.MaxValue;
        /// <summary>
        /// 請求次數
        /// </summary>
        public int RequestNum = 3;
        /// <summary>
        /// 可通過文件上傳提交的文件類型
        /// </summary>
        public string Accept = "*/*";
        /// <summary>
        /// 內容類型
        /// </summary>
        public string ContentType = "application/x-www-form-urlencoded";
        /// <summary>
        /// 實例化頭部信息
        /// </summary>
        private WebHeaderCollection header = new WebHeaderCollection();
        /// <summary>
        /// 頭部信息
        /// </summary>
        public WebHeaderCollection WebHeader
        {
            get { return header; }
            set { header = value; }
        }
        /// <summary>
        /// 定義請求Cookie字元串
        /// </summary>
        public string RequestCookies { get; set; }
        /// <summary>
        /// 非同步參數數據
        /// </summary>
        public string XHRParams { get; set; }
    }
}
