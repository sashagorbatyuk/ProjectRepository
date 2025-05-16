using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace MessengerClient
{
    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private string username;
        private Thread receiveThread;
        private readonly string historyFile = "chat_history.txt";

        public MainWindow()
        {
            InitializeComponent();

            // Спочатково вимикаємо кнопки
            SendText.IsEnabled = false;
            SendImage.IsEnabled = false;
            SendAudio.IsEnabled = false;
            DisconnectButton.IsEnabled = false;

            // Завантажуємо історію
            if (File.Exists(historyFile))
            {
                var history = File.ReadAllText(historyFile);
                ChatBox.AppendText(history);
            }
        }


        private void Connect_Click(object sender, RoutedEventArgs e)
        {

            username = UsernameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                //MessageBox.Show("Введіть ім'я користувача");
                return;
            }

            try
            {
                client = new TcpClient("127.0.0.1", 8888);
                stream = client.GetStream();

                Send($"CONNECT|{username}");

                receiveThread = new Thread(ReceiveData) { IsBackground = true };
                receiveThread.Start();

                ChatBox.AppendText("[SYSTEM]: Connected\n");

                // Увімкнути кнопки
                SendText.IsEnabled = true;
                SendImage.IsEnabled = true;
                SendAudio.IsEnabled = true;
                DisconnectButton.IsEnabled = true;
                Connect.IsEnabled = false;
                UsernameBox.IsEnabled = true;
                SendVideo.IsEnabled = true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show("Помилка підключення: " + ex.Message);
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Send($"EXIT|{username}|bye");
                stream?.Close();
                client?.Close();
                ChatBox.AppendText("[SYSTEM]: Disconnected\n");

                // Вимкнути кнопки
                SendText.IsEnabled = false;
                SendImage.IsEnabled = false;
                SendAudio.IsEnabled = false;
                DisconnectButton.IsEnabled = false;
                Connect.IsEnabled = true;
                UsernameBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                //MessageBox.Show("Помилка при відключенні: " + ex.Message);
            }
        }
        private void SendVideo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video Files (*.mp4;*.avi;*.mov)|*.mp4;*.avi;*.mov"
            };

            if (dlg.ShowDialog() == true)
            {
                byte[] videoBytes = File.ReadAllBytes(dlg.FileName);
                Send($"VIDEO|{username}|{videoBytes.Length}");
                stream.Write(videoBytes, 0, videoBytes.Length);
                stream.Flush();

                AppendTextToChat($"{username} sent a video file.");
            }
        }


        private void SendText_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;
            Send($"TEXT|{username}|{message}");
            MessageBox.Clear();
        }

        private void SendImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (dlg.ShowDialog() == true)
            {
                byte[] imageBytes = File.ReadAllBytes(dlg.FileName);
                Send($"IMAGE|{username}|{imageBytes.Length}");
                stream.Write(imageBytes, 0, imageBytes.Length);
                stream.Flush();

                AppendImageToChat(imageBytes);
            }
        }

        private void SendAudio_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3"
            };

            if (dlg.ShowDialog() == true)
            {
                byte[] audioBytes = File.ReadAllBytes(dlg.FileName);
                Send($"AUDIO|{username}|{audioBytes.Length}"); // \n обов'язково
                stream.Write(audioBytes, 0, audioBytes.Length);
                stream.Flush();

                AppendTextToChat($"{username} sent an audio file (not playable yet).");
            }
        }

        private void Send(string message)
        {
            if (stream == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
        }


        private void ReceiveData()
        {
            var reader = new StreamReader(stream);
            try
            {
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;

                    // Обробка повідомлень за типом
                    string[] parts = line.Split('|');
                    string cmd = parts[0];

                    if (cmd == "COMMAND")
                    {
                        if (parts.Length < 3) return;
                        string from = parts[1];
                        string commandPayload = parts[2];

                        if (commandPayload.StartsWith("USER_LIST:"))
                        {
                            string users = commandPayload.Substring("USER_LIST:".Length);
                            Dispatcher.Invoke(() => UpdateUserList(users));
                        }
                        else if (commandPayload.StartsWith("USER_JOINED:"))
                        {
                            string user = commandPayload.Substring("USER_JOINED:".Length);
                            Dispatcher.Invoke(() =>
                            {
                                if (!UserList.Items.Contains(user))
                                    UserList.Items.Add(user);
                            });
                        }
                        else if (commandPayload.StartsWith("USER_LEFT:"))
                        {
                            string user = commandPayload.Substring("USER_LEFT:".Length);
                            Dispatcher.Invoke(() => UserList.Items.Remove(user));
                        }

                        return;
                    }


                    switch (cmd)
                    {
                        case "TEXT":
                            {
                                string from = parts[1];
                                string msg = parts[2];
                                Dispatcher.Invoke(() => AppendTextToChat($"{from}: {msg}"));
                                break;
                            }
                        case "IMAGE":
                            {
                                string from = parts[1];
                                int length = int.Parse(parts[2]);
                                byte[] imageBytes = new byte[length];
                                int received = 0;
                                while (received < length)
                                {
                                    int r = stream.Read(imageBytes, received, length - received);
                                    if (r == 0) break;
                                    received += r;
                                }
                                Dispatcher.Invoke(() =>
                                {
                                    AppendTextToChat($"{from} sent an image:");
                                    AppendImageToChat(imageBytes);
                                });
                                break;
                            }
                        case "USERLIST":
                            {
                                string users = parts[1];
                                Dispatcher.Invoke(() => UpdateUserList(users));
                                break;
                            }
                        case "AUDIO":
                            {
                                string from = parts[1];
                                int length = int.Parse(parts[2]);
                                byte[] audioBytes = new byte[length];
                                int received = 0;
                                while (received < length)
                                {
                                    int r = stream.Read(audioBytes, received, length - received);
                                    if (r == 0) break;
                                    received += r;
                                }

                                string filePath = $"audio_{DateTime.Now.Ticks}.mp3";
                                File.WriteAllBytes(filePath, audioBytes);

                                Dispatcher.Invoke(() =>
                                {
                                    AppendAudioToChat(from, filePath);
                                });
                                break;
                            }
                        case "VIDEO":
                            {
                                string from = parts[1];
                                int length = int.Parse(parts[2]);
                                byte[] videoBytes = new byte[length];
                                int received = 0;
                                while (received < length)
                                {
                                    int r = stream.Read(videoBytes, received, length - received);
                                    if (r == 0) break;
                                    received += r;
                                }

                                string filePath = $"video_{DateTime.Now.Ticks}.mp4";
                                File.WriteAllBytes(filePath, videoBytes);

                                Dispatcher.Invoke(() =>
                                {
                                    AppendTextToChat($"{from} sent a video:");
                                    AppendVideoToChat(filePath); // <-- ЦЕ головне
                                });
                                break;
                            }
                        case "COMMAND":
                            {
                                string from = parts[1];
                                string commandData = parts[2];

                                if (commandData.StartsWith("USER_LIST:"))
                                {
                                    string users = commandData.Substring("USER_LIST:".Length);
                                    Dispatcher.Invoke(() => UpdateUserList(users));
                                }
                                else if (commandData.StartsWith("USER_JOINED:"))
                                {
                                    string user = commandData.Substring("USER_JOINED:".Length);
                                    Dispatcher.Invoke(() =>
                                    {
                                        AppendTextToChat($"[SYSTEM]: {user} joined.");
                                    });
                                }
                                else if (commandData.StartsWith("USER_LEFT:"))
                                {
                                    string user = commandData.Substring("USER_LEFT:".Length);
                                    Dispatcher.Invoke(() =>
                                    {
                                        AppendTextToChat($"[SYSTEM]: {user} left.");
                                    });
                                }

                                break;
                            }



                        case "EXIT":
                            {
                                string from = parts[1];
                                Dispatcher.Invoke(() => AppendTextToChat($"[SYSTEM]: {from} disconnected."));
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                //Dispatcher.Invoke(() => MessageBox.Show("Connection lost: " + ex.Message));
            }
        }

        private void AppendVideoToChat(string filePath)
        {
            var media = new MediaElement
            {
                Source = new Uri(Path.GetFullPath(filePath)),
                Width = 300,
                Height = 200,
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Volume = 0.5
            };

            var playBtn = new Button { Content = "▶", Width = 30 };
            var pauseBtn = new Button { Content = "⏸", Width = 30 };
            var stopBtn = new Button { Content = "⏹", Width = 30 };

            playBtn.Click += (s, e) => media.Play();
            pauseBtn.Click += (s, e) => media.Pause();
            stopBtn.Click += (s, e) => media.Stop();

            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 5, 0, 5) };
            var controlPanel = new StackPanel { Orientation = Orientation.Horizontal };
            controlPanel.Children.Add(playBtn);
            controlPanel.Children.Add(pauseBtn);
            controlPanel.Children.Add(stopBtn);

            panel.Children.Add(controlPanel);
            panel.Children.Add(media);

            var container = new BlockUIContainer(panel);
            ChatBox.Document.Blocks.Add(container);
            ChatBox.ScrollToEnd();

            File.AppendAllText(historyFile, $"[VIDEO:{filePath}]\n");
        }


        private void AppendAudioToChat(string from, string filePath)
        {
            AppendTextToChat($"{from} sent an audio file:");

            var media = new MediaElement
            {
                Source = new Uri(Path.GetFullPath(filePath)),
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Width = 300,
                Height = 30
            };

            var playButton = new Button { Content = "▶", Width = 30 };
            var pauseButton = new Button { Content = "⏸", Width = 30 };
            var stopButton = new Button { Content = "⏹", Width = 30 };

            playButton.Click += (s, e) => media.Play();
            pauseButton.Click += (s, e) => media.Pause();
            stopButton.Click += (s, e) => media.Stop();

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(playButton);
            panel.Children.Add(pauseButton);
            panel.Children.Add(stopButton);
            panel.Children.Add(media);

            var container = new BlockUIContainer(panel);
            ChatBox.Document.Blocks.Add(container);
            ChatBox.ScrollToEnd();

            File.AppendAllText(historyFile, $"[AUDIO:{filePath}]\n");
        }


        private void AppendTextToChat(string text)
        {
            ChatBox.AppendText(text + "\n");
            ChatBox.ScrollToEnd();

            // Зберігаємо історію
            File.AppendAllText(historyFile, text + "\n");
        }

        private void AppendImageToChat(byte[] imageBytes)
        {
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageBytes))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();

            var imgControl = new System.Windows.Controls.Image
            {
                Source = image,
                Width = 200,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var container = new BlockUIContainer(imgControl);

            ChatBox.Document.Blocks.Add(container);
            ChatBox.ScrollToEnd();

            // Зберігаємо текстове позначення у історію
            File.AppendAllText(historyFile, "[IMAGE]\n");
        }

        private void UpdateUserList(string csvUsernames)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UserList.Items.Clear();
                var users = csvUsernames.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var user in users)
                {
                    UserList.Items.Add(user);
                }
            });
        }


        private void UsernameBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UsernamePlaceholder.Visibility = Visibility.Collapsed;
        }

        private void UsernameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
                UsernamePlaceholder.Visibility = Visibility.Visible;
        }

        private void Window_Closed(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                Send($"EXIT|{username}|bye");
                stream?.Close();
                client?.Close();
            }
            catch { }
        }

    }
}
