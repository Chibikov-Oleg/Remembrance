﻿using JetBrains.Annotations;
using Remembrance.DAL.Contracts.Model;
using Scar.Common.WPF.View.Contracts;

namespace Remembrance.Card.Management.Contracts
{
    public interface IWordsProcessor
    {
        [NotNull]
        TranslationInfo AddWord([CanBeNull] string text, [CanBeNull] string sourceLanguage = null, [CanBeNull] string targetLanguage = null, [CanBeNull] object id = null);

        bool ChangeWord([NotNull] object id, [NotNull] string text, [NotNull] string sourceLanguage, [NotNull] string targetLanguage, IWindow ownerWindow = null);

        [NotNull]
        string GetDefaultTargetLanguage([NotNull] string sourceLanguage);

        bool ProcessNewWord([NotNull] string text, [CanBeNull] string sourceLanguage = null, [CanBeNull] string targetLanguage = null, IWindow ownerWindow = null);

        [NotNull]
        TranslationInfo ReloadTranslationDetailsIfNeeded([NotNull] TranslationEntry translationEntry);
    }
}