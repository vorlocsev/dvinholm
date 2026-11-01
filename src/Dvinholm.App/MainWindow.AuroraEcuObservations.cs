using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Dvinholm.App;

/// <summary>
/// A numbered passive-response tile, never a module name or physical position.
/// The tile index is an observation list index, not an ECU address or location.
/// </summary>
internal sealed record AuroraEcuObservationTile(int Index, string Label, string Hint, bool Selectable);

internal static class AuroraEcuObservationProjection
{
    internal const int MaxVisibleTiles = 20;

    internal static IReadOnlyList<AuroraEcuObservationTile> Project(
        IReadOnlyList<UdsEcuCandidate> candidates, bool complete)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var tiles = new List<AuroraEcuObservationTile>(
            Math.Min(candidates.Count, MaxVisibleTiles));
        for (var index = 0; index < Math.Min(candidates.Count, MaxVisibleTiles); index++)
        {
            var candidate = candidates[index]
                ?? throw new ArgumentException("Null passive observation.", nameof(candidates));
            if (candidate.ResponseCount < 0 ||
                candidate.ResponseId > (candidate.IsExtended ? 0x1FFFFFFFu : 0x7FFu))
                throw new ArgumentOutOfRangeException(nameof(candidates),
                    "Passive response is not a valid CAN observation.");

            var hex = UdsPassiveEcuListPresentation.Hex(candidate.ResponseId, candidate.IsExtended);
            tiles.Add(new AuroraEcuObservationTile(
                index,
                $"#{index + 1} · RX 0x{hex}",
                $"{(candidate.IsExtended ? "29" : "11")}-bit · " +
                $"{candidate.ResponseCount} observed frame(s). " +
                "Index only; ECU identity, physical position and TX address unverified.",
                complete));
        }
        return tiles.AsReadOnly();
    }
}

public sealed partial class MainWindow
{
    private readonly List<Button> _ecuObservationButtons = new();

    private void ClearAuroraEcuObservationRail()
    {
        _ecuObservationButtons.Clear();
        if (EcuObservationRail is not null)
            EcuObservationRail.Children.Clear();
        if (EcuObservationRailSummary is not null)
            EcuObservationRailSummary.Text =
                "Нет завершённого пассивного наблюдения. На изображении нет отметок физических ECU.";
    }

    private void ShowAuroraEcuObservationRail(
        IReadOnlyList<UdsEcuCandidate> candidates, bool complete)
    {
        if (EcuObservationRail is null || EcuObservationRailSummary is null)
            return;
        ClearAuroraEcuObservationRail();

        var tiles = AuroraEcuObservationProjection.Project(candidates, complete);
        foreach (var tile in tiles)
        {
            var button = new Button
            {
                Content = tile.Label,
                Tag = tile.Index,
                IsEnabled = tile.Selectable,
                MinWidth = 119,
                Margin = new Thickness(0, 0, 7, 0),
                Padding = new Thickness(10, 7, 10, 7),
                FontSize = 12
            };
            ToolTipService.SetToolTip(button, tile.Hint);
            button.Click += EcuObservationTile_Click;
            EcuObservationRail.Children.Add(button);
            _ecuObservationButtons.Add(button);
        }

        EcuObservationRailSummary.Text = !complete
            ? $"Неполный поиск: {candidates.Count} наблюдений, выбор заблокирован. " +
              "Позиции ECU на машине не установлены."
            : candidates.Count == 0
                ? "Ответов не обнаружено. Это не означает, что в машине нет ECU."
                : $"Пассивных ответов: {candidates.Count}, плиток: {tiles.Count}. " +
                  "Номера показывают порядок ответов, а НЕ расположение блоков.";
    }

    private void EcuObservationTile_Click(object sender, RoutedEventArgs e)
    {
        // All clicks stay in the same existing selection path. The handler
        // does not connect, authenticate, infer a TX address or send CAN.
        if (!_ecuScanComplete || sender is not Button { Tag: int index } ||
            index < 0 || index >= _ecus.Count || !EcusView.IsEnabled)
            return;

        if (EcusView.SelectedIndex != index)
            EcusView.SelectedIndex = index;
        else
            HighlightAuroraEcuObservation(index);
    }

    private void HighlightAuroraEcuObservation(int selectedIndex)
    {
        for (var i = 0; i < _ecuObservationButtons.Count; i++)
        {
            var selected = i == selectedIndex && _ecuScanComplete;
            _ecuObservationButtons[i].BorderThickness = new Thickness(selected ? 2 : 1);
            _ecuObservationButtons[i].BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(255, 43, 216, 237)
                : Color.FromArgb(255, 54, 95, 126));
            _ecuObservationButtons[i].Background = new SolidColorBrush(selected
                ? Color.FromArgb(255, 14, 99, 135)
                : Color.FromArgb(255, 16, 40, 65));
        }
    }
}
