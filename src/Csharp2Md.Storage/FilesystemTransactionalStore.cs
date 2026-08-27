using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

public sealed class FilesystemTransactionalStore : ITransactionalStore
{
    private readonly string _outputRoot;
    private readonly IPackageProjector? _projector;
    private readonly IBatchComposer? _composer;
    private readonly FilesystemRetryPolicy _retry;
    private readonly Dictionary<string, SolutionContribution> _contributions = new(StringComparer.Ordinal);

    public FilesystemTransactionalStore(
        string outputRoot,
        IPackageProjector? projector = null,
        IBatchComposer? composer = null)
        : this(outputRoot, projector, composer, FilesystemRetryPolicy.Default)
    {
    }

    internal FilesystemTransactionalStore(
        string outputRoot,
        IPackageProjector? projector,
        FilesystemRetryPolicy retry)
        : this(outputRoot, projector, composer: null, retry)
    {
    }

    internal FilesystemTransactionalStore(
        string outputRoot,
        IPackageProjector? projector,
        IBatchComposer? composer,
        FilesystemRetryPolicy retry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(retry.MaxAttempts, 1);
        _outputRoot = Path.GetFullPath(outputRoot);
        _projector = projector;
        _composer = composer;
        _retry = retry;
    }

    internal IReadOnlyDictionary<string, SolutionContribution> AccumulatedContributions => _contributions;

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

    public void PublishBatch(ImmutableArray<BatchSolutionRecord> solutions)
    {
        if (solutions.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Batch publication requires at least one solution record.", nameof(solutions));
        }

        EnsureWritableRoot();
        var lockPath = _outputRoot + ".lock";
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
            var (fragments, envelope) = BatchPublication.Prepare(solutions, _contributions, _composer);
            WriteRootArtifacts(envelope, fragments);
            _contributions.Clear();
        }
        catch (PublicationRejectedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PublicationRejectedException("io", exception.Message, exception);
        }
        finally
        {
            lockStream.Dispose();
            TryDeleteFile(lockPath);
        }
    }

    private void WriteRootArtifacts(BatchManifestEnvelope envelope, ImmutableArray<StagedFragment> fragments)
    {
        var compositionStaging = Path.Combine(_outputRoot, "composition.staging");
        var compositionDir = Path.Combine(_outputRoot, "composition");
        var manifestStaging = Path.Combine(_outputRoot, "batch-manifest.json.staging");
        var manifestPath = Path.Combine(_outputRoot, "batch-manifest.json");

        try
        {
            FilesystemIo.DeleteDirectory(compositionStaging, _retry);
            TryDeleteFile(manifestStaging);

            if (!fragments.IsDefaultOrEmpty)
            {
                Directory.CreateDirectory(compositionStaging);
                foreach (var fragment in fragments)
                {
                    var relative = fragment.CanonicalKey.StartsWith("composition/", StringComparison.Ordinal)
                        ? fragment.CanonicalKey["composition/".Length..]
                        : fragment.CanonicalKey;
                    var destination = Path.Combine(
                        compositionStaging,
                        relative.Replace('/', Path.DirectorySeparatorChar));
                    var directory = Path.GetDirectoryName(destination);
                    ArgumentException.ThrowIfNullOrEmpty(directory);
                    Directory.CreateDirectory(directory);
                    using var stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                    stream.Write(fragment.ReadPayload().AsSpan());
                }
            }

            var manifestBytes = CanonicalJson.Write(envelope);
            using (var stream = new FileStream(manifestStaging, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(manifestBytes.AsSpan());
            }

            ReplaceComposition(compositionStaging, compositionDir, fragments.IsDefaultOrEmpty);
            ReplaceFile(manifestStaging, manifestPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FilesystemIo.DeleteDirectory(compositionStaging, _retry);
            TryDeleteFile(manifestStaging);
            throw new PublicationRejectedException("io", exception.Message, exception);
        }
    }

    private void ReplaceComposition(string stagingPath, string destination, bool empty)
    {
        if (empty)
        {
            FilesystemIo.DeleteDirectory(stagingPath, _retry);
            FilesystemIo.DeleteDirectory(destination, _retry);
            return;
        }

        var bakPath = destination + ".bak";
        FilesystemIo.DeleteDirectory(bakPath, _retry);

        var replaced = false;
        if (Directory.Exists(destination))
        {
            FilesystemIo.MoveDirectory(destination, bakPath, _retry);
            replaced = true;
        }

        try
        {
            FilesystemIo.MoveDirectory(stagingPath, destination, _retry);
        }
        catch
        {
            if (replaced && Directory.Exists(bakPath) && !Directory.Exists(destination))
            {
                FilesystemIo.MoveDirectory(bakPath, destination, _retry);
            }

            throw;
        }

        FilesystemIo.DeleteDirectory(bakPath, _retry);
    }

    private static void ReplaceFile(string stagingPath, string destination)
    {
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(stagingPath, destination);
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
                this,
                solutionKey,
                coordinate,
                childPath,
                stagingPath,
                lockPath,
                lockStream,
                sourceReader,
                _projector,
                _composer,
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
        private readonly FilesystemTransactionalStore _store;
        private readonly string _solutionKey;
        private readonly SolutionCoordinate _coordinate;
        private readonly string _childPath;
        private readonly string _stagingPath;
        private readonly string _lockPath;
        private readonly FileStream _lockStream;
        private readonly ISourceDocumentReader _sourceReader;
        private readonly IPackageProjector? _projector;
        private readonly IBatchComposer? _composer;
        private readonly FilesystemRetryPolicy _retry;
        private readonly List<StagedFragment> _deferred = [];
        private FactualSnapshot _staged = FactualSnapshot.Empty;
        private bool _committed;
        private bool _released;

        public Session(
            FilesystemTransactionalStore store,
            string solutionKey,
            SolutionCoordinate coordinate,
            string childPath,
            string stagingPath,
            string lockPath,
            FileStream lockStream,
            ISourceDocumentReader sourceReader,
            IPackageProjector? projector,
            IBatchComposer? composer,
            FilesystemRetryPolicy retry)
        {
            _store = store;
            _solutionKey = solutionKey;
            _coordinate = coordinate;
            _childPath = childPath;
            _stagingPath = stagingPath;
            _lockPath = lockPath;
            _lockStream = lockStream;
            _sourceReader = sourceReader;
            _projector = projector;
            _composer = composer;
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
                var outcome = PublicationPipeline.Publish(
                    _staged,
                    new ManifestContext(_coordinate.Identity.Value, _coordinate.SolutionFileName),
                    _coordinate,
                    Path.GetFileName(_childPath),
                    _projector,
                    _composer,
                    _sourceReader);
                var artifacts = outcome.Fragments.AddRange(_deferred);
                WriteStaging(artifacts);
                SwapStagingIntoChild();
                if (outcome.Contribution is not null)
                {
                    _store._contributions[outcome.Contribution.SolutionIdentity] = outcome.Contribution;
                }

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
