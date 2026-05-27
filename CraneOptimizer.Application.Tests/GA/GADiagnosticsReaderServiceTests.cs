using System;
using System.IO;
using System.Linq;
using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

namespace CraneOptimizer.Application.Tests
{
    public class GADiagnosticsReaderServiceTests
    {
        [Fact]
        public void TestReadMissingFilesGracefully()
        {
            var service = new GADiagnosticsReaderService();

            // Run history with invalid/missing folder
            var history = service.ReadRunHistory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Assert.Empty(history);

            // Detailed diagnostics with missing file path
            var detailed = service.ReadDetailedDiagnostics(Path.Combine(Path.GetTempPath(), "does_not_exist.csv"));
            Assert.Empty(detailed);

            // Failure breakdown with missing file path
            var breakdown = service.ReadFailureBreakdown(Path.Combine(Path.GetTempPath(), "does_not_exist.csv"));
            Assert.Empty(breakdown);
        }

        [Fact]
        public void TestParseMockRunHistoryCorrectly()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            
            var reportsDir = Path.Combine(tempFolder, "Reports", "GA");
            Directory.CreateDirectory(reportsDir);

            var historyPath = Path.Combine(reportsDir, "ga_run_history.csv");

            // Mock CSV with quotes, commas inside fields
            var csvContent = 
                "Timestamp,ProjectName,ProjectFolderPath,IfcFileName,CraneNameOrId,TotalTargetElements,SelectedElements,LiftedElements,FailedElements,CoverageRatio,HealthLabel,FinalCraneSetupCount,GreedyIterations,PopulationSize,ConfiguredGenerations,ActualGenerationLoopCount,TotalGATimeMs,ElementAssignmentTimeMs,CraneCapacityLookupCount,CraneCapacityCacheHits,CraneCapacityCacheMisses,CraneCapacityCacheHitRatio,MemoryBeforeMB,MemoryAfterMB,MemoryDeltaMB,GCGen0Delta,GCGen1Delta,GCGen2Delta,ProfilingLogPath,DetailedDiagnosticsCsvPath,FailureBreakdownCsvPath\n" +
                "2026-05-24 12:00:00,\"Test, Project\",\"F:\\Path\",model.ifc,\"Crane A\",100,80,60,20,75.00%,Good,2,5,50,100,100,150.5,50.2,1000,800,200,80.00%,12.5,14.2,1.7,1,0,0,F:\\logs\\ga.txt,F:\\reports\\diag.csv,F:\\reports\\break.csv";

            File.WriteAllText(historyPath, csvContent);

            var service = new GADiagnosticsReaderService();
            var list = service.ReadRunHistory(tempFolder);

            Assert.Single(list);
            var item = list[0];
            Assert.Equal("2026-05-24 12:00:00", item.Timestamp);
            Assert.Equal("Test, Project", item.ProjectName);
            Assert.Equal("F:\\Path", item.ProjectFolderPath);
            Assert.Equal("model.ifc", item.IfcFileName);
            Assert.Equal("Crane A", item.CraneNameOrId);
            Assert.Equal(100, item.TotalTargetElements);
            Assert.Equal(80, item.SelectedElements);
            Assert.Equal(60, item.LiftedElements);
            Assert.Equal(20, item.FailedElements);
            Assert.Equal("75.00%", item.CoverageRatio);
            Assert.Equal("Good", item.HealthLabel);
            Assert.Equal(2, item.FinalCraneSetupCount);
            Assert.Equal(1000, int.Parse(item.CraneCapacityLookupCount));
            Assert.Equal("80.00%", item.CraneCapacityCacheHitRatio);
            Assert.Equal("1.7", item.MemoryDeltaMB);
            Assert.Equal("F:\\reports\\diag.csv", item.DetailedDiagnosticsCsvPath);
            Assert.Equal("F:\\reports\\break.csv", item.FailureBreakdownCsvPath);

            // Clean up
            Directory.Delete(tempFolder, true);
        }

        [Fact]
        public void TestParseMockDetailedDiagnosticsCorrectly()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");

