using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

namespace UniversalCLI
{
    public class RpcClient
    {
        private const int DEFAULT_TIMEOUT = 6000;
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string replyQueueName;
        private readonly EventingBasicConsumer consumer;
        private readonly IBasicProperties props;
        private RpcResponse resultHandle;

        public RpcClient(string amqpsUrl)
        {
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(amqpsUrl);

            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            replyQueueName = channel.QueueDeclare().QueueName;
            consumer = new EventingBasicConsumer(channel);

            props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var response = Encoding.UTF8.GetString(body);
                var properties = ea.BasicProperties;
                if (properties.CorrelationId == correlationId)
                {
                    switch(properties.Type)
                    {
                        case "return":  // Return directly.
                            resultHandle.Push(response, 0);
                            resultHandle.Prompt = (string)properties.Headers["prompt"];
                            resultHandle.Close();
                            break;
                        case "yield":  // Enumeration.
                            resultHandle.Push(response, (int)properties.Headers["pack_num"]);
                            break;
                        case "close":  // Close enumeration.
                            resultHandle.Prompt = (string)properties.Headers["prompt"];
                            resultHandle.Close();
                            break;
                    }
                }
            };

            channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);
        }

        private RpcResponse Call(string message, int timeout=DEFAULT_TIMEOUT)
        {
            if (resultHandle != null && !resultHandle.Closed)
            {
                throw new Exception("Last result handler hasn't been closed yet!");
            }
            resultHandle = new RpcResponse(timeout) { Prompt=resultHandle?.Prompt };  // Inherit prompt.
            var messageBytes = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(
                exchange: "",
                routingKey: "rpc_queue",
                basicProperties: props,
                body: messageBytes);

            return resultHandle;
        }

        public RpcResponse Call(object cmdPack, int timeout=DEFAULT_TIMEOUT)
        {
            var message = JsonConvert.SerializeObject(cmdPack);
            var result = Call(message, timeout);
            return result;
        }

        public void Close()
        {
            connection.Close();
            resultHandle?.Close();
        }
    }
}
