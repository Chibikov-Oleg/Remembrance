using System;
using JetBrains.Annotations;
using Remembrance.Contracts.ImageSearch.Data;
using Scar.Common.DAL.Model;

namespace Remembrance.Contracts.DAL.Model
{
    public sealed class WordImageInfo : Entity<WordKey>
    {
        [UsedImplicitly]
        public WordImageInfo()
        {
        }

        public WordImageInfo([NotNull] WordKey wordKey, int searchIndex, [CanBeNull] ImageInfoWithBitmap image)
        {
            Id = wordKey ?? throw new ArgumentNullException(nameof(wordKey));
            SearchIndex = searchIndex;
            Image = image;
        }

        public int SearchIndex { get; set; }

        [CanBeNull]
        public ImageInfoWithBitmap Image { get; set; }

        public override string ToString()
        {
            return $"Image for {Id})";
        }
    }
}