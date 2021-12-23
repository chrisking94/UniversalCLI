using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace UniversalCLI
{
    class Program
    {
        private static RpcClient client;

        static void Main(string[] args)
        {
            bool bCliMode = args.Length == 0;  // Interactive CLI mode or single command mode.

            // 1. Initialize RabbitMQ client.
            // 1.1 Acquire server url.
            string serverUrl = "";
            string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);
            string hostFilePath = strWorkPath + "\\ucli.host";
            if (File.Exists(hostFilePath)) serverUrl = File.ReadAllText(hostFilePath);
            while (string.IsNullOrEmpty(serverUrl))
            {
                Console.WriteLine("Please input AMPQS server address ( amqps://username:password@hostname:port/vhost ):");
                serverUrl = Console.ReadLine();
            }
            // 1.2 Connect.
            try
            {
                client = new RpcClient(serverUrl);
                File.WriteAllText(hostFilePath, serverUrl);  // Save server url in order to use next time.
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to connect RPC server: {serverUrl}");
                Console.WriteLine();
                Console.WriteLine($"Error message: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                if (bCliMode)
                {
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                }
                return;
            }
            // 1.3 Handshake.
            Handshake();

            // 2. Execute cmds.
            if (bCliMode)  // Run the cli executable directly.
            {
                while (true)
                {
                    var strCmd = Console.ReadLine();
                    var result = client.Call(new { type = "cmd", data = strCmd });
                    var resObj = ProcessResult(result);
                    Console.Write(resObj["prompt"]);
                }
            }
            else  // Run ucli as a cmd command.
            {
                var strCmd = string.Join(" ", args);
                var result = client.Call(new { type = "cmd", data = strCmd });
                ProcessResult(result);
            }

            client.Close();
        }

        private static JObject ProcessResult(string jsonString)
        {
            var resultJobj = JObject.Parse(jsonString);
            var type = resultJobj["type"].Value<string>();

            if ("error".Equals(type))
            {
                Console.WriteLine(resultJobj["data"]);
                if (resultJobj.TryGetValue("fix", out var value))
                {
                    string strVal = value.Value<string>();
                    if ("handshake" == strVal)
                    {
                        Handshake();
                    }
                }
            }
            else
            {
                string result = resultJobj["data"].Value<string>();
                if (!string.IsNullOrEmpty(result))
                {
                    Console.WriteLine(result);
                }
            }
            return resultJobj;
        }

        private static void Handshake()
        {
            Console.WriteLine("Handshaking...");
            var hsResStr = client.Call(new { type = "handshake" });
            var hsResObj = JObject.Parse(hsResStr);
            Console.WriteLine(hsResObj["data"]);
        }
    }
}