            var csvContent = 
                "Timestamp,ProjectName,IfcFileName,CraneNameOrId,ElementId,ElementName,ElementType,StoreyOrElevation,Weight,COG_X,COG_Y,COG_Z,AssignedSetupIndex,AssignedCraneX,AssignedCraneY,AssignedCraneZ,BestRadius,RequiredBoomLength,RequiredCapacity,AvailableCapacity,LiftStatus,IsLifted,FailureReason,FailureCategory,SuggestedAction\n" +
                "2026-05-24 12:00:00,TestProj,model.ifc,CraneX,E1,\"Element, One\",Slab,12.5,5.2,1.1,2.2,3.3,1,10.0,20.0,0.0,8.5,15.0,5.2,7.5,Liftable,true,Liftable,Liftable,\"None\"\n" +
                "2026-05-24 12:00:00,TestProj,model.ifc,CraneX,E2,ElementTwo,Beam,12.5,10.5,4.4,5.5,6.6,-1,N/A,N/A,N/A,25.4,30.0,10.5,0.0,SpecialCase,false,RadiusBeyondLoadChart,RadiusBeyondLoadChart,\"Suggest bigger crane\"";

            File.WriteAllText(tempFile, csvContent);

            var service = new GADiagnosticsReaderService();
            var list = service.ReadDetailedDiagnostics(tempFile);

            Assert.Equal(2, list.Count);

            // E1
            var e1 = list[0];
            Assert.Equal("E1", e1.ElementId);
            Assert.Equal("Element, One", e1.ElementName);
            Assert.Equal("Slab", e1.ElementType);
            Assert.Equal("12.5", e1.StoreyOrElevation);
            Assert.Equal(5.2, e1.Weight);
            Assert.Equal(1.1, e1.CogX);
            Assert.Equal(1, e1.AssignedSetupIndex);
            Assert.Equal("10.0", e1.AssignedCraneX);
            Assert.Equal(8.5, e1.BestRadius);
            Assert.Equal(5.2, e1.RequiredCapacity);
            Assert.Equal(7.5, e1.AvailableCapacity);
            Assert.Equal("Liftable", e1.LiftStatus);
            Assert.True(e1.IsLifted);
            Assert.Equal("Liftable", e1.FailureCategory);

            // E2
            var e2 = list[1];
            Assert.Equal("E2", e2.ElementId);
            Assert.Equal("ElementTwo", e2.ElementName);
            Assert.Equal("Beam", e2.ElementType);
            Assert.Equal(10.5, e2.Weight);
            Assert.Equal(-1, e2.AssignedSetupIndex);
            Assert.Equal("N/A", e2.AssignedCraneX);
            Assert.False(e2.IsLifted);
            Assert.Equal("RadiusBeyondLoadChart", e2.FailureReason);
            Assert.Equal("RadiusBeyondLoadChart", e2.FailureCategory);
            Assert.Equal("Suggest bigger crane", e2.SuggestedAction);

            // Clean up
            File.Delete(tempFile);
        }

        [Fact]
        public void TestParseMockBreakdownCorrectly()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");

            var csvContent = 
                "FailureCategory,Count,PercentageOfFailedElements,PercentageOfSelectedElements\n" +
                "OutOfRadius,5,50.00%,10.00%\n" +
                "RadiusBeyondLoadChart,2,20.00%,4.00%";

            File.WriteAllText(tempFile, csvContent);

            var service = new GADiagnosticsReaderService();
            var list = service.ReadFailureBreakdown(tempFile);

            Assert.Equal(2, list.Count);

            var b1 = list[0];
            Assert.Equal("OutOfRadius", b1.FailureCategory);
            Assert.Equal(5, b1.Count);
            Assert.Equal("50.00%", b1.PercentageOfFailedElements);
            Assert.Equal("10.00%", b1.PercentageOfSelectedElements);
            Assert.Equal(50.00, b1.PctFailedValue);
            Assert.Equal(10.00, b1.PctSelectedValue);

            var b2 = list[1];
            Assert.Equal("RadiusBeyondLoadChart", b2.FailureCategory);
            Assert.Equal(2, b2.Count);
            Assert.Equal(20.0, b2.PctFailedValue);
            Assert.Equal(4.0, b2.PctSelectedValue);

            // Clean up
            File.Delete(tempFile);
        }
    }
}
