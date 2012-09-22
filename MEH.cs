using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;

/* 
 * Based on http://www.codeproject.com/Articles/137979/Simple-HTTP-Server-in-C
 * 
 * "Offered to the public domain for any use with no restriction
 * and also with no warranty of any kind, please enjoy. - David Jeske."
*/

namespace MEH {
  public class HttpProcessor {
    #region Private variables
    public TcpClient socket;
    public HttpServer srv;

    private Stream inputStream;
    public StreamReader inputData;
    public StreamWriter outputStream;

    public String http_method;
    public String http_path;
    public String http_protocol_versionstring;
    public Hashtable httpHeaders = new Hashtable();

    private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB
    #endregion

    public HttpProcessor(TcpClient s, HttpServer srv) {
      this.socket = s;
      this.srv = srv;
    }

    private string StreamReadLine(Stream inputStream) {
      int next_char;
      string data = "";
      while(true) {
        next_char = inputStream.ReadByte();
        if(next_char == '\n') { break; }
        if(next_char == '\r') { continue; }
        if(next_char == -1) { Thread.Sleep(1); continue; };
        data += Convert.ToChar(next_char);
      }

      return data;
    }

    internal void Process() {
      // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
      // "processed" view of the world, and we want the data raw after the headers
      inputStream = new BufferedStream(socket.GetStream());

      // we probably shouldn't be using a StreamWriter for all output from handlers either
      // - outputStream.BaseStream can be used to circumvent StreamWriter if
      outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
      try {
        ParseRequest();
        ReadHeaders();
        if(http_method.Equals("GET")) {
          HandleGETRequest();
        } else if(http_method.Equals("POST")) {
          HandlePOSTRequest();
        }
      } catch(Exception e) {
        System.Diagnostics.Debug.WriteLine("Kalon: " + e.ToString());
        Respond(HttpStatusCode.InternalServerError);
      }

      outputStream.Flush();

      inputStream = null;
      outputStream = null;            
      socket.Close();
    }

    private void ParseRequest() {
      String request = StreamReadLine(inputStream);
      string[] tokens = request.Split(' ');
      if(tokens.Length != 3) {
        throw new Exception("invalid HTTP request line");
      }
      http_method = tokens[0].ToUpper();
      http_path = tokens[1];
      http_protocol_versionstring = tokens[2];

      System.Diagnostics.Debug.WriteLine("Kalon: Processing " + request);
    }

    private void ReadHeaders() {
      String line;
      while((line = StreamReadLine(inputStream)) != null) {
        if(line.Equals("")) {
          return;
        }

        int separator = line.IndexOf(':');
        if(separator == -1) {
          throw new Exception("invalid HTTP header line: " + line);
        }

        String name = line.Substring(0, separator);
        int pos = separator + 1;
        while((pos < line.Length) && (line[pos] == ' ')) {
          pos++; // strip any spaces
        }

        string value = line.Substring(pos, line.Length - pos);
        httpHeaders[name] = value;
      }
    }

    private void HandleGETRequest() {
      srv.HandleGETRequest(this);
    }

    private const int BUF_SIZE = 4096;
    private void HandlePOSTRequest() {
      // this post data processing just reads everything into a memory stream.
      // this is fine for smallish things, but for large stuff we should really
      // hand an input stream to the request processor. However, the input stream 
      // we hand him needs to let him see the "end of the stream" at this content 
      // length, because otherwise he won't know when he's seen it all!

      int content_len = 0;
      MemoryStream ms = new MemoryStream();
      if(this.httpHeaders.ContainsKey("Content-Length")) {
        content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
        if(content_len > MAX_POST_SIZE) {
          throw new Exception(
              String.Format("POST Content-Length({0}) too big for this simple server", content_len));
        }
        byte[] buf = new byte[BUF_SIZE];
        int to_read = content_len;
        while(to_read > 0) {
          int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
          
          if(numread == 0) {
            if(to_read == 0) {
              break;
            } else {
              throw new Exception("client disconnected during post");
            }
          }
          to_read -= numread;
          ms.Write(buf, 0, numread);
        }
        ms.Seek(0, SeekOrigin.Begin);
      }

      inputData = new StreamReader(ms);
      srv.HandlePOSTRequest(this);
    }

    /// <summary>
    /// Write data to socket.
    /// </summary>
    /// <param name="status">Status code</param>
    /// <param name="headers">Headers (Content-Type, Connection and Length will be added automatically if missing)</param>
    /// <param name="body">Response</param>
    public void Respond(HttpStatusCode status, Dictionary<string, string> headers, string body) {
      outputStream.WriteLine(String.Format("HTTP/1.0 {0} {1}", ((int)status).ToString(), status.ToString()));
      if(!headers.ContainsKey("Content-Type")) headers.Add("Content-Type", "text/html");
      if(!headers.ContainsKey("Connection"))   headers.Add("Connection", "close");
      if(!headers.ContainsKey("Length"))       headers.Add("Length", body.Length.ToString());
      foreach(KeyValuePair<string, string> header in headers) {
        outputStream.WriteLine(header.Key + ": " + header.Value);
      }
      outputStream.WriteLine("");
      outputStream.WriteLine(body);
    }
    public void Respond(HttpStatusCode status, string body) {
      Respond(status, new Dictionary<string, string>(), body);
    }
    public void Respond(HttpStatusCode status) {
      Respond(status, status.ToString());
    }
    public void Respond(string body) {
      Respond(HttpStatusCode.OK, body);
    }
  }

  public class HttpServer {
    /// <summary>
    /// Action to be performed on a certain URL.
    /// </summary>
    /// <param name="matches">A GroupCollection instance with attributes that matched provided regular expression</param>
    /// <param name="processor">HTTP processor, see the respond method</param>
    public delegate void ResponseHandler(GroupCollection matches, HttpProcessor processor);

