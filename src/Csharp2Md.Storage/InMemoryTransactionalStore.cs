using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

public sealed class InMemoryTransactionalStore : ITransactionalStore
{
    private readonly Dictionary<string, CommittedPublication> _publications = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SolutionContribution> _contributions = new(StringComparer.Ordinal);
    private readonly IPackageProjector? _projector;
    private readonly IBatchComposer? _composer;
    private readonly int? _readingBudgetTokens;
    private readonly int? _maxFileReadsPerScenario;
    private readonly ImmutableArray<string> _allowlist;

    public InMemoryTransactionalStore(
        IPackageProjector? projector = null,
        IBatchComposer? composer = null,
        int? readingBudgetTokens = null,
        int? maxFileReadsPerScenario = null,
        ImmutableArray<string> allowlist = default)
    {
        _projector = projector;
        _composer = composer;
        _readingBudgetTokens = readingBudgetTokens;
        _maxFileReadsPerScenario = maxFileReadsPerScenario;
        _allowlist = allowlist;
    }

    internal IReadOnlyDictionary<string, SolutionContribution> AccumulatedContributions => _contributions;

    internal PublishedBatch? LastPublishedBatch { get; private set; }

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

        var (fragments, envelope) = BatchPublication.Prepare(solutions, _contributions, _composer);
        LastPublishedBatch = new PublishedBatch(CanonicalJson.Write(envelope), fragments);
        _contributions.Clear();
    }

    public bool TryGetPublication(string solutionKey, out CommittedPublication publication)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionKey);
        return _publications.TryGetValue(SolutionCoordinate.For(solutionKey).Identity.Value, out publication!);
    }

    private IStoreSession Open(
        SolutionCoordinate coordinate,
        ISourceDocumentReader sourceReader,
        string solutionKey)
    {
        var identity = coordinate.Identity.Value;
        if (!_activeKeys.Add(identity))
        {
            throw new PublicationRejectedException("lock", identity);
        }

        return new Session(this, coordinate, solutionKey, sourceReader, _projector, _composer);
    }

    private void Release(string identity) => _activeKeys.Remove(identity);

    private void Publish(string identity, CommittedPublication publication) =>
        _publications[identity] = publication;

    private sealed class Session : IStoreSession, IDeferredFragmentStaging
    {
        private readonly InMemoryTransactionalStore _store;
        private readonly SolutionCoordinate _coordinate;
        private readonly string _solutionKey;
        private readonly ISourceDocumentReader _sourceReader;
        private readonly IPackageProjector? _projector;
        private readonly IBatchComposer? _composer;
        private readonly List<StagedFragment> _deferred = [];
        private FactualSnapshot _staged = FactualSnapshot.Empty;
        private bool _committed;

        public Session(
            InMemoryTransactionalStore store,
            SolutionCoordinate coordinate,
            string solutionKey,
            ISourceDocumentReader sourceReader,
            IPackageProjector? projector,
            IBatchComposer? composer)
        {
            _store = store;
            _coordinate = coordinate;
            _solutionKey = solutionKey;
            _sourceReader = sourceReader;
            _projector = projector;
            _composer = composer;
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

            var outcome = PublicationPipeline.Publish(
                _staged,
                new ManifestContext(_coordinate.Identity.Value, _coordinate.SolutionFileName),
                _coordinate,
                PackageDirectoryName(_coordinate),
                _projector,
                _composer,
                _sourceReader,
                createView: null,
                _store._readingBudgetTokens,
                _store._maxFileReadsPerScenario,
                _store._allowlist);
            var artifacts = outcome.Fragments.AddRange(_deferred);
            foreach (var fragment in _deferred)
            {
                _ = fragment.ReadPayload();
            }

            _committed = true;
            var identity = _coordinate.Identity.Value;
            _store.Release(identity);
            var publication = new CommittedPublication(_solutionKey, artifacts);
            _store.Publish(identity, publication);
            if (outcome.Contribution is not null)
            {
                _store._contributions[outcome.Contribution.SolutionIdentity] = outcome.Contribution;
            }

            return publication;
        }

        public void Abort()
        {
            _staged = FactualSnapshot.Empty;
            _store.Release(_coordinate.Identity.Value);
        }

        private void EnsureActive()
        {
            if (_committed)
            {
                throw new PublicationRejectedException("session-state", _solutionKey);
            }
        }
    }

    private static string PackageDirectoryName(SolutionCoordinate coordinate)
    {
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(coordinate.Identity.Value)))[..32];
        return "s-" + hex;
    }
}

internal sealed record PublishedBatch(
    ImmutableArray<byte> Manifest,
    ImmutableArray<StagedFragment> Composition);
