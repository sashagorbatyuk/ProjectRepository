using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private string nickname = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NicknameBox.Text))
            {
                MessageBox.Show("Введіть нікнейм.");
                return;
            }

            nickname = NicknameBox.Text;

            try
            {
                client = new TcpClient("127.0.0.1", 5000);
                stream = client.GetStream();

                // Повідомлення серверу про нове підключення
                SendRaw($"[JOIN]{nickname}");

                new Thread(ReceiveMessages).Start();
                ChatBox.AppendText("Підключено до сервера.\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка з’єднання: {ex.Message}");
            }
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageBox.Text)) return;

            string msg = $"{nickname}: {MessageBox.Text}";
            SendRaw(msg);
            MessageBox.Clear();
        }

        private void SendRaw(string msg)
        {
            if (stream == null) return;
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            int byteCount;

            try
            {
                while ((byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, byteCount);

                    // Якщо це оновлення списку онлайн
                    if (message.StartsWith("[USERS]"))
                    {
                        string[] users = message.Substring(7).Split(';');
                        Dispatcher.Invoke(() =>
                        {
                            UserListBox.Items.Clear();
                            foreach (var user in users)
                                if (!string.IsNullOrWhiteSpace(user))
                                    UserListBox.Items.Add(user);
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => ChatBox.AppendText(message + "\n"));
                    }
                }
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() => ChatBox.AppendText("Втрачено з’єднання з сервером.\n"));
            }
        }
    }
}