    #region Private variables
    protected IPAddress addr;
    protected int port;

    private bool is_active = true;
    private TcpListener listener;
    private Dictionary<string, ResponseHandler> getUrlMap = new Dictionary<string, ResponseHandler>();
    private Dictionary<string, ResponseHandler> postUrlMap = new Dictionary<string, ResponseHandler>();
    #endregion

    /// <summary>
    /// Base class of the HTTP server
    /// </summary>
    /// <param name="port">Port on which the server should run</param>
    public HttpServer(int port) {
      this.addr = IPAddress.Any;
      this.port = port;
    }

    /// <summary>
    /// Base class of the HTTP server
    /// </summary>
    /// <param name="addr">IP address to which the server shoud bind</param>
    /// <param name="port">Port on which the server should run</param>
    public HttpServer(IPAddress addr, int port) {
      this.addr = addr;
      this.port = port;
    }

    /// <summary>
    /// Starts the server.
    /// 
    /// Example usage:
    ///     class MyServer : HttpServer {}
    ///     ...
    ///     MyServer server = new MyServer(8080);
    ///     Thread serverThread = new Thread(new ThreadStarted(server.Listen));
    ///     serverThread.Start();
    /// </summary>
    public void Listen() {
      listener = new TcpListener(System.Net.IPAddress.Any, port);
      listener.Start();
      while(is_active) {
        TcpClient s = listener.AcceptTcpClient();
        HttpProcessor processor = new HttpProcessor(s, this);
        Thread thread = new Thread(new ThreadStart(processor.Process));
        thread.Start();
        Thread.Sleep(1);
      }
    }

    /// <summary>
    /// Maps a handler to a GET request.
    /// 
    /// Example usage:
    ///     class App : HttpServer {
    ///       App(port) : base(port) {
    ///         GET("/hi/([^/+])$", (m, p, i) => p.Respond("<h1>Hi, " + m[1].Value + "!</h1>");
    ///         GET("/", (m, p, i) => p.Respond("Hello there!"));
    ///       }
    ///     }
    /// </summary>
    /// <param name="path">Regular expression capturing the path portion of the URL</param>
    /// <param name="handler">Handler delegate</param>
    public void GET(string path, ResponseHandler handler) {
      getUrlMap.Add(path, handler);
    }

    /// <summary>
    /// Maps a handler to a POST request.
    /// 
    /// Example usage:
    ///     class App : HttpServer {
    ///       App(int port) : base(port) {
    ///         POST("/data", (m, p) => {
    ///           string data = p.inputData.ReadToEnd();
    ///           p.Respond(data);
    ///         });
    ///       }
    ///     }
    /// </summary>
    /// <param name="path">Regular expression capturing the path portion of the URL</param>
    /// <param name="handler">Handler delegate</param>
    public void POST(string path, ResponseHandler handler) {
      postUrlMap.Add(path, handler);
    }

    public delegate string FileProcessor(string path);

    /// <summary>
    /// Helper method for serving static files from the disk.
    /// 
    /// Example usage:
    ///     class App : HttpServer {
    ///         public App (int port) : base(int port) {
    ///           ServeFiles("/slides/(?<path>\d+).jpg$", "image/jpeg", "C:\\slides\\", (n) => "that-slide-" + n + ".jpg");
    ///           // GET /slides/1.jpg will serve C:\slides\that-slide-1.jpg
    ///           // GET /slides/230912/1.jpg will serrve C:\slides\230912\1.jpg
    ///         }
    ///     }
    /// </summary>
    /// <param name="path">Regular expression capturing the desired URL. Should have only one capture group named "path"</param>
    /// <param name="mimeType">MIME type of file(s) to be served</param>
    /// <param name="source_dir">Source directory (on the disk)</param>
    public void ServeFiles(string path, string mimeType, string source_dir, FileProcessor processor) {
      GET(path, (m, p) => {
        string filename = m["path"].Value;
        string abspath = source_dir + "\\" + processor.Invoke(filename);

        if(File.Exists(abspath)) {
          System.Diagnostics.Debug.WriteLine("meh: serving " + abspath);

          p.outputStream.WriteLine("HTTP/1.0 200 OK");
          p.outputStream.WriteLine("Conent-Type: " + mimeType);
          p.outputStream.WriteLine("Connection: close");
          using(var fs = new FileStream(abspath, FileMode.Open, FileAccess.Read)) {
            int size = (int)fs.Length;
            p.outputStream.WriteLine("Content-Length: " + size.ToString());
            p.outputStream.WriteLine(" ");
            var buffer = new byte[size];
            fs.Read(buffer, 0, size);
            p.outputStream.BaseStream.Write(buffer, 0, size);
          }
        } else {
          p.Respond(HttpStatusCode.NotFound);
        }
      });
    }

    private void InvokeResponseHandler(Dictionary<string, ResponseHandler> dict, HttpProcessor p) {
      foreach(KeyValuePair<string, ResponseHandler> pair in dict) {
        Match match = Regex.Match(p.http_path, pair.Key);
        if(match.Success) {
          try {
            pair.Value.Invoke(match.Groups, p);
          } catch {
            p.Respond(HttpStatusCode.InternalServerError, "Internal server error.");
          }
          return;
        }
      }

      p.Respond(HttpStatusCode.NotFound, "There's nothing here!");
    }

    internal void HandleGETRequest(HttpProcessor p) {
      InvokeResponseHandler(getUrlMap, p);
    }

    internal void HandlePOSTRequest(HttpProcessor p) {
      InvokeResponseHandler(postUrlMap, p);
    }
  }
}