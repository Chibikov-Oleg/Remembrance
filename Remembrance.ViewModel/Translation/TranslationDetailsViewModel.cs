﻿using System;
using JetBrains.Annotations;
using PropertyChanged;

namespace Remembrance.ViewModel.Translation
{
    [AddINotifyPropertyChangedInterface]
    public sealed class TranslationDetailsViewModel
    {
        public TranslationDetailsViewModel([NotNull] TranslationResultViewModel translationResult)
        {
            TranslationResult = translationResult ?? throw new ArgumentNullException(nameof(translationResult));
        }

        [UsedImplicitly]
        [DoNotNotify]
        public object Id { get; set; }

        [DoNotNotify]
        public object TranslationEntryId
        {
            get;
            [UsedImplicitly]
            set;
        }

        [NotNull]
        public TranslationResultViewModel TranslationResult { get; set; }
    }
}