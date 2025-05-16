using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    static List<TcpClient> clients = new List<TcpClient>();
    static Dictionary<TcpClient, string> clientNames = new Dictionary<TcpClient, string>();
    const string historyPath = "history.txt";

    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, 8888);
        server.Start();
        Console.WriteLine("[SERVER] Запущено...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            lock (clients)
            {
                clients.Add(client);
            }
            new Thread(() => HandleClient(client)).Start();
        }
    }


    static void HandleClient(TcpClient client)
    {
        try
        {
            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            var netStream = client.GetStream();

            while (true)
            {
                string data = reader.ReadLine();
                if (string.IsNullOrEmpty(data)) break;

                string[] parts = data.Split('|', 3);
                if (parts.Length < 3) continue;

                string type = parts[0], name = parts[1], payload = parts[2];

                if (type == "JOIN")
                {
                    lock (clients)
                        clientNames[client] = name;

                    Broadcast($"COMMAND|SERVER|USER_JOINED:{name}");
                    Send(client, $"COMMAND|SERVER|USER_LIST:{string.Join(",", clientNames.Values)}");
                    continue;
                }
                else if (type == "EXIT")
                {
                    lock (clients)
                    {
                        clients.Remove(client);
                        clientNames.Remove(client);
                    }
                    Broadcast($"COMMAND|SERVER|USER_LEFT:{name}");
                    client.Close();
                    Console.WriteLine($"[-] {name} відключився");
                    return;
                }
                else if (type == "AUDIO" || type == "VIDEO" || type == "IMAGE")
                {
                    int length = int.Parse(payload);
                    byte[] buffer = new byte[length];
                    int totalRead = 0;

                    while (totalRead < length)
                    {
                        int bytesRead = netStream.Read(buffer, totalRead, length - totalRead);
                        if (bytesRead == 0) break;
                        totalRead += bytesRead;
                    }

                    // Спочатку передаємо команду, потім — байти
                    Broadcast($"{type}|{name}|{length}");
                    BroadcastBinary(buffer);
                    continue;
                }

                // Текстове повідомлення
                File.AppendAllText(historyPath, $"[{DateTime.Now}] {name}: {payload}\n");
                Broadcast(data);
            }
        }
        catch (IOException) { }
        finally
        {
            lock (clients)
            {
                clients.Remove(client);
                clientNames.Remove(client);
            }
            client.Close();
        }
    }


    static void Broadcast(string message)
    {
        byte[] msg = Encoding.UTF8.GetBytes(message + "\n");
        lock (clients)
        {
            for (int i = clients.Count - 1; i >= 0; i--)
            {
                try
                {
                    clients[i].GetStream().Write(msg, 0, msg.Length);
                }
                catch
                {
                    clients[i].Close();
                    clients.RemoveAt(i);
                }
            }
        }
    }
    static void BroadcastBinary(byte[] data)
    {
        lock (clients)
        {
            for (int i = clients.Count - 1; i >= 0; i--)
            {
                try
                {
                    clients[i].GetStream().Write(data, 0, data.Length);
                }
                catch
                {
                    clients[i].Close();
                    clients.RemoveAt(i);
                }
            }
        }
    }




    static void Send(TcpClient client, string message)
    {
        try
        {
            byte[] msg = Encoding.UTF8.GetBytes(message + "\n");
            client.GetStream().Write(msg, 0, msg.Length);
        }
        catch
        {
            client.Close();
            clients.Remove(client);
        }
    }
}
