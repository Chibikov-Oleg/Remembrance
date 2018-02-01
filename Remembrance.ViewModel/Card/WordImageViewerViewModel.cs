using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Common.Logging;
using JetBrains.Annotations;
using PropertyChanged;
using Remembrance.Contracts.DAL.Local;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.ImageSearch;
using Remembrance.Contracts.ImageSearch.Data;
using Remembrance.ViewModel.Translation;
using Scar.Common.Async;
using Scar.Common.Events;
using Scar.Common.WPF.Commands;

namespace Remembrance.ViewModel.Card
{
    [UsedImplicitly]
    [AddINotifyPropertyChangedInterface]
    public sealed class WordImageViewerViewModel : IDisposable
    {
        [NotNull]
        private readonly ICancellationTokenSourceProvider _cancellationTokenSourceProvider;

        [NotNull]
        private readonly IImageDownloader _imageDownloader;

        [NotNull]
        private readonly IImageSearcher _imageSearcher;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly PriorityWordViewModel _word;

        [NotNull]
        private readonly IWordImagesInfoRepository _wordImagesInfoRepository;

        [NotNull]
        private string _parentText;

        [NotNull]
        private WordKey _wordKey;

        public WordImageViewerViewModel(
            [NotNull] ILog logger,
            [NotNull] PriorityWordViewModel word,
            [NotNull] IWordImagesInfoRepository wordImagesInfoRepository,
            [NotNull] ICancellationTokenSourceProvider cancellationTokenSourceProvider,
            [NotNull] IImageDownloader imageDownloader,
            [NotNull] IImageSearcher imageSearcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _word = word ?? throw new ArgumentNullException(nameof(word));
            _wordImagesInfoRepository = wordImagesInfoRepository ?? throw new ArgumentNullException(nameof(wordImagesInfoRepository));
            _cancellationTokenSourceProvider = cancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(cancellationTokenSourceProvider));
            _imageDownloader = imageDownloader ?? throw new ArgumentNullException(nameof(imageDownloader));
            _imageSearcher = imageSearcher ?? throw new ArgumentNullException(nameof(imageSearcher));
            _word.TranslationEntryKeySet += WordTranslationEntryKeySet;
            _word.ParentTextSet += Word_ParentTextSet;
            SetNextImageCommand = new CorrelationCommand(SetNextImage);
            SetPreviousImageCommand = new CorrelationCommand(SetPreviousImage);
        }

        public int SearchIndex { get; private set; }

        [CanBeNull]
        public string ImageName { get; private set; }

        [CanBeNull]
        public string ImageUrl { get; private set; }

        [DependsOn(nameof(ImageName), nameof(ImageUrl), nameof(SearchIndex))]
        [CanBeNull]
        public string ToolTip => $"{SearchIndex + 1}. {ImageName} ({ImageUrl})";

        public bool IsLoading { get; private set; } = true;

        [NotNull]
        private string SearchText => _word.WordText + " " + _parentText;

        [CanBeNull]
        public BitmapSource Image { get; private set; }

        [NotNull]
        public ICommand SetPreviousImageCommand { get; }

        [NotNull]
        public ICommand SetNextImageCommand { get; }

        public void Dispose()
        {
            _word.TranslationEntryKeySet -= WordTranslationEntryKeySet;
            _word.ParentTextSet -= Word_ParentTextSet;
        }

        private async void WordTranslationEntryKeySet(object sender, [NotNull] EventArgs<TranslationEntryKey> e)
        {
            _wordKey = new WordKey(e.Parameter, _word);

            var wordImageInfo = _wordImagesInfoRepository.TryGetById(_wordKey);
            if (wordImageInfo != null)
            {
                await UpdateImageViewAsync(wordImageInfo).ConfigureAwait(false);
            }
            else
            {
                await SetWordImageAsync().ConfigureAwait(false);
            }
        }

        private void Word_ParentTextSet(object sender, [NotNull] EventArgs<string> e)
        {
            _parentText = e.Parameter;
        }

        private async Task SetWordImageAsync()
        {
            IsLoading = true;
            Image = null;
            WordImageInfo wordImageInfo = null;
            _logger.TraceFormat("Setting new image for {0} at search index {1}...", _wordKey, SearchIndex);
            _wordImagesInfoRepository.Delete(_wordKey);
            await _cancellationTokenSourceProvider.ExecuteAsyncOperation(
                    async cancellationToken =>
                    {
                        var imagesUrls = await _imageSearcher.SearchImagesAsync(SearchText, cancellationToken, SearchIndex).ConfigureAwait(false);
                        if (imagesUrls != null)
                        {
                            var imageDownloadTasks = imagesUrls.Select(
                                async image => new ImageInfoWithBitmap
                                {
                                    ImageBitmap = null, //images[i++],
                                    ThumbnailBitmap = await _imageDownloader.DownloadImageAsync(image.ThumbnailUrl, cancellationToken).ConfigureAwait(false),
                                    ImageInfo = image
                                });
                            var imageInfoWithBitmaps = await Task.WhenAll(imageDownloadTasks).ConfigureAwait(false);

                            wordImageInfo = new WordImageInfo(_wordKey, SearchIndex, imageInfoWithBitmaps.SingleOrDefault());
                        }
                    })
                .ConfigureAwait(false);
            if (wordImageInfo == null)
            {
                IsLoading = false;
                _logger.WarnFormat("Image for {0} at search index {1} was not set", _wordKey, SearchIndex);
                return;
            }

            _wordImagesInfoRepository.Upsert(wordImageInfo);
            _logger.InfoFormat("Image for {0} at search index {1} was saved", _wordKey, SearchIndex);
            await UpdateImageViewAsync(wordImageInfo).ConfigureAwait(false);
        }

        private async Task UpdateImageViewAsync([NotNull] WordImageInfo wordImageInfo)
        {
            SearchIndex = wordImageInfo.SearchIndex;
            var imageBytes = wordImageInfo.Image?.ThumbnailBitmap;
            if (imageBytes == null || imageBytes.Length == 0)
            {
                await SetNextImageAsync(true).ConfigureAwait(false);
                return;
            }

            ImageName = wordImageInfo.Image.ImageInfo.Name;
            ImageUrl = wordImageInfo.Image.ImageInfo.Url;
            Image = LoadImage(imageBytes);
            IsLoading = false;
        }

        [CanBeNull]
        private static BitmapImage LoadImage([NotNull] byte[] imageBytes)
        {
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageBytes))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }

        private async void SetPreviousImage()
        {
            await SetNextImageAsync(false).ConfigureAwait(false);
        }

        //todo: disable prev image if curindex =0
        //todo: show empty image
        //TODO: reload unloaded image button
        //todo:cancellationtolen for next prev (like in PhotoReviewer)
        //TODO: separate image processor
        private async void SetNextImage()
        {
            await SetNextImageAsync(true).ConfigureAwait(false);
        }

        private async Task SetNextImageAsync(bool increase)
        {
            _logger.InfoFormat(
                "Setting {1} image for {0}...",
                this,
                increase
                    ? "next"
                    : "previous");
            if (increase)
            {
                SearchIndex++;
            }
            else
            {
                if (SearchIndex != 0)
                {
                    SearchIndex--;
                }
            }

            await SetWordImageAsync().ConfigureAwait(false);
        }

        public override string ToString()
        {
            return _word.ToString();
        }
    }
}