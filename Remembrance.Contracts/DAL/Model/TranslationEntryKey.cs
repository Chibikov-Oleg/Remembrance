using System;
using JetBrains.Annotations;

namespace Remembrance.Contracts.DAL.Model
{
    public class TranslationEntryKey : IEquatable<TranslationEntryKey>
    {
        [UsedImplicitly]
        public TranslationEntryKey()
        {
        }

        public TranslationEntryKey([NotNull] string text, [NotNull] string sourceLanguage, [NotNull] string targetLanguage)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            SourceLanguage = sourceLanguage ?? throw new ArgumentNullException(nameof(sourceLanguage));
            TargetLanguage = targetLanguage ?? throw new ArgumentNullException(nameof(targetLanguage));
        }

        [NotNull]
        public string SourceLanguage { get; set; }

        [NotNull]
        public string TargetLanguage { get; set; }

        public string Text { get; set; }

        public bool Equals(TranslationEntryKey other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Text == other.Text && SourceLanguage == other.SourceLanguage && TargetLanguage == other.TargetLanguage;
        }

        public static bool operator ==([CanBeNull] TranslationEntryKey obj1, [CanBeNull] TranslationEntryKey obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            return obj1?.Equals(obj2) == true;
        }

        // this is second one '!='
        public static bool operator !=([CanBeNull] TranslationEntryKey obj1, [CanBeNull] TranslationEntryKey obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            return obj is TranslationEntryKey key && Equals(key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Text.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceLanguage.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetLanguage.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{Text} [{SourceLanguage}->{TargetLanguage}]";
        }
    }
}