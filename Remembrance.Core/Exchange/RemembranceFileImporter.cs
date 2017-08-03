﻿using System.Collections.Generic;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Remembrance.Card.Management.CardManagement.Data;
using Remembrance.Contracts.CardManagement;
using Remembrance.Contracts.DAL;
using Remembrance.Contracts.DAL.Model;

namespace Remembrance.Card.Management.Exchange
{
    [UsedImplicitly]
    internal sealed class RemembranceFileImporter : BaseFileImporter<RemembranceExchangeEntry>
    {
        public RemembranceFileImporter(
            [NotNull] ITranslationEntryRepository translationEntryRepository,
            [NotNull] ILog logger,
            [NotNull] IWordsProcessor wordsProcessor,
            [NotNull] IMessenger messenger,
            [NotNull] ITranslationDetailsRepository translationDetailsRepository)
            : base(translationEntryRepository, logger, wordsProcessor, messenger, translationDetailsRepository)
        {
        }

        protected override TranslationEntryKey GetKey(RemembranceExchangeEntry exchangeEntry)
        {
            return exchangeEntry.TranslationEntry.Key;
        }

        protected override ICollection<string> GetPriorityTranslations(RemembranceExchangeEntry exchangeEntry)
        {
            return exchangeEntry.PriorityTranslations;
        }
    }
}