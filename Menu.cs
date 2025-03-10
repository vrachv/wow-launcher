﻿using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

namespace wow_launcher_cs
{
    public partial class Menu : Form
    {
        private Settings settings;

        public Menu()
        {
            InitializeComponent();
        }

        private bool mouseDown;
        private Point lastLocation;
        private bool DLConfigUA;
        private bool DLConfigWoW;
        private bool ClenupPatchD;
        private string locale;

        private void titleBar_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            lastLocation = e.Location;
        }

        private void titleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                this.Location = new Point(
                    (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);

                this.Update();
            }
        }

        private void titleBar_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }

        private void Menu_Load(object sender, EventArgs e)
        {
            locale = GetClientLocaleConfig();

            if (locale == "NULL")
            {
                if (Directory.Exists("Data/ruRU"))
                    locale = "ruRU";
                else if (Directory.Exists("Data/enGB"))
                    locale = "enGB";
                else if (Directory.Exists("Data/enUS"))
                    locale = "enUS";
            }

            if (!Directory.Exists("Data") || locale=="NULL")
            {
                var result = MessageBox.Show("Не знайдено папки Data\\ \rПеремістіть Launcher в корінь папки World of Warcraft 3.3.5", "Помилка", MessageBoxButtons.OK);
                if (result == DialogResult.OK)
                {
                    Application.Exit();
                }
            }

            //UpdatePlayButton(playButton);
            UpdateDownloadInfoLabel("");
            Text = "Launcher";
            if (File.Exists("Launcher.exe.old"))
                File.Delete("Launcher.exe.old");

            // Очистка Кешу
            if (Directory.Exists("Cache"))
                Directory.Delete("Cache", true);
        }

        public void UpdatePatches()
        {
            UpdateDownloadInfoLabel("Перевірка оновлень.");
            DLConfigUA = GetLauncherConfig("DownloadUALocale");
            ClenupPatchD = GetLauncherConfig("Patch-D-Cleanup");
            locale = GetClientLocaleConfig();

            /* тиха очистка від сміття 0-дня. TODO: не забути вимкнути коли треба буде використати файл з назвою */
            if (File.Exists("Data/ruRU/patch-ruRU-D.MPQ") && ClenupPatchD)
            {
                File.Delete("Data/ruRU/patch-ruRU-D.MPQ");
            }

            if (!DLConfigUA || locale != "ruRU")
            {
                string patchname = "patch-ruRU-4.MPQ";
                string infotxt = "";

                if (locale != "ruRU")
                    infotxt = "Не вибрано ruRU клієнт. ";
                else
                    infotxt = "";

                if (File.Exists("Data/ruRU/" + patchname))
                {
                    File.Delete("Data/ruRU/" + patchname);
                    infotxt += "UA переклад видалено!";
                }

                UpdateDownloadInfoLabel($"Оновлення вимкнено. {infotxt}");
                return;
            }

            SetPlayButtonState(false);

            Thread thread = new Thread(() =>
            {
                if (Updater.data.disabled)
                {
                    UpdateDownloadInfoLabel("Оновлення скасовано. Немає з'єднання?.");
                    SetPlayButtonState(true);
                    return;
                }

                foreach (Updater.PatchData patch in Updater.data.Patches)
                {
                    bool dlCpt = false;

                    if (File.Exists("Data/ruRU/" + patch.name) && Updater.CalculateMD5("Data/ruRU/" + patch.name).CompareTo(patch.md5) == 0)
                    {
                        UpdateDownloadInfoLabel("Оновлення відсутні.");
                        continue;
                    }

                    if (File.Exists("Data/ruRU/" + patch.name))
                        File.Delete("Data/ruRU/" + patch.name);

                    using (WebClient wc = new WebClient())
                    {
                        UpdateDownloadInfoLabel("Завантаження: " + patch.name);

                        wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(UpdateProgress);
                        wc.DownloadFileCompleted += ((sender, args) =>
                        {
                            dlCpt = true; 
                        });
                        wc.DownloadFileAsync(new System.Uri(patch.link), "Data/ruRU/" + patch.name);
                    }
                    while (!dlCpt)
                    {
                        Application.DoEvents();
                    }
                }
                UpdateDownloadInfoLabel("Завантажено останнє оновлення.");
                SetPlayButtonState(true);
            });
            thread.Start();  
        }

        public void UpdateWowExecutable()
        {
            DLConfigWoW = GetLauncherConfig("PatchClient");

            if (Updater.data.disabled || !DLConfigWoW)
                return;

            SetPlayButtonState(false);

            if (File.Exists("Wow.exe"))
            {
                if (Updater.CalculateMD5("Wow.exe").CompareTo(Updater.data.Wow.md5) == 0)
                {
                    UpdateDownloadInfoLabel("Оновлення Wow.exe не потрібне.");
                    SetPlayButtonState(true);
                    //PlayWow();
                    return;
                }

                if (File.Exists("Wow.exe.old"))
                    File.Delete("Wow.exe.old");
                File.Move("Wow.exe", "Wow.exe.old");
            }

            using (WebClient wc = new WebClient())
            {
                wc.DownloadFileCompleted += ((sender, args) =>
                {
                    UpdateDownloadInfoLabel("Wow.exe оновлено.");
                    SetPlayButtonState(true);
                });
                wc.DownloadFileAsync(new System.Uri(Updater.data.Wow.link), "Wow.exe"); //Качає WoW.exe коли натиснуто кнопку Play
            }
        }

        static private void PlayWow()
        {
            Process.Start("Wow.exe");
            Environment.Exit(0); // закриваєм Launcher
        }

        public void UpdateProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            SetProgressBarPct(Convert.ToInt32(percentage));
        }

        public void SetProgressBarPct(int pct)
        {
            Bitmap bmp = new Bitmap(Properties.Resources.dl_bar_green);
            for (int Xcount = (pct * bmp.Width) / 100; Xcount < bmp.Width; Xcount++)
            {
                for (int Ycount = 0; Ycount < bmp.Height; Ycount++)
                {
                    bmp.SetPixel(Xcount, Ycount, Color.Transparent);
                }
            }
            progressBar.BackgroundImage = bmp;
        }

        public async void SendUserSurvey()
        {
            // Збираєм статистику по кількості активних користувачів
            try
            {
                // Визначення MAC-адреси активного мережевого адаптера
                string macAddress = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(macAddress))
                {
                    macAddress ="00:00:00:00:00:00";
                }

                // Обчислення MD5 з MAC-адреси
                string Hash = BitConverter.ToString(
                    MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(macAddress)))
                    .Replace("-", "");

                // Відправка запиту через WebClient
                using (WebClient wc = new WebClient())
                {
                    string url = $"http://updater.freedom-wow.in.ua/client/survey.php?count_user={Hash}";
                    string response = await wc.DownloadStringTaskAsync(url);
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося надіслати запит: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void playButton_MouseDown(object sender, MouseEventArgs e)
        {
            playButton.BackgroundImage = Properties.Resources.PlayButtonDown;
        }

        private void playButton_MouseUp(object sender, MouseEventArgs e)
        {
            playButton.BackgroundImage = Properties.Resources.PlayButtonBase;
        }

        private void playButton_MouseEnter(object sender, EventArgs e)
        {
            playButton.BackgroundImage = Properties.Resources.PlayButtonHover;
        }

        private void playButton_MouseLeave(object sender, EventArgs e)
        {
            playButton.BackgroundImage = Properties.Resources.PlayButtonBase;
        }

        private void minimizeButton_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void minimizeButton_MouseEnter(object sender, EventArgs e)
        {
            minimizeButton.BackgroundImage = Properties.Resources.MinimizeButtonHover;
        }

        private void minimizeButton_MouseDown(object sender, MouseEventArgs e)
        {
            minimizeButton.BackgroundImage = Properties.Resources.MinimizeButtonDown;
        }

        private void minimizeButton_MouseUp(object sender, MouseEventArgs e)
        {
            minimizeButton.BackgroundImage = Properties.Resources.MinimizeButtonBase;
        }

        private void minimizeButton_MouseLeave(object sender, EventArgs e)
        {
            minimizeButton.BackgroundImage = Properties.Resources.MinimizeButtonBase;
        }

        private void closeButton_MouseDown(object sender, MouseEventArgs e)
        {
            closeButton.BackgroundImage = Properties.Resources.CloseButtonDown;
        }

        private void closeButton_MouseEnter(object sender, EventArgs e)
        {
            closeButton.BackgroundImage = Properties.Resources.CloseButtonHover;
        }

        private void closeButton_MouseLeave(object sender, EventArgs e)
        {
            closeButton.BackgroundImage = Properties.Resources.CloseButtonBase;
        }

        private void closeButton_MouseUp(object sender, MouseEventArgs e)
        {
            closeButton.BackgroundImage = Properties.Resources.CloseButtonBase;
        }

        private void Menu_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void playButton_EnabledChanged(object sender, EventArgs e)
        {
            UpdatePlayButton(sender);
        }

        private void UpdatePlayButton(object b)
        {
            if (playButton.Enabled)
                playButton.BackgroundImage = Properties.Resources.PlayButtonBase;
            else
                playButton.BackgroundImage = Properties.Resources.PlayButtonDisabled;
        }

        public void CheckRealmlistAndUpdate()
        {
            locale = GetClientLocaleConfig();

            string wtfpath = $@"Data/{locale}/realmlist.wtf";

            if (File.Exists(wtfpath))
            {
                FileAttributes attributes = File.GetAttributes(wtfpath);

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(wtfpath, FileAttributes.Normal);

                if (File.ReadAllText(wtfpath).CompareTo("set realmlist login1.freedom-wow.in.ua") != 0)
                {
                    if (settings.CheckboxRealmlist.Checked)
                    {
                        settings.ChangeRealmlist(wtfpath);

                    }
                }
            }
            else
            {
                using (StreamWriter sw = new StreamWriter(wtfpath, true, Encoding.UTF8))
                {
                    sw.Write("set realmlist login1.freedom-wow.in.ua");
                    playButton.Enabled = true;
                }
            }
        }

        private void Menu_Shown(object sender, EventArgs e)
        {
            SendUserSurvey();

            // Перевірка, чи запущений процес Wow.exe
            if (Process.GetProcessesByName("Wow").Any())
            {
                UpdateDownloadInfoLabel("Гра запущена. Перевірка оновлення скасована.");
                MessageBox.Show("Гра запущена! Закрийте WoW перед оновленням патчів.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            else
            {
                UpdatePatches();
                UpdateWowExecutable();
            }
        }

        private void playButton_Click(object sender, EventArgs e)
        {
            PlayWow();
        }

        private void SettingsButton_Click_1(object sender, EventArgs e)
        {
            if (settings == null || settings.IsDisposed)
            {
                settings = new Settings(this);
                settings.Show();
            }
            else
            {
                settings.BringToFront();
            }
        }

        private void SettingsButton_MouseDown(object sender, MouseEventArgs e)
        {
            SettingsButton.BackgroundImage = Properties.Resources.config_button_down;
        }
        private void SettingsButton_MouseEnter(object sender, EventArgs e)
        {
            SettingsButton.BackgroundImage = Properties.Resources.config_button_hover;
        }

        private void SettingsButton_MouseLeave(object sender, EventArgs e)
        {
            SettingsButton.BackgroundImage = Properties.Resources.config_button_base;
        }

        private void SettingsButton_MouseUp(object sender, MouseEventArgs e)
        {
            SettingsButton.BackgroundImage = Properties.Resources.config_button_base;
        }

        public bool GetLauncherConfig(string config)
        {
            // Основна папка
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string LauncherConfigFilePath = Path.Combine(baseDirectory, "Launcher.ini");

            bool state = false;

            // Перевірка, чи існує файл Launcher.ini
            if (!File.Exists(LauncherConfigFilePath))
            {
                // Створюємо файл із дефолтними налаштуваннями
                var settings = new wow_launcher_cs.Settings(this);
                settings.SetDefaultLauncherConfig();
            }

            try
            {
                // Читання файлу Launcher.ini
                string[] configLines = File.ReadAllLines(LauncherConfigFilePath);
                // Пошук параметра
                string configLine = configLines.FirstOrDefault(line => line.StartsWith($"{config} "));

                if (!string.IsNullOrEmpty(configLine))
                {
                    // Отримання значення параметра
                    string status = configLine.Split('"')[1];

                    // Встановлення значення чекбокса
                    if (status == "1")
                        state = true;
                    else
                        state = false;
                }
                else
                {
                    // Якщо параметра не існує, створюємо його і ставимо значення "1"
                    var settings = new wow_launcher_cs.Settings(this);
                    settings.UpdateLauncherConfig(config, true);
                    state = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка читання Launcher.ini\nПомилка: {ex.Message}",
                                "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return state;
        }

        public string GetClientLocaleConfig()
        {
            // Основна папка
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");
            string wtfDirectory = Path.Combine(baseDirectory, "WTF");
            string configFilePath = Path.Combine(wtfDirectory, "Config.wtf");
            string clientLocaleConfig = "NULL";

            if (File.Exists(configFilePath))
            {
                string[] configLines = File.ReadAllLines(configFilePath);

                string localeLine = configLines.FirstOrDefault(line => line.StartsWith("SET locale "));

                if (!string.IsNullOrEmpty(localeLine))
                {
                    //Console.WriteLine($"Локаль знайдено: {localeLine}");
                    clientLocaleConfig = localeLine.Split('"')[1];
                }
            }
            
            if (clientLocaleConfig == "NULL")
            {
                if (Directory.Exists(Path.Combine(dataDirectory, "ruRU")))
                    clientLocaleConfig = "ruRU";
                else if (Directory.Exists(Path.Combine(dataDirectory, "enGB")))
                    clientLocaleConfig = "enGB";
                else if (Directory.Exists(Path.Combine(dataDirectory, "enUS")))
                    clientLocaleConfig = "enUS";
                else
                    MessageBox.Show("Жодної папки з локалізацією не знайдено!\nПомістіть Лаунчер в корінь папки з грою World of Warcraft 3.3.5", "Помилка", MessageBoxButtons.OK);
            }

            return clientLocaleConfig;
        }

        public void UpdateDownloadInfoLabel(string text)
        {
            if (DownloadInfoLabel.InvokeRequired)
            {
                DownloadInfoLabel.Invoke(new MethodInvoker(() => DownloadInfoLabel.Text = text));
            }
            else
            {
                DownloadInfoLabel.Text = text;
            }
        }
        private void SetPlayButtonState(bool enabled)
        {
            if (playButton.InvokeRequired)
            {
                playButton.Invoke(new MethodInvoker(() => playButton.Enabled = enabled));
            }
            else
            {
                playButton.Enabled = enabled;
            }
            UpdatePlayButton(playButton);
        }

        private void OnLinkAreaClicked(object sender, MouseEventArgs e)
        {
            // Відкриття сайту у браузері за замовчуванням
            string url = "https://discord.gg/pGmU9YTwY8";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true // Це потрібно для відкриття URL у браузері
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити сайт: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void discordLinkLabel_MouseEnter(object sender, EventArgs e)
        {
            discordLinkLabel.Cursor = Cursors.Hand;
        }

        private void discordLinkLabel_MouseLeave(object sender, EventArgs e)
        {
            discordLinkLabel.Cursor = Cursors.Default;
        }
    }
}
