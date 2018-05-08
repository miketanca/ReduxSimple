﻿using ReduxSimple.Samples.Extensions;
using SuccincT.Options;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using HistoryStore = SuccincT.Unions.Union<ReduxSimple.Samples.Counter.CounterStore, ReduxSimple.Samples.TicTacToe.TicTacToeStore, ReduxSimple.Samples.TodoList.TodoListStore, ReduxSimple.Samples.Pokedex.PokedexStore>;

namespace ReduxSimple.Samples.Components
{
    public sealed partial class HistoryComponent : UserControl
    {
        public HistoryStore Store
        {
            get { return (HistoryStore)GetValue(StoreProperty); }
            set { SetValue(StoreProperty, value); }
        }

        public static readonly DependencyProperty StoreProperty =
            DependencyProperty.Register(nameof(Store), typeof(HistoryStore), typeof(HistoryComponent), new PropertyMetadata(null, StoreChanged));

        private static void StoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HistoryComponent component)
            {
                component.Initialize();
            }
        }

        private class HistoryComponentState
        {
            public ImmutableList<object> CurrentActions { get; set; } = ImmutableList<object>.Empty;
            public ImmutableList<object> FutureActions { get; set; } = ImmutableList<object>.Empty;
            public int MaxPosition { get; set; } = 0;
            public int CurrentPosition { get; set; } = 0;
            public bool PlaySessionActive { get; set; } = false;
        }

        private class HistoryComponentStore : ReduxStore<HistoryComponentState>
        {
            protected override HistoryComponentState Reduce(HistoryComponentState state, object action)
            {
                if (action is GoBackAction)
                {
                    var lastAction = state.CurrentActions.Last();

                    return new HistoryComponentState
                    {
                        CurrentActions = state.CurrentActions.Remove(lastAction),
                        FutureActions = state.FutureActions.Add(lastAction),
                        MaxPosition = state.MaxPosition,
                        CurrentPosition = state.CurrentPosition - 1,
                        PlaySessionActive = state.PlaySessionActive
                    };
                }
                if (action is GoForwardAction goForwardAction)
                {
                    var futureActionOption = state.FutureActions.TryLast();

                    if (futureActionOption.HasValue && futureActionOption.Value == goForwardAction.Action)
                    {
                        // Continue on existing timeline
                        var futureAction = futureActionOption.Value;
                        return new HistoryComponentState
                        {
                            CurrentActions = state.CurrentActions.Add(futureAction),
                            FutureActions = state.FutureActions.Remove(futureAction),
                            MaxPosition = state.MaxPosition,
                            CurrentPosition = state.CurrentPosition + 1,
                            PlaySessionActive = state.PlaySessionActive
                        };
                    }
                    else
                    {
                        // Create a new timeline
                        return new HistoryComponentState
                        {
                            CurrentActions = state.CurrentActions.Add(goForwardAction.Action),
                            FutureActions = ImmutableList<object>.Empty,
                            MaxPosition = state.CurrentActions.Count + 1,
                            CurrentPosition = state.CurrentPosition + 1,
                            PlaySessionActive = state.PlaySessionActive
                        };
                    }
                }
                if (action is ResetAction)
                {
                    return new HistoryComponentState
                    {
                        CurrentActions = ImmutableList<object>.Empty,
                        FutureActions = ImmutableList<object>.Empty,
                        MaxPosition = 0,
                        CurrentPosition = 0,
                        PlaySessionActive = state.PlaySessionActive
                    };
                }
                if (action is TogglePlayPauseAction)
                {
                    return new HistoryComponentState
                    {
                        CurrentActions = state.CurrentActions,
                        FutureActions = state.FutureActions,
                        MaxPosition = state.MaxPosition,
                        CurrentPosition = state.CurrentPosition,
                        PlaySessionActive = !state.PlaySessionActive
                    };
                }
                return base.Reduce(state, action);
            }
        }

        private class GoBackAction { }
        private class GoForwardAction
        {
            public object Action { get; set; }
        }
        private class ResetAction { }
        private class MoveToPositionAction
        {
            public int Position { get; set; }
        }
        private class TogglePlayPauseAction { }

        private HistoryComponentStore _internalStore = new HistoryComponentStore();

        public HistoryComponent()
        {
            InitializeComponent();
        }

        private void Initialize()
        {
            if (Store != null)
            {
                Store.Match()
                    .Case1().Do(InitializeFromStore)
                    .Case2().Do(InitializeFromStore)
                    .Case3().Do(InitializeFromStore)
                    .Case4().Do(InitializeFromStore)
                    .Exec();
            }
        }

        private void InitializeFromStore<TState>(ReduxStoreWithHistory<TState> store) where TState : class, new()
        {
            // Observe UI events
            UndoButton.ObserveOnClick()
                .Subscribe(_ => store.Undo());
            RedoButton.ObserveOnClick()
                .Subscribe(_ => store.Redo());
            ResetButton.ObserveOnClick()
                .Subscribe(_ => store.Reset());
            
            PlayPauseButton.ObserveOnClick()
                .Subscribe(_ =>
                {
                    _internalStore.Dispatch(new TogglePlayPauseAction());
                });

            Slider.ObserveOnValueChanged()
                .Subscribe(e =>
                {
                    int newPosition = (int)e.EventArgs.NewValue;
                    _internalStore.Dispatch(new MoveToPositionAction { Position = newPosition });
                });

            // Observe changes on internal state
            _internalStore.ObserveState(state => state.MaxPosition)
                .Subscribe(maxPosition =>
                {
                    Slider.Maximum = maxPosition;
                });

            _internalStore.ObserveState(state => state.CurrentPosition)
                .Subscribe(currentPosition =>
                {
                    Slider.Value = currentPosition;

                    if (!_internalStore.State.PlaySessionActive)
                    {
                        UndoButton.IsEnabled = store.CanUndo;
                        RedoButton.IsEnabled = store.CanRedo;
                        ResetButton.IsEnabled = store.CanUndo || store.CanRedo;
                        PlayPauseButton.IsEnabled = store.CanRedo;
                    }
                });

            _internalStore.ObserveState(state => state.PlaySessionActive)
                .Subscribe(playSessionActive =>
                {
                    if (playSessionActive)
                    {
                        UndoButton.IsEnabled = false;
                        RedoButton.IsEnabled = false;
                        ResetButton.IsEnabled = false;
                        PlayPauseButton.IsEnabled = true;

                        Slider.IsEnabled = false;

                        PlayPauseButton.Content = "\xE769";
                    }
                    else
                    {
                        UndoButton.IsEnabled = store.CanUndo;
                        RedoButton.IsEnabled = store.CanRedo;
                        ResetButton.IsEnabled = store.CanUndo || store.CanRedo;
                        PlayPauseButton.IsEnabled = store.CanRedo;

                        Slider.IsEnabled = _internalStore.State.MaxPosition > 0;

                        PlayPauseButton.Content = "\xE768";
                    }
                });

            _internalStore.ObserveAction<MoveToPositionAction>()
                .Subscribe(a =>
                {
                    if (a.Position < _internalStore.State.CurrentPosition)
                    {
                        for (int i = 0; i < _internalStore.State.CurrentPosition - a.Position; i++)
                        {
                            store.Undo();
                        }
                    }
                    if (a.Position > _internalStore.State.CurrentPosition)
                    {
                        for (int i = 0; i < a.Position - _internalStore.State.CurrentPosition; i++)
                        {
                            store.Redo();
                        }
                    }
                });

            // Observe changes on listened state
            store.ObserveAction()
                .ObserveOnDispatcher()
                .Subscribe(action =>
                {
                    _internalStore.Dispatch(new GoForwardAction { Action = action });
                    if (_internalStore.State.PlaySessionActive && !store.CanRedo)
                    {
                        _internalStore.Dispatch(new TogglePlayPauseAction());
                    }
                });

            store.ObserveUndoneAction()
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    _internalStore.Dispatch(new GoBackAction());
                });

            store.ObserveReset()
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    _internalStore.Dispatch(new ResetAction());
                });

            Observable.Interval(TimeSpan.FromSeconds(1))
                .ObserveOnDispatcher()
                .Where(_ => _internalStore.State.PlaySessionActive)
                .Subscribe(_ =>
                {
                    store.Redo();
                });

            // Initialize UI
            UndoButton.IsEnabled = store.CanUndo;
            RedoButton.IsEnabled = store.CanRedo;
            ResetButton.IsEnabled = store.CanUndo || store.CanRedo;
            PlayPauseButton.IsEnabled = store.CanRedo;
        }
    }
}
