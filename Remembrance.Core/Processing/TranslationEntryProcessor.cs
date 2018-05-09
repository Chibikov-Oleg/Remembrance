using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Easy.MessageHub;
using JetBrains.Annotations;
using Remembrance.Contracts.CardManagement;
using Remembrance.Contracts.DAL.Local;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.DAL.Shared;
using Remembrance.Contracts.Languages;
using Remembrance.Contracts.Processing;
using Remembrance.Contracts.Processing.Data;
using Remembrance.Contracts.Translate;
using Remembrance.Contracts.Translate.Data.WordsTranslator;
using Remembrance.Contracts.View.Settings;
using Remembrance.Resources;
using Scar.Common;
using Scar.Common.Messages;
using Scar.Common.WPF.View.Contracts;

namespace Remembrance.Core.Processing
{
    [UsedImplicitly]
    internal sealed class TranslationEntryProcessor : ITranslationEntryProcessor
    {
        [NotNull]
        private readonly ITranslationDetailsCardManager _cardManager;

        [NotNull]
        private readonly ILanguageManager _languageManager;

        [NotNull]
        private readonly ILearningInfoRepository _learningInfoRepository;

        [NotNull]
        private readonly Func<ILoadingWindow> _loadingWindowFactory;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly IMessageHub _messageHub;

        [NotNull]
        private readonly IPrepositionsInfoRepository _prepositionsInfoRepository;

        [NotNull]
        private readonly SynchronizationContext _synchronizationContext;

        [NotNull]
        private readonly ITextToSpeechPlayer _textToSpeechPlayer;

        [NotNull]
        private readonly ITranslationDetailsRepository _translationDetailsRepository;

        [NotNull]
        private readonly ITranslationEntryDeletionRepository _translationEntryDeletionRepository;

        [NotNull]
        private readonly ITranslationEntryRepository _translationEntryRepository;

        [NotNull]
        private readonly IWordImageInfoRepository _wordImageInfoRepository;

        [NotNull]
        private readonly IWordImageSearchIndexRepository _wordImageSearchIndexRepository;

        [NotNull]
        private readonly IWordsTranslator _wordsTranslator;

