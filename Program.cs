using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ChatServer server = new ChatServer();
            server.Start();
        }
    }

    public class ChatServer
    {
        private TcpListener tcpListener;
        private Dictionary<TcpClient, string> clients = new Dictionary<TcpClient, string>();
        private readonly object locker = new object();

        public void Start()
        {
            tcpListener = new TcpListener(IPAddress.Any, 5000);
            tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); // Додано для повторного використання адреси
            tcpListener.Start();
            Console.WriteLine("Сервер запущено на порту 5000...");

            Console.CancelKeyPress += (sender, e) => // Для коректного завершення сервера
            {
                Console.WriteLine("Завершення сервера...");
                tcpListener.Stop();
                Environment.Exit(0);
            };

            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                Console.WriteLine("Клієнт з’єднався.");
                new Thread(() => HandleClient(client)).Start();
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            string nickname = "???";
            byte[] buffer = new byte[1024];
            int byteCount;

            try
            {
                // Чекаємо на JOIN-повідомлення
                byteCount = stream.Read(buffer, 0, buffer.Length);
                string firstMessage = Encoding.UTF8.GetString(buffer, 0, byteCount);

                if (firstMessage.StartsWith("[JOIN]"))
                {
                    nickname = firstMessage.Substring(6).Trim();

                    lock (locker)
                    {
                        clients[client] = nickname;
                    }

                    Console.WriteLine($"Користувач '{nickname}' приєднався.");
                    BroadcastSystemMessage($"{nickname} приєднався до чату.");
                    BroadcastUserList();
                }
                else
                {
                    Console.WriteLine("Клієнт не надіслав JOIN. Відключено.");
                    client.Close();
                    return;
                }

                while ((byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    Console.WriteLine($"[{nickname}] {message}");
                    BroadcastMessage(message);
                }
            }
            catch (Exception)
            {
                // Ігноруємо помилки з'єднання
            }
            finally
            {
                lock (locker)
                {
                    clients.Remove(client);
                }

                Console.WriteLine($"Користувач '{nickname}' вийшов.");
                BroadcastSystemMessage($"{nickname} вийшов з чату.");
                BroadcastUserList();
                client.Close();
            }
        }

        private void BroadcastMessage(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (locker)
            {
                foreach (var client in clients.Keys)
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch { /* Пропустити */ }
                }
            }
        }

        private void BroadcastSystemMessage(string message)
        {
            BroadcastMessage($"[Сервер]: {message}");
        }

        private void BroadcastUserList()
        {
            string userListMessage = "[USERS]" + string.Join(";", clients.Values);
            byte[] data = Encoding.UTF8.GetBytes(userListMessage);

            lock (locker)
            {
                foreach (var client in clients.Keys)
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch { /* Пропустити */ }
                }
            }
        }
    }
}
