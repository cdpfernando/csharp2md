using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage;

public sealed class FilesystemTransactionalStore : ITransactionalStore
{
    private readonly string _outputRoot;
    private readonly IPackageProjector? _projector;
    private readonly FilesystemRetryPolicy _retry;

    public FilesystemTransactionalStore(string outputRoot, IPackageProjector? projector = null)
        : this(outputRoot, projector, FilesystemRetryPolicy.Default)
    {
    }

    internal FilesystemTransactionalStore(
        string outputRoot,
        IPackageProjector? projector,
        FilesystemRetryPolicy retry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(retry.MaxAttempts, 1);
        _outputRoot = Path.GetFullPath(outputRoot);
        _projector = projector;
        _retry = retry;
    }

    public IStoreSession Open(SolutionCoordinate coordinate, ISourceDocumentReader sourceReader)
    {
        ArgumentNullException.ThrowIfNull(sourceReader);
        return Open(coordinate, sourceReader, coordinate.Identity.Value);
    }

    public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionKey);
        ArgumentNullException.ThrowIfNull(sourceReader);
        return Open(SolutionCoordinate.For(solutionKey), sourceReader, solutionKey);
    }

    private IStoreSession Open(
        SolutionCoordinate coordinate,
        ISourceDocumentReader sourceReader,
        string solutionKey)
    {
        EnsureWritableRoot();

        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(coordinate.Identity.Value)))[..32];
        var childPath = Path.Combine(_outputRoot, "s-" + hex);
        var lockPath = childPath + ".lock";
        var stagingPath = childPath + ".staging";

        FileStream lockStream;
        try
        {
            lockStream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1);
        }
        catch (IOException)
        {
            throw new PublicationRejectedException("lock", _outputRoot);
        }

        try
        {
            if (Directory.Exists(childPath) && !File.Exists(Path.Combine(childPath, PackagePublisher.ManifestKey)))
            {
                throw new PublicationRejectedException("not-a-package", childPath);
            }

            return new Session(
                solutionKey,
                hex,
                childPath,
                stagingPath,
                lockPath,
                lockStream,
                sourceReader,
                _projector,
                _retry);
        }
        catch
        {
            lockStream.Dispose();
            TryDeleteFile(lockPath);
            throw;
        }
    }

    private void EnsureWritableRoot()
    {
        if (File.Exists(_outputRoot))
        {
            throw new PublicationRejectedException("not-a-package", _outputRoot);
        }

        try
        {
            Directory.CreateDirectory(_outputRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PublicationRejectedException("io", exception.Message, exception);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            Debug.WriteLine($"Failed to delete '{path}': {exception.Message}");
        }
    }

    private sealed class Session : IStoreSession, IDeferredFragmentStaging
    {
        private readonly string _solutionKey;
        private readonly string _hex;
        private readonly string _childPath;
        private readonly string _stagingPath;
        private readonly string _lockPath;
        private readonly FileStream _lockStream;
        private readonly ISourceDocumentReader _sourceReader;
        private readonly IPackageProjector? _projector;
        private readonly FilesystemRetryPolicy _retry;
        private readonly List<StagedFragment> _deferred = [];
        private FactualSnapshot _staged = FactualSnapshot.Empty;
        private bool _committed;
        private bool _released;

        public Session(
            string solutionKey,
            string hex,
            string childPath,
            string stagingPath,
            string lockPath,
            FileStream lockStream,
            ISourceDocumentReader sourceReader,
            IPackageProjector? projector,
            FilesystemRetryPolicy retry)
        {
            _solutionKey = solutionKey;
            _hex = hex;
            _childPath = childPath;
            _stagingPath = stagingPath;
            _lockPath = lockPath;
            _lockStream = lockStream;
            _sourceReader = sourceReader;
            _projector = projector;
            _retry = retry;
        }

        public void Stage(FactualSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            EnsureActive();
            _staged = _staged.Merge(snapshot);
        }

        public void StageDeferred(StagedFragment fragment)
        {
            ArgumentNullException.ThrowIfNull(fragment);
            EnsureActive();
            _deferred.Add(fragment);
        }

        public CommittedPublication Commit()
        {
            EnsureActive();
            _ = _sourceReader.Documents;

            try
            {
                var artifacts = PublicationPipeline.Publish(
                    _staged,
                    new ManifestContext(_hex, Path.GetFileName(_solutionKey)),
                    _projector,
                    _sourceReader);
                artifacts = artifacts.AddRange(_deferred);
                WriteStaging(artifacts);
                SwapStagingIntoChild();
                _committed = true;
                return new CommittedPublication(_solutionKey, artifacts);
            }
            catch (PublicationRejectedException)
            {
                DeleteStagingDirectory();
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DeleteStagingDirectory();
                throw new PublicationRejectedException("io", exception.Message, exception);
            }
            finally
            {
                ReleaseLock();
            }
        }

        public void Abort()
        {
            DeleteStagingDirectory();
            ReleaseLock();
        }

        private void WriteStaging(ImmutableArray<StagedFragment> artifacts)
        {
            DeleteStagingDirectory();
            Directory.CreateDirectory(_stagingPath);

            foreach (var fragment in artifacts)
            {
                var destination = Path.Combine(
                    _stagingPath,
                    fragment.CanonicalKey.Replace('/', Path.DirectorySeparatorChar));
                var directory = Path.GetDirectoryName(destination);
                ArgumentException.ThrowIfNullOrEmpty(directory);
                Directory.CreateDirectory(directory);
                using var stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                stream.Write(fragment.ReadPayload().AsSpan());
            }
        }

        private void SwapStagingIntoChild()
        {
            var bakPath = _childPath + ".bak";
            FilesystemIo.DeleteDirectory(bakPath, _retry);

            var replaced = false;
            if (Directory.Exists(_childPath))
            {
                FilesystemIo.MoveDirectory(_childPath, bakPath, _retry);
                replaced = true;
            }

            try
            {
                FilesystemIo.MoveDirectory(_stagingPath, _childPath, _retry);
            }
            catch
            {
                if (replaced && Directory.Exists(bakPath) && !Directory.Exists(_childPath))
                {
                    FilesystemIo.MoveDirectory(bakPath, _childPath, _retry);
                }

                throw;
            }

            FilesystemIo.DeleteDirectory(bakPath, _retry);
        }

        private void DeleteStagingDirectory() => FilesystemIo.DeleteDirectory(_stagingPath, _retry);

        private void ReleaseLock()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _lockStream.Dispose();
            TryDeleteFile(_lockPath);
        }

        private void EnsureActive()
        {
            if (_committed)
            {
                throw new PublicationRejectedException("session-state", _solutionKey);
            }
        }
    }
}
