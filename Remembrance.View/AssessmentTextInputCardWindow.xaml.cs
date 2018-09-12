using System.Windows;
using JetBrains.Annotations;
using Remembrance.Contracts.View.Card;
using Remembrance.ViewModel;

namespace Remembrance.View
{
    /// <summary>
    /// The assessment text input card window.
    /// </summary>
    [UsedImplicitly]
    internal sealed partial class AssessmentTextInputCardWindow : IAssessmentTextInputCardWindow
    {
        public AssessmentTextInputCardWindow([NotNull] AssessmentTextInputCardViewModel viewModel, [CanBeNull] Window ownerWindow = null)
        {
            DataContext = viewModel;
            Owner = ownerWindow;
            InitializeComponent();
        }
    }
}