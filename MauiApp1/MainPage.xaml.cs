namespace MauiApp1;

using System;
using System.IO;
using System.Net.Mail;
using System.Net;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Serilog;


public partial class MainPage : ContentPage
{



    private CancellationTokenSource rotateCancelTokenSource;
    private FileSystemWatcher watcher;
    private string fileName, fileFullPath, contentType;
    private bool isWatchFolder;
    public string InputText { get; set; } = "";
    public MainPage()
    {

        try
        {
            InitializeComponent();
            InputText = "";

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
                HorizontalOptions = LayoutOptions.Start
            };

            myLayout.Children.Add(label);

            await monitorScroll.ScrollToAsync(0, Double.MaxValue, animated: true);
        });
    }
    // ----

    // Sets the selected file so then its possible to start monitoring
    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        if (isWatchFolder)
        {
            var folderResult = await FolderPicker.PickAsync(default);

            if(folderResult.IsSuccessful)
            {
                StopWatcher();
                updateFileDetails(folderResult.Folder.Name, folderResult.Folder.Path, "folder");
            }
        }
        else
        {
            FileResult file = await FilePicker.PickAsync();

            if (file != null)
            {
                StopWatcher();
                updateFileDetails(file.FileName, file.FullPath, file.ContentType);
            }
        }
    }

    private void updateFileDetails(string fn, string ffp, string fct)
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

    private void OnShowSettingsClicked(object sender, EventArgs e)
    {

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

    private void OnNotifyClicked(Object sender, EventArgs e)
    {
        string smtpHost = "smtp.gmail.com";
        int smtpPort = 587;
        string emailRemetente = "diego_planinscheck@estudante.sc.senai.br"; // Substitua pelo seu e-mail do Gmail
        string senhaRemetente = "phygbnrtzoinkdzw"; // Substitua pela sua senha do Gmail
        string emailDestinatario = "leonardorafaelli@gmail.com"; // Substitua pelo e-mail do destinatário
        string assunto = "Assunto do e-mail";
        string corpoMensagem = "Corpo do pega no meu pau e-mail.";

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

                Log.Information("E-mail enviado com sucesso");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar o e-mail: " + ex.Message);
            Log.Information($"{ex.Message}");
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

        }
        else
        {
            StartWatcher();
        }
    }

    private void StopWatcher()
    {
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

    private async Task RotateImage(Image img, double deg, uint ms, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await img.RotateTo(deg, ms);
            img.Rotation = 0;
        }
    }

    // Initialize logs on a external file
    private static void InitializeLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(@"C:\Users\leona.RAFAELLINOTE\Desktop\logs.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Logging initialized.");
    }

}

