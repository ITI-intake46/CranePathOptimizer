using System.Globalization;
using System.IO;
using System.Text;

namespace CraneOptimizer.Domain.Diagnostics
{
    public static class IfcImportProfiler
    {
        private const string CsvHeader = "TimestampUtc,ModelRole,IfcPath,Stage,SubStage,Status,StartedUtc,CompletedUtc,ElapsedMs,CountName,CountValue,CurrentIndex,TotalCount,ProductGlobalId,EntityLabel,ExpressType,IsCompleted,ExceptionType,ExceptionMessage,ThreadId";
        private static readonly object SyncRoot = new();
        private static StreamWriter? _writer;

        public static string CurrentModelRole { get; private set; } = "Manual";
        public static string CurrentIfcPath { get; private set; } = string.Empty;
        public static string? CurrentLogFilePath { get; private set; }

        public static string Start(string ifcPath, string? currentProjectFolder)
        {
            if (string.IsNullOrWhiteSpace(currentProjectFolder))
            {
                throw new InvalidOperationException(
                    "The active project folder could not be resolved. No IFC performance log was created. " +
                    "Canonical fallback, if explicitly approved, would be the application base directory logs folder.");
            }

            if (!DebugOutputGate.EnableProjectPerformanceCapture)
            {
                _writer?.Dispose();
                _writer = null;
                CurrentLogFilePath = null;
                return string.Empty;
            }

            string projectFolder = Path.GetFullPath(currentProjectFolder);
            string logsFolder = Path.Combine(projectFolder, "logs");
            Directory.CreateDirectory(logsFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string logPath = Path.Combine(logsFolder, $"log_ifc_import_stage_perf_{timestamp}.csv");

            lock (SyncRoot)
            {
                _writer?.Dispose();
                _writer = new StreamWriter(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
                {
                    AutoFlush = true
                };
                _writer?.WriteLine(CsvHeader);
                _writer?.Flush();

                CurrentIfcPath = ifcPath;
                CurrentModelRole = GetModelRole(ifcPath);
                CurrentLogFilePath = logPath;
            }

            return logPath;
        }

        public static void Record(
            string stage,
            string subStage,
            string status,
            DateTime startedUtc,
            DateTime completedUtc,
            long elapsedMs,
            string countName,
            long countValue,
            bool isCompleted,
            string? exceptionType = null,
            string? exceptionMessage = null,
            int? currentIndex = null,
            int? totalCount = null,
            string? productGlobalId = null,
            int? entityLabel = null,
            string? expressType = null)
        {
            lock (SyncRoot)
            {
                if (_writer == null)
                {
                    return;
                }

                string[] fields =
                [
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    CurrentModelRole,
                    CurrentIfcPath,
                    stage,
                    subStage,
                    status,
                    startedUtc.ToString("O", CultureInfo.InvariantCulture),
                    completedUtc.ToString("O", CultureInfo.InvariantCulture),
                    elapsedMs.ToString(CultureInfo.InvariantCulture),
                    countName,
                    countValue.ToString(CultureInfo.InvariantCulture),
                    currentIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    totalCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    productGlobalId ?? string.Empty,
                    entityLabel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    expressType ?? string.Empty,
                    isCompleted ? "True" : "False",
                    exceptionType ?? string.Empty,
                    exceptionMessage ?? string.Empty,
                    Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture)
                ];

                if (!DebugOutputGate.EnableProjectPerformanceCapture || _writer == null)


                    return;



                _writer.WriteLine(string.Join(",", Array.ConvertAll(fields, EscapeCsv)));


                _writer.Flush();
            }
        }

        public static string GetModelRole(string ifcPath)
        {
            if (string.IsNullOrEmpty(ifcPath)) return "Manual";
            if (ifcPath.Contains("testBuilding4withMarks.ifc", StringComparison.OrdinalIgnoreCase))
                return "SmallBaseline";
            if (ifcPath.Contains("fianl_jizan_4.ifc", StringComparison.OrdinalIgnoreCase))
                return "LargeRepro";
            return "Manual";
        }

        private static string EscapeCsv(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }
    }
}
