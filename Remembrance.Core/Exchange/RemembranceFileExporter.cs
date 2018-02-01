using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Remembrance.Contracts;
using Remembrance.Contracts.CardManagement.Data;
using Remembrance.Contracts.DAL.Local;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.DAL.Shared;
using Remembrance.Contracts.Exchange;
using Remembrance.Core.CardManagement.Data;
using Scar.Common.Events;

namespace Remembrance.Core.Exchange
{
    [UsedImplicitly]
    internal sealed class RemembranceFileExporter : IFileExporter
    {
        private static readonly JsonSerializerSettings ExportEntrySerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new TranslationEntryContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly ITranslationEntryProcessor _translationEntryProcessor;

        [NotNull]
        private readonly ITranslationEntryRepository _translationEntryRepository;

        [NotNull]
        private readonly IWordPriorityRepository _wordPriorityRepository;

        [NotNull]
        private readonly IEqualityComparer<IWord> _wordsEqualityComparer;

        public RemembranceFileExporter(
            [NotNull] ITranslationEntryRepository translationEntryRepository,
            [NotNull] ITranslationDetailsRepository translationDetailsRepository,
            [NotNull] ILog logger,
            [NotNull] ITranslationEntryProcessor translationEntryProcessor,
            [NotNull] IEqualityComparer<IWord> wordsEqualityComparer,
            [NotNull] IWordPriorityRepository wordPriorityRepository)
        {
            _wordPriorityRepository = wordPriorityRepository ?? throw new ArgumentNullException(nameof(wordPriorityRepository));
            _wordsEqualityComparer = wordsEqualityComparer ?? throw new ArgumentNullException(nameof(wordsEqualityComparer));
            _translationEntryProcessor = translationEntryProcessor ?? throw new ArgumentNullException(nameof(translationEntryProcessor));
            _translationEntryRepository = translationEntryRepository ?? throw new ArgumentNullException(nameof(translationEntryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<ProgressEventArgs> Progress;

        public async Task<ExchangeResult> ExportAsync(string fileName, CancellationToken cancellationToken)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var translationEntries = _translationEntryRepository.GetAll();
            var exportEntries = new List<RemembranceExchangeEntry>(translationEntries.Length);
            var totalCount = translationEntries.Length;
            var count = 0;
            foreach (var translationEntry in translationEntries)
            {
                var translationDetails = await _translationEntryProcessor.ReloadTranslationDetailsIfNeededAsync(translationEntry.Id, translationEntry.ManualTranslations, cancellationToken).ConfigureAwait(false);
                var translationInfo = new TranslationInfo(translationEntry, translationDetails);
                var priorityWords = new HashSet<ExchangeWord>(_wordsEqualityComparer);
                foreach (var translationVariant in translationInfo.TranslationDetails.TranslationResult.PartOfSpeechTranslations.SelectMany(partOfSpeechTranslation => partOfSpeechTranslation.TranslationVariants))
                {
                    if (_wordPriorityRepository.Check(new WordKey(translationEntry.Id, translationVariant)))
                    {
                        priorityWords.Add(new ExchangeWord(translationVariant));
                    }

                    if (translationVariant.Synonyms == null)
                    {
                        continue;
                    }

                    foreach (var synonym in translationVariant.Synonyms.Where(synonym => _wordPriorityRepository.Check(new WordKey(translationEntry.Id, synonym))))
                    {
                        priorityWords.Add(new ExchangeWord(synonym));
                    }
                }

                exportEntries.Add(
                    new RemembranceExchangeEntry(
                        priorityWords.Any()
                            ? priorityWords
                            : null,
                        translationEntry));
                OnProgress(Interlocked.Increment(ref count), totalCount);
            }

            try
            {
                var json = JsonConvert.SerializeObject(exportEntries, Formatting.Indented, ExportEntrySerializerSettings);
                File.WriteAllText(fileName, json);
                return new ExchangeResult(true, null, exportEntries.Count);
            }
            catch (IOException ex)
            {
                _logger.Warn("Cannot save file to disk", ex);
            }
            catch (JsonSerializationException ex)
            {
                _logger.Warn("Cannot serialize object", ex);
            }

            return new ExchangeResult(false, null, 0);
        }

        private void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }
    }
}