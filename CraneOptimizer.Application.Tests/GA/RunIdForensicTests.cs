using System;
using Xunit;
using CraneOptimizer.GAExperiment;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Tests.GA
{
    public class RunIdForensicTests
    {
        [Fact]
        public void GenerateRunId_DifferentParameterSetIds_ProduceDifferentIds()
        {
            // Arrange
            string projectFile = @"F:\06-ITI\00-Project\tryProjects\auditmodel\project.db";
            string craneModel = "STC800";
            int seed = 42;

            var settingsA = new GASettings
            {
                ParameterSetId = "B02",
                ParameterSetName = "Balanced_MedPop",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 5.0
            };

            var settingsB = new GASettings
            {
                ParameterSetId = "R02",
                ParameterSetName = "MinReloc_HighWeight",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 25.0
            };

            // Act
            string idA = Program.GenerateRunId(projectFile, craneModel, seed, settingsA);
            string idB = Program.GenerateRunId(projectFile, craneModel, seed, settingsB);

            // Assert
            Assert.NotEqual(idA, idB);
        }

        [Fact]
        public void GenerateRunId_B02_R02_C01_NoLongerCollide()
        {
            // Arrange
            string projectFile = @"F:\06-ITI\00-Project\tryProjects\auditmodel\project.db";
            string craneModel = "STC800";
            int seed = 42;

            var b02 = new GASettings
            {
                ParameterSetId = "B02",
                ParameterSetName = "Balanced_MedPop",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 5.0
            };

            var r02 = new GASettings
            {
                ParameterSetId = "R02",
                ParameterSetName = "MinReloc_HighWeight",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 25.0
            };

            var c01 = new GASettings
            {
                ParameterSetId = "C01",
                ParameterSetName = "Friendly_LowPenalties",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 5.0,
                RadiusPenaltyWeight = 1.0,
                HeightPenaltyWeight = 2.0,
                OverweightPenaltyWeight = 5.0
            };

            // Act
            string idB02 = Program.GenerateRunId(projectFile, craneModel, seed, b02);
            string idR02 = Program.GenerateRunId(projectFile, craneModel, seed, r02);
            string idC01 = Program.GenerateRunId(projectFile, craneModel, seed, c01);

            // Assert
            Assert.NotEqual(idB02, idR02);
            Assert.NotEqual(idB02, idC01);
            Assert.NotEqual(idR02, idC01);
        }

        [Fact]
        public void GenerateRunId_SameSettingsSameSeed_IsDeterministic()
        {
            // Arrange
            string projectFile = @"F:\06-ITI\00-Project\tryProjects\auditmodel\project.db";
            string craneModel = "STC800";
            int seed = 42;

            var settingsA = new GASettings
            {
                ParameterSetId = "B02",
                ParameterSetName = "Balanced_MedPop",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 5.0
            };

            var settingsB = new GASettings
            {
                ParameterSetId = "B02",
                ParameterSetName = "Balanced_MedPop",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 5.0
            };

            // Act
            string id1 = Program.GenerateRunId(projectFile, craneModel, seed, settingsA);
            string id2 = Program.GenerateRunId(projectFile, craneModel, seed, settingsB);

            // Assert
            Assert.Equal(id1, id2);
        }

        [Fact]
        public void GenerateRunId_DifferentSeeds_ProduceDifferentIds()
        {
            // Arrange
            string projectFile = @"F:\06-ITI\00-Project\tryProjects\auditmodel\project.db";
            string craneModel = "STC800";

            var settings = new GASettings
            {
                ParameterSetId = "B02",
                ParameterSetName = "Balanced_MedPop",
                PopulationSize = 60,
                MaxGenerations = 80,
                MutationSigma = 0.005,
                MaxCranePositions = 2,
                RelocationWeight = 5.0
            };

            // Act
            string id1 = Program.GenerateRunId(projectFile, craneModel, 1, settings);
            string id2 = Program.GenerateRunId(projectFile, craneModel, 42, settings);
            string id3 = Program.GenerateRunId(projectFile, craneModel, 99, settings);

            // Assert
            Assert.NotEqual(id1, id2);
            Assert.NotEqual(id1, id3);
            Assert.NotEqual(id2, id3);
        }
    }
}
