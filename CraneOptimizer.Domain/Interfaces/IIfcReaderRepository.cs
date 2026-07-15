using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.CreatedProject;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcReaderRepository : IDisposable
    {
        List<PrecastElement> ReadElements(string ifcPath);
        List<PrecastElement> ReadElements(string ifcPath, IfcReadingOptions? options);
        List<PrecastElement> ReadElements(string path, IProgress<double> progress);
        List<PrecastElement> ReadElements(string path, IProgress<double> progress, IfcReadingOptions? options, CancellationToken cancellationToken = default);

        List<PrecastElement> ReadElements(
            string path,
            IProgress<IfcLoadingProgressUpdate> progress,
            IfcReadingOptions? options,
            CancellationToken cancellationToken = default)
        {
            var doubleProgress = new Progress<double>(p => progress.Report(new IfcLoadingProgressUpdate(p, string.Empty, string.Empty)));
            return ReadElements(path, doubleProgress, options, cancellationToken);
        }

        Task InitializeSessionForRenderingAsync(string ifcPath, CancellationToken cancellationToken = default);

        double ModelLengthFactor { get; }
        double? ModelGroundElevation { get; }

        IfcGeoreferencingResult? GetGeoreferencingData();

        void ClearData();
    }
}
