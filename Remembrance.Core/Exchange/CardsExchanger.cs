using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Easy.MessageHub;
using JetBrains.Annotations;
using Remembrance.Contracts.CardManagement;
using Remembrance.Contracts.CardManagement.Data;
using Remembrance.Resources;
using Scar.Common.Events;
using Scar.Common.IO;
using Scar.Common.Messages;

namespace Remembrance.Core.Exchange
{
    [UsedImplicitly]
    internal sealed class CardsExchanger : ICardsExchanger, IDisposable
    {
        private const string JsonFilesFilter = "Json files (*.json)|*.json;";

        [NotNull]
        private readonly IFileExporter _exporter;

        [NotNull]
        private readonly IFileImporter[] _importers;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly IMessageHub _messenger;

        [NotNull]
        private readonly IOpenFileService _openFileService;

        [NotNull]
        private readonly ISaveFileService _saveFileService;

        public CardsExchanger(
            [NotNull] IOpenFileService openFileService,
            [NotNull] ISaveFileService saveFileService,
            [NotNull] ILog logger,
            [NotNull] IFileExporter exporter,
            [NotNull] IFileImporter[] importers,
            [NotNull] IMessageHub messenger)
        {
            _openFileService = openFileService ?? throw new ArgumentNullException(nameof(openFileService));
            _saveFileService = saveFileService ?? throw new ArgumentNullException(nameof(saveFileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            _importers = importers ?? throw new ArgumentNullException(nameof(importers));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            foreach (var importer in importers)
                importer.Progress += Importer_Progress;
        }

        public event EventHandler<ProgressEventArgs> Progress;

        public async Task ExportAsync(CancellationToken cancellationToken)
        {
            if (!_saveFileService.SaveFileDialog($"{Texts.Title}: {Texts.Export}", JsonFilesFilter))
                return;

            _logger.Info($"Performing export to {_saveFileService.FileName}...");
            ExchangeResult exchangeResult = null;
            OnProgress(0, 1);
            try
            {
                await Task.Run(async () => { exchangeResult = await _exporter.ExportAsync(_saveFileService.FileName, cancellationToken).ConfigureAwait(false); }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                OnProgress(1, 1);
            }

            if (exchangeResult.Success)
            {
                _logger.Info($"Export to {_saveFileService.FileName} has been performed");
                _messenger.Publish(Texts.ExportSucceeded.ToMessage());
                Process.Start(_saveFileService.FileName);
            }
            else
            {
                _logger.Warn($"Export to {_saveFileService.FileName} failed");
                _messenger.Publish(Texts.ExportFailed.ToError());
            }
        }

        public async Task ImportAsync(CancellationToken cancellationToken)
        {
            if (!_openFileService.OpenFileDialog($"{Texts.Title}: {Texts.Import}", JsonFilesFilter))
                return;

            OnProgress(0, 1);
            try
            {
                await Task.Run(
                        async () =>
                        {
                            foreach (var importer in _importers)
                            {
                                _logger.Info($"Performing import from {_openFileService.FileName} with {importer.GetType().Name}...");
                                var exchangeResult = await importer.ImportAsync(_openFileService.FileName, cancellationToken).ConfigureAwait(false);

                                if (exchangeResult.Success)
                                {
                                    _logger.Info($"ImportAsync from {_openFileService.FileName} has been performed");
                                    var mainMessage = string.Format(Texts.ImportSucceeded, exchangeResult.Count);
                                    _messenger.Publish(
                                        exchangeResult.Errors != null
                                            ? $"[{importer.GetType().Name}] {mainMessage}. {Texts.ImportErrors}:{Environment.NewLine}{string.Join(Environment.NewLine, exchangeResult.Errors)}".ToWarning()
                                            : $"[{importer.GetType().Name}] {mainMessage}".ToMessage());
                                    return;
                                }

                                _logger.Warn($"ImportAsync from {_openFileService.FileName} failed");
                            }

                            _messenger.Publish(Texts.ImportFailed.ToError());
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                OnProgress(1, 1);
            }
        }

        public void Dispose()
        {
            foreach (var importer in _importers)
                importer.Progress -= Importer_Progress;
        }

        private void Importer_Progress(object sender, ProgressEventArgs e)
        {
            Progress?.Invoke(this, e);
        }

        private void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }
    }
}