using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

public sealed class FilesystemTransactionalStore : ITransactionalStore
{
    private readonly string _outputRoot;
    private readonly IPackageProjector? _projector;
    private readonly IBatchComposer? _composer;
    private readonly FilesystemRetryPolicy _retry;
    private readonly int? _readingBudgetTokens;
    private readonly int? _maxFileReadsPerScenario;
    private readonly ImmutableArray<string> _allowlist;
    private readonly Dictionary<string, SolutionContribution> _contributions = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="readingBudgetTokens"/> and <paramref name="maxFileReadsPerScenario"/> (T52's
    /// <c>--reading-budget-tokens</c> and <c>--max-file-reads-per-scenario</c>), absent an override, are
    /// the same declared defaults <see cref="Mapping.CeilingCalculator"/> already derives the enforced
    /// per-artifact ceiling from on every publish; <paramref name="allowlist"/> is the same allowlist
    /// <see cref="Analysis.AnalysisRequest"/> already admitted through the pipeline (T12), threaded here
    /// only so its digest reaches the published provenance (GCPC-058).
    /// </summary>
    public FilesystemTransactionalStore(
        string outputRoot,
        IPackageProjector? projector = null,
        IBatchComposer? composer = null,
        int? readingBudgetTokens = null,
        int? maxFileReadsPerScenario = null,
        ImmutableArray<string> allowlist = default)
        : this(
            outputRoot,
            projector,
            composer,
            FilesystemRetryPolicy.Default,
            readingBudgetTokens,
            maxFileReadsPerScenario,
            allowlist)
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
        : this(outputRoot, projector, composer, retry, null, null, default)
    {
    }

    internal FilesystemTransactionalStore(
        string outputRoot,
        IPackageProjector? projector,
        IBatchComposer? composer,
        FilesystemRetryPolicy retry,
        int? readingBudgetTokens,
        int? maxFileReadsPerScenario,
        ImmutableArray<string> allowlist)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(retry.MaxAttempts, 1);
        _outputRoot = Path.GetFullPath(outputRoot);
        _projector = projector;
        _composer = composer;
        _retry = retry;
        _readingBudgetTokens = readingBudgetTokens;
        _maxFileReadsPerScenario = maxFileReadsPerScenario;
        _allowlist = allowlist;
    }

    internal IReadOnlyDictionary<string, SolutionContribution> AccumulatedContributions => _contributions;

    /// <summary>
    /// Registers a contribution built by re-reading an already-published package (<see
    /// cref="ContributionReader"/>, T51's <c>compose</c> path) as if it had come from a live <see
    /// cref="IStoreSession.Commit"/> on this store instance, so <see cref="PublishBatch"/> composes it
    /// through the exact same path a live <c>analyze</c> batch uses -- no second write path is introduced.
    /// </summary>
    public void SeedContribution(string solutionIdentity, SolutionContribution contribution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionIdentity);
        ArgumentNullException.ThrowIfNull(contribution);
        _contributions[solutionIdentity] = contribution;
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
            var (fragments, envelope) = BatchPublication.Prepare(
                solutions,
                _contributions,
                _composer,
                PackagePresent);
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

    private bool PackagePresent(string identity) =>
        File.Exists(Path.Combine(
            _outputRoot,
            BatchManifestBuilder.PackageDirectoryName(identity),
            PackagePublisher.ManifestKey));

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
                    _sourceReader,
                    createView: null,
                    _store._readingBudgetTokens,
                    _store._maxFileReadsPerScenario,
                    _store._allowlist);
                var artifacts = outcome.Fragments.AddRange(_deferred);
                WriteStaging(artifacts);
                RewriteManifestWithWrittenByteSizes();
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

        /// <summary>
        /// GCPC-057/GCPC-061: deferred source fragments must remain single-read, so their real size is
        /// unknowable when <see cref="Mapping.ManifestBuilder"/> first constructs the manifest. Once all
        /// fragments have been written into the still-private staging directory, rebuild the manifest
        /// from those actual file lengths and re-shard it under the same published ceiling. Any failure
        /// still aborts staging before the atomic directory swap.
        /// </summary>
        private void RewriteManifestWithWrittenByteSizes()
        {
            var manifestPath = Path.Combine(_stagingPath, PackagePublisher.ManifestKey);
            var root = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(
                File.ReadAllBytes(manifestPath), PackagePublisher.ManifestKey);
            var resolved = ManifestSharder.Resolve(root, path => ReadStagedFile(path));
            var entries = resolved.Artifacts
                .Where(static entry => entry.Role != ManifestSharder.PartRole)
                .Select(entry => WithWrittenCardinality(entry, ReadStagedFile(entry.Path)))
                .ToImmutableArray();
            var rewritten = root with { Artifacts = entries };
            var ceilingBytes = root.Provenance?.ArtifactCeilingBytes ?? int.MaxValue;
            var fragments = ManifestSharder.ToFragments(rewritten, ceilingBytes);

            File.Delete(manifestPath);
            FilesystemIo.DeleteDirectory(Path.Combine(_stagingPath, "manifest"), _retry);
            foreach (var fragment in fragments)
            {
                var destination = Path.Combine(
                    _stagingPath,
                    fragment.CanonicalKey.Replace('/', Path.DirectorySeparatorChar));
                var directory = Path.GetDirectoryName(destination);
                ArgumentException.ThrowIfNullOrEmpty(directory);
                Directory.CreateDirectory(directory);
                using var stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                stream.Write(fragment.Payload.AsSpan());
            }
        }

        private ImmutableArray<byte> ReadStagedFile(string relativePath) =>
            File.ReadAllBytes(Path.Combine(
                _stagingPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar))).ToImmutableArray();

        private static ManifestEntry WithWrittenCardinality(
            ManifestEntry entry,
            ImmutableArray<byte> bytes) =>
            entry with
            {
                ByteSize = bytes.Length,
                Count = entry.Path == PackagePublisher.RegistryKey
                    ? entry.Count
                    : ManifestBuilder.CountTopLevelEntries(bytes.AsSpan()),
            };

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
