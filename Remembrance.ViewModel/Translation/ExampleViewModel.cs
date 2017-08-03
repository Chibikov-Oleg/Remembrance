﻿using JetBrains.Annotations;

namespace Remembrance.ViewModel.Translation
{
    public sealed class ExampleViewModel : TextEntryViewModel
    {
        [CanBeNull]
        public TextEntryViewModel[] Translations
        {
            get;
            [UsedImplicitly]
            set;
        }
    }
}