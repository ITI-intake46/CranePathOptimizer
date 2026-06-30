using CraneOptimizer.Domain.Diagnostics;
using System;

namespace CraneOptimizer.Infrastructure.Ifc
{
    public sealed class IfcModelSessionService : IIfcModelSessionService
    {
        private readonly object _gate = new();
        private IIfcModelSession? _current;

        public IIfcModelSession? Current
        {
            get
            {
                lock (_gate)
                {
                    return _current?.IsDisposed == true ? null : _current;
                }
            }
        }

        public IIfcModelSession OpenSession(string ifcPath, IProgress<IfcLoadingProgressUpdate>? progress = null, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ClearCore();
                _current = IfcModelSession.Open(ifcPath, progress, cancellationToken);
                return _current;
            }
        }

        public IIfcModelSession? GetActiveSession(string ifcPath)
        {
            lock (_gate)
            {
                if (_current == null || _current.IsDisposed)
                    return null;

                var requestedIdentity = IfcModelSession.CreateFileIdentity(ifcPath);
                return string.Equals(_current.FileIdentity, requestedIdentity, StringComparison.Ordinal)
                    ? _current
                    : null;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                ClearCore();
            }
        }

        public void Dispose()
        {
            Clear();
        }

        private void ClearCore()
        {
            if (_current == null)
                return;

            try
            {
                _current.Dispose();
            }
            finally
            {
                _current = null;
            }
        }
    }
}
