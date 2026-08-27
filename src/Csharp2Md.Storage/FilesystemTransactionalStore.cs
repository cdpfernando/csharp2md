using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage;

public sealed class FilesystemTransactionalStore : ITransactionalStore
{
    private readonly string _outputRoot;

    public FilesystemTransactionalStore(string outputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        _outputRoot = Path.GetFullPath(outputRoot);
    }

    public IStoreSession Open(string solutionKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionKey);
        EnsureWritableRoot();

        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(solutionKey)))[..32];
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

            return new Session(solutionKey, hex, childPath, stagingPath, lockPath, lockStream);
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

    private sealed class Session : IStoreSession
    {
        private readonly string _solutionKey;
        private readonly string _hex;
        private readonly string _childPath;
        private readonly string _stagingPath;
        private readonly string _lockPath;
        private readonly FileStream _lockStream;
        private FactualSnapshot _staged = FactualSnapshot.Empty;
        private bool _committed;
        private bool _released;

        public Session(
            string solutionKey,
            string hex,
            string childPath,
            string stagingPath,
            string lockPath,
            FileStream lockStream)
        {
            _solutionKey = solutionKey;
            _hex = hex;
            _childPath = childPath;
            _stagingPath = stagingPath;
            _lockPath = lockPath;
            _lockStream = lockStream;
        }

        public void Stage(FactualSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            EnsureActive();
            _staged = _staged.Merge(snapshot);
        }

        public CommittedPublication Commit()
        {
            EnsureActive();

            try
            {
            var artifacts = PublicationPipeline.Publish(
                _staged,
                new ManifestContext(_hex, Path.GetFileName(_solutionKey)));
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
                File.WriteAllBytes(destination, [.. fragment.Payload]);
            }
        }

        private void SwapStagingIntoChild()
        {
            var bakPath = _childPath + ".bak";
            if (Directory.Exists(bakPath))
            {
                Directory.Delete(bakPath, recursive: true);
            }

            var replaced = false;
            if (Directory.Exists(_childPath))
            {
                MoveDirectory(_childPath, bakPath);
                replaced = true;
            }

            try
            {
                MoveDirectory(_stagingPath, _childPath);
            }
            catch
            {
                if (replaced && Directory.Exists(bakPath) && !Directory.Exists(_childPath))
                {
                    MoveDirectory(bakPath, _childPath);
                }

                throw;
            }

            if (Directory.Exists(bakPath))
            {
                Directory.Delete(bakPath, recursive: true);
            }
        }

        private static void MoveDirectory(string source, string destination)
        {
            const int maxAttempts = 8;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Move(source, destination);
                    return;
                }
                catch (Exception exception) when (
                    attempt < maxAttempts
                    && exception is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(15 * attempt);
                }
            }
        }

        private void DeleteStagingDirectory()
        {
            if (Directory.Exists(_stagingPath))
            {
                Directory.Delete(_stagingPath, recursive: true);
            }
        }

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
