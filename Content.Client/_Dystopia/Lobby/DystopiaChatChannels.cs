// SPDX-License-Identifier: AGPL-3.0-or-later
// Dystopia: лобби-терминал — вкладки каналов чата вместо ванильного переключателя (кнопка с выпадающей полосой
// вылезала за рамку). Вкладки — те каналы, в которые сейчас можно писать; выбранная залита. Выбор идёт через
// ванильный переключатель (он спрятан), поэтому Tab-перебор и префиксы сообщений работают как раньше.

using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Dystopia.Lobby;

public sealed class DystopiaChatChannels : BoxContainer
{
    private readonly Dictionary<ChatSelectChannel, DystopiaTerminalButton> _tabs = new();
    private ChatUIController? _controller;
    private ChannelSelectorButton? _selector;

    public DystopiaChatChannels()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 6;
        HorizontalExpand = true;
    }

    /// <summary>Привязать к чату: прячет его переключатель канала и фильтр, вкладки ведут его переключатель.</summary>
    public void Attach(ChatBox chat)
    {
        _selector = chat.ChatInput.ChannelSelector;
        _selector.Visible = false;
        chat.ChatInput.FilterButton.Visible = false;
        _controller = UserInterfaceManager.GetUIController<ChatUIController>();
        if (IsInsideTree)
            Subscribe();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        Subscribe();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_controller != null)
            _controller.SelectableChannelsChanged -= Rebuild;

        if (_selector != null)
            _selector.OnChannelSelect -= OnSelected;
    }

    private void Subscribe()
    {
        if (_controller == null || _selector == null)
            return;

        _controller.SelectableChannelsChanged -= Rebuild;
        _controller.SelectableChannelsChanged += Rebuild;
        _selector.OnChannelSelect -= OnSelected;
        _selector.OnChannelSelect += OnSelected;
        Rebuild(_controller.SelectableChannels);
    }

    private void Rebuild(ChatSelectChannel channels)
    {
        RemoveAllChildren();
        foreach (var channel in ChannelSelectorPopup.ChannelSelectorOrder)
        {
            if ((channels & channel) == 0)
                continue;

            if (!_tabs.TryGetValue(channel, out var tab))
            {
                tab = new DystopiaTerminalButton
                {
                    Text = ChannelSelectorButton.ChannelSelectorName(channel).ToUpperInvariant(),
                };

                var prefix = ChatUIController.ChannelPrefixes[channel];
                if (prefix != default)
                    tab.ToolTip = Loc.GetString("dystopia-lobby-chat-prefix", ("prefix", prefix.ToString()));

                var picked = channel;
                tab.OnPressed += _ => _selector?.Select(picked);
                _tabs[channel] = tab;
            }

            AddChild(tab);
        }

        if (_selector != null)
            OnSelected(_selector.SelectedChannel);
    }

    private void OnSelected(ChatSelectChannel channel)
    {
        foreach (var (key, tab) in _tabs)
        {
            tab.Selected = key == channel;
        }
    }
}
