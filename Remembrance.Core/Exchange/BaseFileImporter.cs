﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Remembrance.Card.Management.CardManagement.Data;
using Remembrance.Contracts;
using Remembrance.Contracts.CardManagement;
using Remembrance.Contracts.CardManagement.Data;
using Remembrance.Contracts.DAL;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.Translate.Data.WordsTranslator;
using Remembrance.Resources;
using Scar.Common;
using Scar.Common.Events;
using Scar.Common.Exceptions;

// ReSharper disable MemberCanBePrivate.Global

namespace Remembrance.Card.Management.Exchange
{
    [UsedImplicitly]
    internal abstract class BaseFileImporter<T> : IFileImporter
        where T : IExchangeEntry
    {
        private const int MaxBlockSize = 25;

        [NotNull]
        protected readonly ILog Logger;

        [NotNull]
        protected readonly IMessenger Messenger;

        [NotNull]
        protected readonly ITranslationDetailsRepository TranslationDetailsRepository;

        [NotNull]
        protected readonly ITranslationEntryRepository TranslationEntryRepository;

        [NotNull]
        protected readonly IWordsProcessor WordsProcessor;

        [NotNull]
        private readonly IEqualityComparer<IWord> _wordsEqualityComparer;

        protected BaseFileImporter(
            [NotNull] ITranslationEntryRepository translationEntryRepository,
            [NotNull] ILog logger,
            [NotNull] IWordsProcessor wordsProcessor,
            [NotNull] IMessenger messenger,
            [NotNull] ITranslationDetailsRepository translationDetailsRepository,
            [NotNull] IEqualityComparer<IWord> wordsEqualityComparer)
        {
            _wordsEqualityComparer = wordsEqualityComparer ?? throw new ArgumentNullException(nameof(wordsEqualityComparer));
            TranslationEntryRepository = translationEntryRepository ?? throw new ArgumentNullException(nameof(translationEntryRepository));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            WordsProcessor = wordsProcessor ?? throw new ArgumentNullException(nameof(wordsProcessor));
            Messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            TranslationDetailsRepository = translationDetailsRepository ?? throw new ArgumentNullException(nameof(translationDetailsRepository));
        }

        public event EventHandler<ProgressEventArgs> Progress;

        public async Task<ExchangeResult> ImportAsync(string fileName, CancellationToken token)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            string[] errors = null;
            var e = new List<string>();
            var count = 0;
            T[] deserialized;

            return await Task.Run(
                () =>
                {
                    try
                    {
                        var file = File.ReadAllText(fileName);
                        deserialized = JsonConvert.DeserializeObject<T[]>(file);
                    }
                    catch (IOException ex)
                    {
                        Logger.Warn("Cannot load file from disk", ex);
                        return new ExchangeResult(false, null, count);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Cannot deserialize object", ex);
                        return new ExchangeResult(false, null, count);
                    }

                    var existing = TranslationEntryRepository.GetAll();
                    var existingKeys = new HashSet<TranslationEntryKey>(existing.Select(x => x.Key));

                    deserialized.RunByBlocks(
                        MaxBlockSize,
                        (block, index, blocksCount) =>
                        {
                            token.ThrowIfCancellationRequested();
                            Logger.Trace($"Processing block {index} out of {blocksCount} ({block.Length} files)...");
                            var blockResult = new List<TranslationInfo>(block.Length);
                            foreach (var exchangeEntry in block)
                            {
                                token.ThrowIfCancellationRequested();
                                Logger.Info($"Importing from {exchangeEntry.Text}...");
                                TranslationInfo translationInfo;
                                try
                                {
                                    var key = GetKey(exchangeEntry);
                                    if (existingKeys.Contains(key))
                                        continue;

                                    translationInfo = WordsProcessor.AddWord(key.Text, key.SourceLanguage, key.TargetLanguage);
                                    blockResult.Add(translationInfo);
                                }
                                catch (LocalizableException ex)
                                {
                                    Logger.Warn($"Cannot translate {exchangeEntry.Text}. The word is skipped", ex);
                                    e.Add(exchangeEntry.Text);
                                    continue;
                                }

                                var priorityTranslations = GetPriorityTranslations(exchangeEntry);
                                if (priorityTranslations != null)
                                {
                                    foreach (var translationVariant in translationInfo.TranslationDetails.TranslationResult.PartOfSpeechTranslations.SelectMany(
                                        partOfSpeechTranslation => partOfSpeechTranslation.TranslationVariants))
                                    {
                                        AddOrRemoveTranslation(priorityTranslations, translationVariant, translationInfo.TranslationEntry.Translations);
                                        if (translationVariant.Synonyms == null)
                                            continue;

                                        foreach (var synonym in translationVariant.Synonyms)
                                            AddOrRemoveTranslation(priorityTranslations, synonym, translationInfo.TranslationEntry.Translations);
                                    }

                                    if (!translationInfo.TranslationEntry.Translations.Any())
                                        translationInfo.TranslationEntry.Translations = translationInfo.TranslationDetails.TranslationResult.GetDefaultWords();
                                    TranslationDetailsRepository.Save(translationInfo.TranslationDetails);
                                }

                                count++;
                            }

                            OnProgress(index + 1, blocksCount);
                            if (blockResult.Any())
                                Messenger.Send(blockResult.ToArray(), MessengerTokens.TranslationInfoBatchToken);
                            return true;
                        });

                    if (e.Any())
                        errors = e.ToArray();
                    return new ExchangeResult(true, errors, count);
                },
                token);
        }

        private void AddOrRemoveTranslation([NotNull] ICollection<ExchangeWord> priorityTranslations, [NotNull] PriorityWord word, [NotNull] ICollection<PriorityWord> translations)
        {
            if (priorityTranslations.Contains(word, _wordsEqualityComparer))
            {
                word.IsPriority = true;
                if (translations.All(x => x.Text != word.Text))
                    translations.Add(word);
            }
            else
            {
                var matchingToRemove = translations.Where(x => _wordsEqualityComparer.Equals(x, word)).ToList();
                foreach (var toRemove in matchingToRemove)
                    translations.Remove(toRemove);
            }
        }

        [NotNull]
        protected abstract TranslationEntryKey GetKey([NotNull] T exchangeEntry);

        [CanBeNull]
        protected abstract ICollection<ExchangeWord> GetPriorityTranslations([NotNull] T exchangeEntry);

        private void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }
    }
}