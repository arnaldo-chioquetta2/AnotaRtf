using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AnotaRtf
{
    public static class Logger
    {
        private static string logFilePath;

        public static void Initialize()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // 🔑 NOME FIXO - Sem data no nome do arquivo
                logFilePath = Path.Combine(baseDir, "AnotaRtf.log");

                // Cabeçalho (sobrescreve arquivo anterior)
                string header = $"[{DateTime.Now:HH:mm:ss.fff}] ========================================\r\n";
                header += $"[{DateTime.Now:HH:mm:ss.fff}] AnoteitoRtf - Início da Sessão\r\n";
                header += $"[{DateTime.Now:HH:mm:ss.fff}] Versão: {Assembly.GetExecutingAssembly().GetName().Version}\r\n";
                header += $"[{DateTime.Now:HH:mm:ss.fff}] Data/Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\n";
                header += $"[{DateTime.Now:HH:mm:ss.fff}] Arquivo: {logFilePath}\r\n";
                header += $"[{DateTime.Now:HH:mm:ss.fff}] ========================================\r\n";

                File.WriteAllText(logFilePath, header);
                Debug.WriteLine(header);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERRO CRÍTICO] Não foi possível criar log: {ex.Message}");
            }
        }

        public static void Write(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logLine = $"[{timestamp}] {message}";

            // 1. Envia para o Debug do Visual Studio
            Debug.WriteLine(logLine);

            // 2. Grava no arquivo de log
            try
            {
                File.AppendAllText(logFilePath, logLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERRO LOG] Não foi possível gravar no arquivo: {ex.Message}");
            }
        }

        public static void WriteException(Exception ex, string context = "")
        {
            Write($"[EXCEÇÃO] {context}");
            Write($"  Tipo: {ex.GetType().Name}");
            Write($"  Mensagem: {ex.Message}");
            Write($"  Stack: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Write($"  Inner: {ex.InnerException.Message}");
            }
        }

        public static string GetLogFilePath()
        {
            return logFilePath;
        }
    }
}