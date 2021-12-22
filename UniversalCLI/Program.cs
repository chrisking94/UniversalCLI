using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.IO;
using System.Text;

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
                Console.WriteLine("Please input AMPQS server address ( ampqs://username:password@hostname:port/vhost ):");
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

            // 2. Execute cmds.
            if (bCliMode)  // Run the cli executable directly.
            {
                Console.WriteLine(client.Call("handshake"));
                while (true)
                {
                    var strCmd = Console.ReadLine();
                    var result = client.Call(strCmd);
                    Console.Write(result);
                }
            }
            else  // Run ucli as a cmd command.
            {
                var strCmd = string.Join(" ", args);
                var result = client.Call(strCmd);
                Console.WriteLine(result);
            }

            client.Close();
        }
    }
}
