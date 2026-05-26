using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CraneOptimizer.Application.Tests
{
    public class RenderPerformanceBenchmark
    {
        private static void RunOnSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
                throw new AggregateException("STA thread threw an exception", caught);
        }

        private static double GetPrivateMemoryMB()
        {
            try
            {
                return Process.GetCurrentProcess().PrivateMemorySize64 / (1024.0 * 1024.0);
            }
            catch
            {
                return GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            }
        }

        public record BenchmarkResult(
            string CaseName,
            string RenderQuality,
            int TotalElements,
            int VisibleElements,
            int Skipped,
            int Shapes,
            int MeshCreated,
            int CacheHit,
            int CacheMiss,
            int GeometryModels,
            int ModelVisuals,
            int Materials,
            int FallbackBoxes,
            int RealMeshElements,
            double DurationMs,
            double MemoryBeforeMB,
            double MemoryAfterMB,
            double MemoryDeltaMB);

        [Fact(Skip = "Exempt from standard test runs to prevent heavy local-file benchmarks unless explicitly intended.")]
        public void ExecuteBenchmarkSuite()
        {
            RunOnSta(() =>
            {
                var smallIfcPath = @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\teklaModelTestIFC_10.ifc";
                var mediumIfcPath = @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\Snowdon Towers Sample Structural_ifc4.ifc";

                var results = new List<BenchmarkResult>();

                // Reset cache to be clean at start of benchmark
                ElementVisualBuilder.ClearMeshCache("benchmark-init");

                // Execute for Small IFC if exists
                if (File.Exists(smallIfcPath))
                {
                    RunBenchmarkForFile("Small IFC (Tekla 10)", smallIfcPath, results);
                }

                // Execute for Medium IFC if exists
                if (File.Exists(mediumIfcPath))
                {
                    RunBenchmarkForFile("Medium IFC (Snowdon)", mediumIfcPath, results);
                }

                // Generate Markdown Artifact Table
                var artifactDir = @"C:\Users\Dell\.gemini\antigravity\brain\52c00ca6-6f04-4aee-9ba2-a2ece4d69034";
                Directory.CreateDirectory(artifactDir);
                var reportPath = Path.Combine(artifactDir, "captured_metrics.md");

                var sb = new StringBuilder();
                sb.AppendLine("# Captured Render Performance Baseline Metrics");
                sb.AppendLine();
                sb.AppendLine("This report documents the baseline render performance metrics captured by running the real engineering extraction and rendering pipelines under various quality and load configurations.");
                sb.AppendLine();
                sb.AppendLine("## Benchmark Execution Details");
                sb.AppendLine($"- **Executed At:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"- **Small IFC Model:** `{Path.GetFileName(smallIfcPath)}` (Size: {new FileInfo(smallIfcPath).Length / 1024d:F1} KB)");
                sb.AppendLine($"- **Medium/Large IFC Model:** `{Path.GetFileName(mediumIfcPath)}` (Size: {new FileInfo(mediumIfcPath).Length / (1024d * 1024d):F2} MB)");
                sb.AppendLine();
                sb.AppendLine("## Performance Metrics Table");
                sb.AppendLine();
                sb.AppendLine("| Case | Quality | Total | Visible | Skipped | Shapes | MeshCreated | CacheHit | CacheMiss | GeomModels | ModelVisuals | Materials | FallbackBoxes | RealMeshes | Duration (ms) | MemBefore (MB) | MemAfter (MB) | MemDelta (MB) |");
                sb.AppendLine("| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

                foreach (var r in results)
                {
                    sb.AppendLine(
                        $"| {r.CaseName} " +
                        $"| {r.RenderQuality} " +
                        $"| {r.TotalElements} " +
                        $"| {r.VisibleElements} " +
                        $"| {r.Skipped} " +
                        $"| {r.Shapes} " +
                        $"| {r.MeshCreated} " +
                        $"| {r.CacheHit} " +
                        $"| {r.CacheMiss} " +
                        $"| {r.GeometryModels} " +
                        $"| {r.ModelVisuals} " +
                        $"| {r.Materials} " +
                        $"| {r.FallbackBoxes} " +
                        $"| {r.RealMeshElements} " +
                        $"| {r.DurationMs:F2} " +
                        $"| {r.MemoryBeforeMB:F1} " +
                        $"| {r.MemoryAfterMB:F1} " +
                        $"| {r.MemoryDeltaMB:F1} |");
                }

                sb.AppendLine();
                sb.AppendLine("## Captured Console [RenderStats] Logs");
                sb.AppendLine("```text");
                foreach (var r in results)
                {
                    sb.AppendLine(
                        $"[RenderStats] Case={r.CaseName} Quality={r.RenderQuality} Elements={r.TotalElements} " +
                        $"Visible={r.VisibleElements} Skipped={r.Skipped} Shapes={r.Shapes} " +
                        $"MeshCreated={r.MeshCreated} CacheHit={r.CacheHit} GeometryModels={r.GeometryModels} " +
                        $"ModelVisuals={r.ModelVisuals} FallbackBoxes={r.FallbackBoxes} DurationMs={r.DurationMs:F1} " +
                        $"MemBefore={r.MemoryBeforeMB:F1}MB MemAfter={r.MemoryAfterMB:F1}MB Delta={r.MemoryDeltaMB:F1}MB");
                }
                sb.AppendLine("```");

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);

                // Print to console for VSTest runner visibility
                Console.WriteLine(sb.ToString());
            });
        }

        private void RunBenchmarkForFile(string caseName, string ifcPath, List<BenchmarkResult> results)
        {
            var sessionService = new IfcModelSessionService();
            var auditService = new IfcDiagnosticAuditService(sessionService);
            var exporterProfileResolver = new IfcExporterProfileResolver();
            var normalizationService = new IfcNormalizationDecisionService(exporterProfileResolver);
            var readerRepository = new IfcReaderRepository(auditService, normalizationService, sessionService);

            // 1. Initial Load & Full Extraction
            var elements = readerRepository.ReadElements(ifcPath);

            var origin = new DomainPoint3D(0, 0, 0);
            var placementOffset = new DomainPoint3D(0, 0, 0);

            // Scenarios A: Low Quality
            ExecuteAndRecord(caseName, "Low", elements, readerRepository, origin, placementOffset, results);

            // Scenarios B: Medium Quality (First Render - Clean Cache)
            ElementVisualBuilder.ClearMeshCache("benchmark-refresh");
            ExecuteAndRecord(caseName, "Medium", elements, readerRepository, origin, placementOffset, results);

            // Scenarios C: Medium Quality (Second Render - Warm Cache)
            ExecuteAndRecord(caseName, "Medium (Warm Cache)", elements, readerRepository, origin, placementOffset, results);

            // Scenarios D: High Quality (First Render - Clean Cache)
            ElementVisualBuilder.ClearMeshCache("benchmark-high-refresh");
            ExecuteAndRecord(caseName, "High", elements, readerRepository, origin, placementOffset, results);

            // Scenarios D2: High Quality (Second Render - Warm Cache)
            ExecuteAndRecord(caseName, "High (Warm Cache)", elements, readerRepository, origin, placementOffset, results);

            // Scenarios E: Filter Change (Simulating 40% hidden elements)
            var filteredElements = elements.Select((e, idx) => new PrecastElement
            {
                ElementID = e.ElementID,
                ElementRevitId = e.ElementRevitId,
                ElementMark = e.ElementMark,
                ElementType = e.ElementType,
                ElementLength = e.ElementLength,
                ElementWidth = e.ElementWidth,
                ElementHeight = e.ElementHeight,
                ElementMaterial = e.ElementMaterial,
                ElementWT = e.ElementWT,
                ElementVolume = e.ElementVolume,
                ElementCOG = e.ElementCOG,
                BBoxLength = e.BBoxLength,
                BBoxWidth = e.BBoxWidth,
                BBoxHeight = e.BBoxHeight,
                IncludeInRender = (idx % 5 < 2) ? false : e.IncludeInRender,
                IncludeInGa = e.IncludeInGa
            }).ToList();
            ExecuteAndRecord(caseName, "Medium (Filter Change)", filteredElements, readerRepository, origin, placementOffset, results);

            // Scenarios F: DB Restored project before xBIM session is ready (falls back to bounding boxes)
            readerRepository.ClearData(); // Clear session
            ExecuteAndRecord(caseName, "DB Restored (No Session)", elements, readerRepository, origin, placementOffset, results);

            // Scenarios G: DB Restored after render-only xBIM session upgrades to real meshes
            readerRepository.InitializeSessionForRenderingAsync(ifcPath).GetAwaiter().GetResult(); // upgrade background session
            ExecuteAndRecord(caseName, "DB Restored (Upgraded Session)", elements, readerRepository, origin, placementOffset, results);

            // Clear session for next file
            readerRepository.ClearData();
        }

        private void ExecuteAndRecord(
            string caseName,
            string desc,
            List<PrecastElement> elements,
            IIfcReaderRepository readerRepository,
            DomainPoint3D origin,
            DomainPoint3D placementOffset,
            List<BenchmarkResult> results)
        {
            var quality = ElementVisualBuilder.MeshQuality.Medium;
            if (desc.StartsWith("Low")) quality = ElementVisualBuilder.MeshQuality.Low;
            else if (desc.StartsWith("High")) quality = ElementVisualBuilder.MeshQuality.High;

            ElementVisualBuilder.CurrentQuality = quality;

            var builder = new ElementVisualBuilder(readerRepository, origin, placementOffset);
            builder.ResetCounters();

            // Run garbage collection first for more stable memory metrics
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memBefore = GetPrivateMemoryMB();
            var sw = Stopwatch.StartNew();

            var visual = builder.BuildBatch(elements.Select(e => (e, (LiftStatus?)null, false)));

            sw.Stop();
            var memAfter = GetPrivateMemoryMB();

            results.Add(new BenchmarkResult(
                CaseName: caseName,
                RenderQuality: desc,
                TotalElements: builder.DiagTotalElements,
                VisibleElements: builder.DiagVisibleElements,
                Skipped: builder.DiagIncludeInRenderSkipped,
                Shapes: builder.DiagShapeInstances,
                MeshCreated: builder.DiagMeshesCreated,
                CacheHit: builder.DiagMeshCacheHits,
                CacheMiss: builder.DiagMeshCacheMisses,
                GeometryModels: builder.DiagGeometryModel3DCount,
                ModelVisuals: builder.DiagModelVisual3DCount,
                Materials: builder.DiagMaterialCreated,
                FallbackBoxes: builder.DiagFallbackBoxCount,
                RealMeshElements: builder.DiagRealMeshElements,
                DurationMs: sw.Elapsed.TotalMilliseconds,
                MemoryBeforeMB: memBefore,
                MemoryAfterMB: memAfter,
                MemoryDeltaMB: memAfter - memBefore
            ));
        }
    }
}
