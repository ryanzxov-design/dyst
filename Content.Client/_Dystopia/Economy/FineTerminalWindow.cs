using System.Linq;
using System.Numerics;
using Content.Shared._Dystopia.Economy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Dystopia.Economy;

/// <summary>
/// Окно штрафного терминала: выбор статьи Свода законов, сумма, пояснение.
/// После подготовки штрафа терминал применяется на нарушителе.
/// </summary>
public sealed class FineTerminalWindow : DefaultWindow
{
    public event Action<int, int, string>? OnPrepare;
    public event Action? OnClear;

    private static readonly Color AccentColor = Color.FromHex("#5B8FD9");
    private static readonly Color DimColor = Color.FromHex("#8A8A8A");
    private static readonly Color WarnColor = Color.FromHex("#E05A4F");

    private readonly OptionButton _lawSelect;
    private readonly RichTextLabel _sanction;
    private readonly LineEdit _amount;
    private readonly LineEdit _reason;
    private readonly Label _pending;
    private readonly Button _clear;

    private List<FineTerminalLawEntry> _laws = new();
    private string _lawsSignature = string.Empty;
    private int _selectedLawId = -1;

    public FineTerminalWindow()
    {
        Title = Loc.GetString("dystopia-fine-terminal-title");
        MinSize = new Vector2(460, 320);
        SetSize = new Vector2(500, 340);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        Contents.AddChild(root);

        root.AddChild(new Label { Text = Loc.GetString("dystopia-fine-terminal-article"), FontColorOverride = AccentColor });
        _lawSelect = new OptionButton { HorizontalExpand = true };
        _lawSelect.OnItemSelected += args =>
        {
            args.Button.SelectId(args.Id);
            _selectedLawId = args.Id;
            UpdateSanction();
        };
        root.AddChild(_lawSelect);

        _sanction = new RichTextLabel { HorizontalExpand = true };
        root.AddChild(_sanction);

        _amount = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-fine-terminal-amount") };
        _reason = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dystopia-fine-terminal-reason-placeholder") };
        root.AddChild(_amount);
        root.AddChild(_reason);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var prepare = new Button { Text = Loc.GetString("dystopia-fine-terminal-prepare"), HorizontalExpand = true };
        prepare.OnPressed += _ =>
        {
            if (!int.TryParse(_amount.Text.Trim(), out var amount) || amount <= 0)
                return;

            OnPrepare?.Invoke(_selectedLawId, amount, _reason.Text);
        };
        _clear = new Button { Text = Loc.GetString("dystopia-fine-terminal-clear") };
        _clear.OnPressed += _ => OnClear?.Invoke();
        buttons.AddChild(prepare);
        buttons.AddChild(_clear);
        root.AddChild(buttons);

        _pending = new Label { Margin = new Thickness(0, 6, 0, 0) };
        root.AddChild(_pending);
        root.AddChild(new Label { Text = Loc.GetString("dystopia-fine-terminal-hint"), FontColorOverride = DimColor });
    }

    private void UpdateSanction()
    {
        var law = _laws.FirstOrDefault(l => l.Id == _selectedLawId);
        var text = law == null || law.Sanction.Length == 0
            ? string.Empty
            : Loc.GetString("dystopia-laws-article-sanction", ("sanction", law.Sanction));
        _sanction.SetMessage(FormattedMessage.FromUnformatted(text), DimColor);
    }

    public void UpdateState(FineTerminalUiState state)
    {
        var signature = string.Join(";", state.Laws.Select(l => $"{l.Id}:{l.Number}:{l.Title}"));
        if (signature != _lawsSignature)
        {
            _lawsSignature = signature;
            _laws = state.Laws;

            _lawSelect.Clear();
            _lawSelect.AddItem(Loc.GetString("dystopia-fine-terminal-no-article"), -1);
            foreach (var law in _laws)
            {
                _lawSelect.AddItem(Loc.GetString("dystopia-laws-article-header", ("number", law.Number), ("title", law.Title)), law.Id);
            }

            if (_selectedLawId != -1 && _laws.All(l => l.Id != _selectedLawId))
                _selectedLawId = -1;
            _lawSelect.SelectId(_selectedLawId);
        }
        else
        {
            _laws = state.Laws;
        }

        UpdateSanction();

        if (state.PendingAmount > 0)
        {
            var article = state.PendingArticle.Length > 0 ? state.PendingArticle : Loc.GetString("dystopia-fine-terminal-no-article");
            _pending.Text = Loc.GetString("dystopia-fine-terminal-pending", ("amount", state.PendingAmount), ("article", article));
            _pending.FontColorOverride = WarnColor;
            _clear.Disabled = false;
        }
        else
        {
            _pending.Text = Loc.GetString("dystopia-fine-terminal-not-prepared");
            _pending.FontColorOverride = DimColor;
            _clear.Disabled = true;
        }
    }
}
