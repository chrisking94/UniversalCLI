using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace UniversalCLI
{
    /// <summary>
    /// Synchronized handle.
    /// </summary>
    public sealed class RpcResponse
    {
        public bool Closed { get; private set; }

        /// <summary>
        /// Prompt returned by RPC server. Set by RPCClient.
        /// </summary>
        public string Prompt { get; internal set; }

        private readonly BlockingCollection<string> respQueue = new BlockingCollection<string>();
        private readonly Dictionary<int, string> disorderMessages = new Dictionary<int, string>();
        private readonly int timeout;
        private int nextPacknum;

        internal RpcResponse(int timeout)
        {
            this.nextPacknum = 0;
            this.timeout = timeout;
        }

        /// <summary>
        /// Get result produced by return.
        /// </summary>
        /// <returns></returns>
        public string GetSingleResult()
        {
            var result = GetResult().First();
            if (!Closed)
            {
                throw new Exception("Result contains multiple parts.");
            }
            return result;
        }
        
        /// <summary>
        /// Get result produced by yield.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetResult()
        {
            int timeCnt = 0;
            while (!Closed)
            {
                // Check queue.
                if (respQueue.Count > 0)
                {
                    yield return respQueue.Take();
                    timeCnt = 0;  // Reset.
                }
                else
                {
                    Thread.Sleep(10);
                    timeCnt += 10;
                }
                // Check timeout.
                if (timeCnt > timeout)
                {
                    throw new TimeoutException($"RpcResultHandle: Message waiting time reached timeout '{timeout} ms'!");
                }
            }
        }

        // Push a result part into this handler. Invoked by RpcClient.
        internal void Push(string resultPack, int packNum)
        {
            if (packNum == nextPacknum)  // Continuous package.
            {
                respQueue.Add(resultPack);
                nextPacknum += 1;
                // Check disorder queue. Find next package.
                lock (disorderMessages)
                {
                    while (disorderMessages.Count > 0)
                    {
                        if (disorderMessages.TryGetValue(nextPacknum, out var value))
                        {
                            respQueue.Add(value);
                            nextPacknum += 1;
                        }
                        else
                        {
                            break;  // Succeeding message not found.
                        }
                    }
                }
            }
            else  // Incontinous package.
            {
                lock (disorderMessages)
                {
                    disorderMessages.Add(packNum, resultPack);
                }
            }
        }

        // Close this handle. Operated by RpcClient.
        internal void Close()
        {
            this.Closed = true;
        }
    }
}
