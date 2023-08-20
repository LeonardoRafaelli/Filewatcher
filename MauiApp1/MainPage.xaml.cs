namespace MauiApp1;

using System;
using System.IO;
using System.Net.Mail;
using System.Net;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Serilog;
using System.Text.RegularExpressions;

public partial class MainPage : ContentPage
{



    private CancellationTokenSource rotateCancelTokenSource;
    private FileSystemWatcher watcher;
    private string fileName, fileFullPath, contentType;
    private bool isWatchFolder;
    private Timer watchingTxtTimer;
    private Timer notificationTimer;
    // Notification
    private Entry mailEntry;
    private string email = "";
    private int valueResetTimer, seconds;

    // Setting options
    private bool isPremium = false;
    private string basepath;
    private string premiumPassword = "root";

    public MainPage()
    {

        try
        {
            InitializeComponent();

            mailEntry = new Entry
            {
                Keyboard = Keyboard.Email,
                Placeholder = "E-mail",
                WidthRequest = 200,
                HeightRequest = 40,
                TextColor = Color.FromRgb(0, 0, 0),
                BackgroundColor = Color.FromRgb(238, 238, 238),
            };

            mailEntry.TextChanged += OnTextEmailChanged;

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    // Adds a new label when the file watcher detects a change
    private void OnChanged(object sender, FileSystemEventArgs e) => ShowOnScreen(e);


    // Other actions for other triggers of the file watcher
    private void OnCreated(object sender, FileSystemEventArgs e) => ShowOnScreen(e);

    private void OnDeleted(object sender, FileSystemEventArgs e) => ShowOnScreen(e);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        ShowOnScreen(e);
    }

    private void ShowOnScreen(FileSystemEventArgs e)
    {
        _ = Dispatcher.Dispatch(async () =>
        {
            string folderFile = isWatchFolder ? (e.Name + " - ") : "";
            DateTime currentDateTime = DateTime.Now;
            Label label = new()
            {
                Text = $"{folderFile}{e.ChangeType} - {currentDateTime}",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Start,
                TextColor = Color.FromRgb(32, 32, 32)
            };

            myLayout.Children.Add(label);

            await monitorScroll.ScrollToAsync(0, Double.MaxValue, animated: true);
        });
        StartNotifyTimer();
        //teste
    }

    private void CleanShowOnScreen(Object sender, EventArgs e)
    {
        if (myLayout.Children.Count > 0)
        {
            myLayout.Clear();
        }
    }


    // ----

    private async void OnShowSettingsClicked(Object sender, EventArgs e)
    {

        //string setBasepath = "Set Basepath";
        string bePremium = "Be Premium";
        string resetPremium = "Reset Premium";
        string changePremiumPass = "Change Premium Password";

        string settingAction = await DisplayActionSheet("Settings", null, "Cancel", bePremium, changePremiumPass, isPremium ? resetPremium : null);

        if (settingAction == bePremium)
        {
            if (!isPremium)
            {
                string passResult = await DisplayPromptAsync("Be Premium", "Enter the password:");

                if (passResult == premiumPassword)
                {
                    loadingIcon.Source = "gold_loading_icon.png";
                    isPremium = true;
                }
                else
                {
                    await DisplayAlert("Wrong Password!", "You may talk to MR. Hollm", "OK");
                }
            }
            else
            {
                await DisplayAlert("Be Premium", "You're already Premium", "OK");
            }
        }

        //if (settingAction == setBasepath)
        //{
        //    string basepathResult = await DisplayPromptAsync("Set Basepath", "Enter the basepath you want the file/folder picker to start from:", "OK");
        //}

        if (settingAction == resetPremium)
        {
            string isSure = await DisplayActionSheet("Are you sure you want\nto reset Premium?", "No", "Yes");

            if (isSure == "Yes")
            {
                loadingIcon.Source = "loading_icon.png";
                isPremium = false;
            }
        }

        if (settingAction == changePremiumPass)
        {
            string currPass = await DisplayPromptAsync("Change Premium Pass", "Enter the current password:", "OK", "Cancel");

            if (currPass == premiumPassword)
            {
                string newPass = await DisplayPromptAsync("Change Premium Pass", "Enter the new password:", "OK", "Cancel");
                premiumPassword = newPass;
            }
            else
            {
                await DisplayAlert("Wrong Password!", "You may talk to MR. Hollm", "OK");
            }
        }
    }

    // Sets the selected file so then its possible to start monitoring
    private async void OnSelectFileClicked(Object sender, EventArgs e)
    {
        if (isWatchFolder)
        {
            var folderResult = await FolderPicker.PickAsync(default);

            if (folderResult.IsSuccessful)
            {
                StopWatcher();
                UpdateFileDetails(folderResult.Folder.Name, folderResult.Folder.Path, "folder");
            }
        }
        else
        {
            FileResult file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select file",
            });


            if (file != null)
            {
                StopWatcher();
                UpdateFileDetails(file.FileName, file.FullPath, file.ContentType);
            }
        }
    }

    private void UpdateFileDetails(string fn, string ffp, string fct)
    {
        fileName = fn;
        fileFullPath = ffp;
        contentType = isWatchFolder ? "folder" : fct;

        fileDetailName.Text = fileName;
        SemanticScreenReader.Announce(fileDetailName.Text);
        fileDetailType.Text = contentType;
        SemanticScreenReader.Announce(fileDetailType.Text);
        fileDetailFolderPath.Text = isWatchFolder ? fileFullPath : Path.GetDirectoryName(fileFullPath);
        SemanticScreenReader.Announce(fileDetailFolderPath.Text);
    }

    private void OnCheckBoxClicked(object sender, CheckedChangedEventArgs e)
    {
        bool isChecked = e.Value;
        isWatchFolder = isChecked;

        if (isChecked)
        {
            openFileExplorerBtn.Text = "Select folder";
            SemanticScreenReader.Announce(openFileExplorerBtn.Text);
        }
        else
        {
            openFileExplorerBtn.Text = "Select file";
            SemanticScreenReader.Announce(openFileExplorerBtn.Text);
        }
    }


    // Start monitoring changes on the selected file
    private void StartStopMonitor(object sender, EventArgs e)
    {
        if (fileFullPath == null || fileName == null)
        {
            return;
        }
        if (watcher != null)
        {
            StopWatcher();
            if (isPremium && watchingTxtTimer != null)
            {
                watchingTxt.Text = "Watching...";
                watchingTxtTimer.Dispose();
            }

        }
        else
        {
            StartWatcher();
            if (isPremium)
            {
                StartWatchingTxtMovement();
            }
        }
    }

    private void StopWatcher()
    {
        StopNotifying();
        // Dispose the file Watcher, before creating a new one
        if (watcher != null)
        {
            watcher.Changed -= OnChanged;
            watcher.Created -= OnCreated;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
            watcher = null;
        }

        StopIconRotation();

        monitorBtn.Text = "Start Watcher";
        monitorBtn.BackgroundColor = Color.FromRgb(10, 140, 10);
        SemanticScreenReader.Announce(monitorBtn.Text);
    }

    private void StartWatcher()
    {
        InitializeWatcher(fileFullPath, fileName, contentType);
        monitorBtn.Text = "Stop Watcher";
        monitorBtn.BackgroundColor = Color.FromRgb(140, 10, 10);
        SemanticScreenReader.Announce(monitorBtn.Text);
    }

    // Initialize watcher
    private void InitializeWatcher(string fullPath, string fileName, string fileContentType)
    {
        //Log.Information($"FP = {fullPath}\n FN = {fileName}\n CT = {fileContentType}");

        StartIconRotation();

        // Create a new File Watcher
        watcher = new FileSystemWatcher($@"{Path.GetDirectoryName(fullPath)}");

        watcher.NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size;

        watcher.Changed += OnChanged;
        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;

        var filter = isWatchFolder ? "*.*" : fileName;
        watcher.Filter = filter;
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;

        // Start notification
        StartNotifyTimer();

    }

    private void StartWatchingTxtMovement()
    {

        watchingTxtTimer = new Timer((Object state) =>
        {
            Dispatcher.Dispatch(() =>
            {
                string label1 = "Watching.", label2 = "Watching..", label3 = "Watching...";
                if (watchingTxt.Text == label1)
                {
                    watchingTxt.Text = label2;
                }
                else if (watchingTxt.Text == label2)
                {
                    watchingTxt.Text = label3;
                }
                else
                {
                    watchingTxt.Text = label1;
                }
            });

        }, null, 0, 1000);

    }

    public static bool IsValidEmail(string email)
    {
        // Regex pattern for basic email validation
        string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        // Check if the email matches the pattern
        return Regex.IsMatch(email, pattern);
    }

    // Notification
    private void StartNotifyTimer()
    {
        if (notificationTimer != null)
        {
            notificationTimer.Dispose();
            notificationTimer = null;
        }
        if (notifyMeCheckBox.IsChecked && seconds > 0)
        {
            notificationTimer = new Timer(SendNotification, null, seconds, Timeout.Infinite);
        }
    }

    private void SendNotification(object state)
    {

        MainThread.InvokeOnMainThreadAsync(async () =>
        {

            string StopAll = "Stop All";
            string StopNotification = "Stop Notification";

            if (notifyAtCheckBox.IsChecked)
            {
                if (IsValidEmail(email))
                {
                    Task.Run(() => SendEmailNotification());
                }
            }
            string result = await DisplayActionSheet("Notify me", null, "Continue", StopAll, StopNotification);

            if (result == StopAll)
            {
                StopWatcher();
            }
            else if (result == StopNotification)
            {
                StopNotifying();
            }
            else
            {
                StartNotifyTimer();
            }
        });
    }

    private void StopNotifying()
    {
        if (notifyMeCheckBox.IsChecked)
        {
            notifyMeCheckBox.IsChecked = false;
        }
        if (notificationTimer != null)
        {
            notificationTimer.Dispose();
            notificationTimer = null;
        }
    }

    private void OnCheckBoxNotifyMeClicked(Object sender, CheckedChangedEventArgs e)
    {
        notifyMeCheckBox.IsChecked = e.Value;
    }
    private void OnCheckBoxAtClicked(Object sender, CheckedChangedEventArgs e)
    {
        notifyAtCheckBox.IsChecked = e.Value;
        if (e.Value)
        {

            notificationLayout.Children.Add(mailEntry);
        }
        else
        {
            notificationLayout.Children.Remove(mailEntry);
        }
    }

    private void OnTextEmailChanged(Object sender, TextChangedEventArgs e)
    {
        if (!IsValidEmail(e.NewTextValue))
        {
            mailEntry.TextColor = Color.FromRgb(220, 10, 10);
        }
        else
        {
            mailEntry.TextColor = Color.FromRgb(0, 0, 0);
        }

        // Text from email entry
        email = e.NewTextValue;
    }

    private void OnTextSecondChanged(Object sender, TextChangedEventArgs e)
    {
        if (int.TryParse(e.NewTextValue, out int result))
        {
            seconds = result * 1000;
            valueResetTimer = seconds;
        }
        else
        {
            ((Entry)sender).Text = null;
        }

    }

    private void SendEmailNotification()
    {
        string smtpHost = "smtp.gmail.com";
        int smtpPort = 587;
        string emailRemetente = "watcherfile@gmail.com"; // Substitua pelo seu e-mail do Gmail
        string senhaRemetente = "pufmraajecqvscgf"; // Substitua pela sua senha do Gmail
        string emailDestinatario = email; // Substitua pelo e-mail do destinatário

        string assunto = "Watcher notification";
        string filefolder = isWatchFolder ? "folder" : "file";
        string corpoMensagem = $"The {filefolder} you're watching has not received a changed within {seconds / 1000} seconds" +
            $"\n\n\nIf you did not request a notification from the filewatcher.exe, please ignore this email.";

        try
        {
            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                // Autenticação no servidor SMTP
                client.Credentials = new NetworkCredential(emailRemetente, senhaRemetente);
                client.EnableSsl = true;

                // Criar a mensagem do e-mail
                var mensagem = new MailMessage(emailRemetente, emailDestinatario, assunto, corpoMensagem);

                // Enviar o e-mail
                client.Send(mensagem);
            }
        }
        catch (Exception ex)
        {
        }

    }


    // Control the monitoring icon
    private async void StartIconRotation()
    {
        StopIconRotation();
        rotateCancelTokenSource = new CancellationTokenSource();

        await RotateImage(loadingIcon, 360, 2000, rotateCancelTokenSource.Token);
    }

    private void StopIconRotation()
    {
        rotateCancelTokenSource?.Cancel();
    }

    private static async Task RotateImage(Image img, double deg, uint ms, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await img.RotateTo(deg, ms);
            img.Rotation = 0;
        }
    }

}