        public TranslationEntryProcessor(
            [NotNull] ITextToSpeechPlayer textToSpeechPlayer,
            [NotNull] ILog logger,
            [NotNull] ITranslationDetailsCardManager cardManager,
            [NotNull] IMessageHub messageHub,
            [NotNull] ITranslationDetailsRepository translationDetailsRepository,
            [NotNull] IWordsTranslator wordsTranslator,
            [NotNull] ITranslationEntryRepository translationEntryRepository,
            [NotNull] IWordImageInfoRepository wordImageInfoRepository,
            [NotNull] IPrepositionsInfoRepository prepositionsInfoRepository,
            [NotNull] ILearningInfoRepository learningInfoRepository,
            [NotNull] ITranslationEntryDeletionRepository translationEntryDeletionRepository,
            [NotNull] IWordImageSearchIndexRepository wordImageSearchIndexRepository,
            [NotNull] ILanguageManager languageManager,
            [NotNull] Func<ILoadingWindow> loadingWindowFactory,
            [NotNull] SynchronizationContext synchronizationContext)
        {
            _wordImageInfoRepository = wordImageInfoRepository ?? throw new ArgumentNullException(nameof(wordImageInfoRepository));
            _prepositionsInfoRepository = prepositionsInfoRepository ?? throw new ArgumentNullException(nameof(prepositionsInfoRepository));
            _learningInfoRepository = learningInfoRepository ?? throw new ArgumentNullException(nameof(learningInfoRepository));
            _translationEntryDeletionRepository = translationEntryDeletionRepository ?? throw new ArgumentNullException(nameof(translationEntryDeletionRepository));
            _wordImageSearchIndexRepository = wordImageSearchIndexRepository ?? throw new ArgumentNullException(nameof(wordImageSearchIndexRepository));
            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _loadingWindowFactory = loadingWindowFactory ?? throw new ArgumentNullException(nameof(loadingWindowFactory));
            _synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
            _wordsTranslator = wordsTranslator ?? throw new ArgumentNullException(nameof(wordsTranslator));
            _translationEntryRepository = translationEntryRepository ?? throw new ArgumentNullException(nameof(translationEntryRepository));
            _translationDetailsRepository = translationDetailsRepository ?? throw new ArgumentNullException(nameof(translationDetailsRepository));
            _textToSpeechPlayer = textToSpeechPlayer ?? throw new ArgumentNullException(nameof(textToSpeechPlayer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cardManager = cardManager ?? throw new ArgumentNullException(nameof(cardManager));
            _messageHub = messageHub ?? throw new ArgumentNullException(nameof(messageHub));
        }

        public async Task<TranslationInfo> AddOrUpdateTranslationEntryAsync(
            TranslationEntryAdditionInfo translationEntryAdditionInfo,
            CancellationToken cancellationToken,
            IWindow ownerWindow,
            bool needPostProcess,
            ICollection<ManualTranslation> manualTranslations)
        {
            if (translationEntryAdditionInfo == null)
            {
                throw new ArgumentNullException(nameof(translationEntryAdditionInfo));
            }

            _logger.TraceFormat("Adding new word translation for {0}...", translationEntryAdditionInfo);

            IWindow loadingWindow = null;
            _synchronizationContext.Send(
                x =>
                {
                    loadingWindow = _loadingWindowFactory();
                    loadingWindow.Show();
                },
                null);

            try
            {
                if (!manualTranslations?.Any() == true)
                {
                    manualTranslations = null;
                }

                CapitalizeManualTranslations(manualTranslations);

                // This method replaces translation with the actual one
                var translationEntryKey = await GetTranslationKeyAsync(translationEntryAdditionInfo, cancellationToken).ConfigureAwait(false);
                if (translationEntryKey == null)
                {
                    return null;
                }

                var translationResult = await TranslateAsync(translationEntryKey, manualTranslations, cancellationToken).ConfigureAwait(false);
                if (translationResult == null)
                {
                    return null;
                }

                var translationEntry = new TranslationEntry(translationEntryKey)
                {
                    Id = translationEntryKey,
                    ManualTranslations = manualTranslations
                };
                _translationEntryRepository.Upsert(translationEntry);
                var translationDetails = new TranslationDetails(translationResult, translationEntryKey);
                _translationDetailsRepository.Upsert(translationDetails);
                var learningInfo = _learningInfoRepository.GetOrInsert(translationEntryKey);
                var translationInfo = new TranslationInfo(translationEntry, translationDetails, learningInfo);
                if (needPostProcess)
                {
                    //No await here
                    // ReSharper disable once AssignmentIsFullyDiscarded
                    _ = PostProcessWordAsync(ownerWindow, translationInfo, cancellationToken).ConfigureAwait(false);
                }

                _logger.InfoFormat("Processing finished for word {0}", translationEntryKey);
                return translationInfo;
            }
            finally
            {
                _synchronizationContext.Post(x => loadingWindow.Close(), null);
            }
        }

        public void DeleteTranslationEntry(TranslationEntryKey translationEntryKey, bool needDeletionRecord)
        {
            if (translationEntryKey == null)
            {
                throw new ArgumentNullException(nameof(translationEntryKey));
            }

            _logger.TraceFormat("Deleting {0}{1}...", translationEntryKey, needDeletionRecord ? " with creating deletion event" : null);
            _prepositionsInfoRepository.Delete(translationEntryKey);
            _translationDetailsRepository.Delete(translationEntryKey);
            _wordImageInfoRepository.ClearForTranslationEntry(translationEntryKey);
            _wordImageSearchIndexRepository.ClearForTranslationEntry(translationEntryKey);
            _translationEntryRepository.Delete(translationEntryKey);
            _learningInfoRepository.Delete(translationEntryKey);
            if (needDeletionRecord)
            {
                _translationEntryDeletionRepository.Upsert(new TranslationEntryDeletion(translationEntryKey));
            }

            _logger.InfoFormat("Deleted {0}", translationEntryKey);
        }

        public async Task<TranslationDetails> ReloadTranslationDetailsIfNeededAsync(
            TranslationEntryKey translationEntryKey,
            ICollection<ManualTranslation> manualTranslations,
            CancellationToken cancellationToken)
        {
            return (await ReloadTranslationDetailsIfNeededInternalAsync(translationEntryKey, manualTranslations, cancellationToken).ConfigureAwait(false)).TranslationDetails;
        }

        public async Task<TranslationInfo> UpdateManualTranslationsAsync(
            TranslationEntryKey translationEntryKey,
            ICollection<ManualTranslation> manualTranslations,
            CancellationToken cancellationToken)
        {
            _logger.TraceFormat("Updating manual translations for {0}...", translationEntryKey);
            if (translationEntryKey == null)
            {
                throw new ArgumentNullException(nameof(translationEntryKey));
            }

            if (!manualTranslations?.Any() == true)
            {
                manualTranslations = null;
            }

            CapitalizeManualTranslations(manualTranslations);

            var translationEntry = _translationEntryRepository.GetById(translationEntryKey);
            translationEntry.ManualTranslations = manualTranslations;
            _translationEntryRepository.Update(translationEntry);

            var reloadResult = await ReloadTranslationDetailsIfNeededInternalAsync(translationEntryKey, manualTranslations, cancellationToken).ConfigureAwait(false);
            var translationDetails = reloadResult.TranslationDetails;

            DeleteFromPriority(translationEntry, manualTranslations, translationDetails);

            if (reloadResult.AlreadyExists)
            {
                var nonManual = translationDetails.TranslationResult.PartOfSpeechTranslations.Where(x => !x.IsManual);
                translationDetails.TranslationResult.PartOfSpeechTranslations =
                    (manualTranslations != null ? ConcatTranslationsWithManual(translationEntryKey.Text, manualTranslations, nonManual) : nonManual).ToArray();
                _translationDetailsRepository.Update(translationDetails);
            }

            var learningInfo = _learningInfoRepository.GetOrInsert(translationEntryKey);
            return new TranslationInfo(translationEntry, translationDetails, learningInfo);
        }

        private static void CapitalizeManualTranslations([CanBeNull] ICollection<ManualTranslation> manulTranslations)
        {
            if (manulTranslations == null)
            {
                return;
            }

            foreach (var manualTranslation in manulTranslations)
            {
                manualTranslation.Text = manualTranslation.Text.Capitalize();
                manualTranslation.Example = manualTranslation.Example.Capitalize();
                manualTranslation.Meaning = manualTranslation.Meaning.Capitalize();
            }
        }

        [NotNull]
        private static IEnumerable<PartOfSpeechTranslation> ConcatTranslationsWithManual(
            [NotNull] string text,
            [NotNull] ICollection<ManualTranslation> manualTranslations,
            [NotNull] IEnumerable<PartOfSpeechTranslation> partOfSpeechTranslations)
        {
            var groups = manualTranslations.GroupBy(x => x.PartOfSpeech);
            var manualPartOfSpeechTranslations = groups.Select(
                group => new PartOfSpeechTranslation
                {
                    IsManual = true,
                    PartOfSpeech = group.Key,
                    Text = text,
                    TranslationVariants = group.Select(
                            manualTranslation => new TranslationVariant
                            {
                                Text = manualTranslation.Text,
                                PartOfSpeech = manualTranslation.PartOfSpeech,
                                Examples = string.IsNullOrWhiteSpace(manualTranslation.Example)
                                    ? null
                                    : new[]
                                    {
                                        new Example
                                        {
                                            Text = manualTranslation.Example
                                        }
                                    },
                                Meanings = string.IsNullOrWhiteSpace(manualTranslation.Meaning)
                                    ? null
                                    : new[]
                                    {
                                        new Word
                                        {
                                            Text = manualTranslation.Meaning
                                        }
                                    }
                            })
                        .ToArray()
                });
            return partOfSpeechTranslations.Concat(manualPartOfSpeechTranslations);
        }

        private void DeleteFromPriority(
            [NotNull] TranslationEntry translationEntry,
            [CanBeNull] ICollection<ManualTranslation> manualTranslations,
            [NotNull] TranslationDetails translationDetails)
        {
            var remainingManualTranslations = translationDetails.TranslationResult.PartOfSpeechTranslations.Where(x => x.IsManual)
                .SelectMany(partOfSpeechTranslation => partOfSpeechTranslation.TranslationVariants)
                .Cast<BaseWord>();
            var deletedManualTranslations = remainingManualTranslations;
            if (manualTranslations != null)
            {
                deletedManualTranslations = deletedManualTranslations.Except(manualTranslations);
            }

            if (translationEntry.PriorityWords == null)
            {
                return;
            }

            var deleted = false;
            foreach (var deletedManualTranslation in deletedManualTranslations)
            {
                translationEntry.PriorityWords.Remove(deletedManualTranslation);
                deleted = true;
            }

            if (deleted)
            {
                _translationEntryRepository.Update(translationEntry);
            }
        }

        [ItemCanBeNull]
        [NotNull]
        private async Task<TranslationEntryKey> GetTranslationKeyAsync([NotNull] TranslationEntryAdditionInfo translationEntryAdditionInfo, CancellationToken cancellationToken)
        {
            var text = translationEntryAdditionInfo.Text;
            var sourceLanguage = translationEntryAdditionInfo.SourceLanguage;
            var targetLanguage = translationEntryAdditionInfo.TargetLanguage;
            if (string.IsNullOrWhiteSpace(text))
            {
                _messageHub.Publish(Errors.WordIsMissing.ToWarning());
                return null;
            }

            if (sourceLanguage == null || sourceLanguage == Constants.AutoDetectLanguage)
            {
                sourceLanguage = await _languageManager.GetSourceAutoSubstituteAsync(text, cancellationToken).ConfigureAwait(false);
            }

            if (targetLanguage == null || targetLanguage == Constants.AutoDetectLanguage)
            {
                targetLanguage = _languageManager.GetTargetAutoSubstitute(sourceLanguage);
            }
            else
            {
                if (!_languageManager.CheckTargetLanguageIsValid(sourceLanguage, targetLanguage))
                {
                    _messageHub.Publish(string.Format(Errors.InvalidTargetLanguage, sourceLanguage, targetLanguage).ToWarning());
                    return null;
                }
            }

            return new TranslationEntryKey(text, sourceLanguage, targetLanguage);
        }

        private async Task PostProcessWordAsync([CanBeNull] IWindow ownerWindow, [NotNull] TranslationInfo translationInfo, CancellationToken cancellationToken)
        {
            var playTtsTask = _textToSpeechPlayer.PlayTtsAsync(translationInfo.TranslationEntryKey.Text, translationInfo.TranslationEntryKey.SourceLanguage, cancellationToken);
            var showCardTask = _cardManager.ShowCardAsync(translationInfo, ownerWindow);
            _messageHub.Publish(translationInfo.TranslationEntry);
            await Task.WhenAll(playTtsTask, showCardTask).ConfigureAwait(false);
        }

        // ReSharper disable once StyleCop.SA1009
        private async Task<(TranslationDetails TranslationDetails, bool AlreadyExists)> ReloadTranslationDetailsIfNeededInternalAsync(
            TranslationEntryKey translationEntryKey,
            ICollection<ManualTranslation> manualTranslations,
            CancellationToken cancellationToken)
        {
            if (!manualTranslations?.Any() == true)
            {
                manualTranslations = null;
            }

            var translationDetails = _translationDetailsRepository.TryGetById(translationEntryKey);
            if (translationDetails != null)
            {
                return (translationDetails, true);
            }

            CapitalizeManualTranslations(manualTranslations);

            var translationResult = await TranslateAsync(translationEntryKey, manualTranslations, cancellationToken).ConfigureAwait(false);
            if (translationResult == null)
            {
                throw new InvalidOperationException("Empty translation result for the existing translation entry");
            }

            // There are no translation details for this word
            translationDetails = new TranslationDetails(translationResult, translationEntryKey);
            _translationDetailsRepository.Insert(translationDetails);
            return (translationDetails, false);
        }

        [ItemCanBeNull]
        [NotNull]
        private async Task<TranslationResult> TranslateAsync(
            [NotNull] TranslationEntryKey translationEntryKey,
            [CanBeNull] ICollection<ManualTranslation> manualTranslations,
            CancellationToken cancellationToken)
        {
            // Used En as ui language to simplify conversion of common words to the enums
            var translationResult = await _wordsTranslator.GetTranslationAsync(
                    translationEntryKey.SourceLanguage,
                    translationEntryKey.TargetLanguage,
                    translationEntryKey.Text,
                    Constants.EnLanguage,
                    cancellationToken)
                .ConfigureAwait(false);

            if (translationResult == null)
            {
                // Manual translations should not be the only translations if there is an error (network error) - the user should retry to get a proper translation
                return null;
            }

            if (translationResult.PartOfSpeechTranslations.Any())
            {
                // replace the original text with the corrected one
                translationEntryKey.Text = translationResult.PartOfSpeechTranslations.First().Text;
                _logger.TraceFormat("Received translation for {0}", translationEntryKey);
            }
            else
            {
                if (manualTranslations == null)
                {
                    _messageHub.Publish(string.Format(Errors.NoTranslations, translationEntryKey).ToWarning());
                    return null;
                }

                translationEntryKey.Text = translationEntryKey.Text.Capitalize();
            }

            if (manualTranslations != null)
            {
                translationResult.PartOfSpeechTranslations =
                    ConcatTranslationsWithManual(translationEntryKey.Text, manualTranslations, translationResult.PartOfSpeechTranslations).ToArray();
            }

            return translationResult;
        }
    }
}