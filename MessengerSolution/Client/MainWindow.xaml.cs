using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfChatClient
{
    public partial class MainWindow : Window
    {
        TcpClient client;
        NetworkStream stream;
        string nickname;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            nickname = NicknameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(nickname))
            {
                MessageBox.Show("Введіть ім'я!");
                return;
            }

            try
            {
                client = new TcpClient("127.0.0.1", 5000); // IP сервера
                stream = client.GetStream();
                await SendMessageAsync($"{nickname} приєднався до чату");
                ReceiveMessages();
                UsersListBox.Items.Add(nickname);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не вдалося підключитись: " + ex.Message);
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                await SendMessageAsync($"{nickname}: {message}");
                MessageTextBox.Clear();
            }
        }

        private async Task SendMessageAsync(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Dispatcher.Invoke(() =>
                {
                    ChatTextBox.AppendText(response);
                    ChatTextBox.ScrollToEnd();
                });
            }
        }
    }
}
