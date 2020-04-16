using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Easy.MessageHub;
using Remembrance.Contracts;
using Remembrance.Contracts.DAL.Local;
using Remembrance.Contracts.Sync;

namespace Remembrance.Core.Sync
{
    sealed class SynchronizationManager : ISynchronizationManager, IDisposable
    {
        readonly FileSystemWatcher _fileSystemWatcher;

        readonly ILog _logger;

        readonly IMessageHub _messageHub;

        readonly IRemembrancePathsProvider _remembrancePathsProvider;

        readonly IList<Guid> _subscriptionTokens = new List<Guid>();

        readonly IReadOnlyCollection<ISyncExtender> _syncExtenders;

        readonly IDictionary<string, IRepositorySynhronizer> _synchronizers;

        string? _allMachinesSharedBasePath;

        string? _thisMachineSharedPath;

        public SynchronizationManager(
            ILog logger,
            ILocalSettingsRepository localSettingsRepository,
            IReadOnlyCollection<IRepositorySynhronizer> synchronizers,
            IReadOnlyCollection<ISyncExtender> syncExtenders,
            IMessageHub messageHub,
            IRemembrancePathsProvider remembrancePathsProvider)
        {
            _remembrancePathsProvider = remembrancePathsProvider ?? throw new ArgumentNullException(nameof(remembrancePathsProvider));
            _ = synchronizers ?? throw new ArgumentNullException(nameof(synchronizers));
            if (!synchronizers.Any())
            {
                throw new ArgumentNullException(nameof(synchronizers));
            }

            _ = localSettingsRepository ?? throw new ArgumentNullException(nameof(localSettingsRepository));

            _synchronizers = synchronizers.ToDictionary(x => x.FileName, x => x);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _syncExtenders = syncExtenders ?? throw new ArgumentNullException(nameof(syncExtenders));
            _messageHub = messageHub ?? throw new ArgumentNullException(nameof(messageHub));
            _fileSystemWatcher = new FileSystemWatcher
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName
            };
            _fileSystemWatcher.Created += FileSystemWatcher_Changed;
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            OnSyncBusChanged(localSettingsRepository.SyncBus);

            _subscriptionTokens.Add(messageHub.Subscribe<SyncBus>(OnSyncBusChanged));
        }

        public void Dispose()
        {
            _fileSystemWatcher.Created -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Dispose();
            foreach (var subscriptionToken in _subscriptionTokens)
            {
                _messageHub.Unsubscribe(subscriptionToken);
            }

            _subscriptionTokens.Clear();
        }

        void OnSyncBusChanged(SyncBus syncBus)
        {
            _fileSystemWatcher.EnableRaisingEvents = false;
            if (syncBus == SyncBus.NoSync)
            {
                _allMachinesSharedBasePath = _thisMachineSharedPath = null;
                return;
            }

            {
                _thisMachineSharedPath = _remembrancePathsProvider.GetSharedPath(syncBus);
                _allMachinesSharedBasePath = Directory.GetParent(_thisMachineSharedPath).FullName;
                _fileSystemWatcher.Path = _allMachinesSharedBasePath;
            }

            SynchronizeExistingRepositories();
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemWatcher.EnableRaisingEvents = false;
            var directoryPath = Path.GetDirectoryName(e.FullPath);
            if (directoryPath == null || directoryPath == _thisMachineSharedPath)
            {
                return;
            }

            _logger.InfoFormat("File system event received: {0}: {1}, {2}", e.ChangeType, e.Name, e.FullPath);
            SynchronizeFile(e.FullPath);
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        void SynchronizeExistingRepositories()
        {
            if (_allMachinesSharedBasePath == null)
            {
                return;
            }

            var paths = Directory.GetDirectories(_allMachinesSharedBasePath).Where(directoryPath => directoryPath != _thisMachineSharedPath).SelectMany(Directory.GetFiles);
            Parallel.ForEach(
                paths,
                filePath =>
                {
                    _logger.TraceFormat("Processing file {0}...", filePath);
                    SynchronizeFile(filePath);
                });
            foreach (var syncExtender in _syncExtenders)
            {
                syncExtender.OnSynchronizationFinished();
            }
        }

        void SynchronizeFile(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            if (!_synchronizers.ContainsKey(fileName ?? throw new InvalidOperationException("fileName should not be null")))
            {
                _logger.WarnFormat("Unknown type of repository: {0}", filePath);
                return;
            }

            _synchronizers[fileName].SyncRepository(filePath);
        }
    }
}