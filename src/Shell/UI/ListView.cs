using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGUI.Controls;
using ConsoleGUI.Data;
using ConsoleGUI.Input;
using ConsoleGUI.UserDefined;

namespace UI
{
    internal class ListView : SimpleControl, IInputListener
    {
        private readonly Color _textColor = Color.White;
        public Color TextColor
        {
            get => _textColor;
        }

        private readonly Color _selectedTextColor = Color.Black;
        public Color SelectedTextColor
        {
            get => _selectedTextColor;
        }

        private int? _selectedIndex = null;
        public int? SelectedIndex
        {
            get => _selectedIndex;
        }

        public ConsoleKey ScrollUpKey => _vscrollp.ScrollUpKey;
        public ConsoleKey ScrollDownKey => _vscrollp.ScrollDownKey;

        private readonly VerticalScrollPanel _vscrollp;
        private readonly VerticalStackPanel _vstackp;

        public ListView()
        {
            Content = new VerticalScrollPanel()
            {
                Content = new VerticalStackPanel()
            };
            _vscrollp = Content as VerticalScrollPanel;
            _vstackp = _vscrollp.Content as VerticalStackPanel;
        }

        public string SelectedItem
        {
            get
            {
                if (_selectedIndex != null)
                {
                    var selectedItem = _vstackp.Children.ElementAt(_selectedIndex.Value);
                    var current = selectedItem as TextBlock;
                    return current.Text;
                }
                else
                {
                    return null;
                }
            }
        }

        public IEnumerable<string> Items
        {
            set
            {
                if (value == null)
                {
                    _vstackp.Children = new TextBlock[] { };
                    _selectedIndex = null;
                }
                else
                {
                    _selectedIndex = 0;

                    var items = new List<TextBlock>();
                    for (int curIndex = 0; curIndex < value.Count(); curIndex++)
                    {
                        items.Add(new TextBlock()
                        {
                            Text = value.ElementAt(curIndex),
                            Color = curIndex == _selectedIndex ? SelectedTextColor : TextColor
                        });
                    }

                    _vstackp.Children = items;
                }
            }
            get
            {
                return _vstackp.Children.Select(x => x as TextBlock).Select(x => x.Text);
            }
        }

        void IInputListener.OnInput(InputEvent inputEvent)
        {
            if (inputEvent.Key.Key == ScrollUpKey)
            {
                if (_selectedIndex > 0)
                {
                    UpdateColor(_selectedIndex, _selectedIndex - 1);
                    _selectedIndex--;
                }
            }
            else if (inputEvent.Key.Key == ScrollDownKey)
            {
                if (_selectedIndex < _vstackp.Children.Count() - 1)
                {
                    UpdateColor(_selectedIndex, _selectedIndex + 1);
                    _selectedIndex++;
                }
            }

            // Page what is displayed based on the selected item
            var totalNumberOfItemsPossibleOnScreen = _vscrollp.Size.Height;
            if (inputEvent.Key.Key == ScrollDownKey && _selectedIndex % totalNumberOfItemsPossibleOnScreen == 0 || inputEvent.Key.Key == ScrollUpKey && _selectedIndex < _vscrollp.Top)
            {
                inputEvent.Handled = true;
                _vscrollp.Top = inputEvent.Key.Key == ScrollDownKey ?
                    _vscrollp.Top + totalNumberOfItemsPossibleOnScreen :
                    _vscrollp.Top - totalNumberOfItemsPossibleOnScreen;

            }
        }

        private void UpdateColor(int? oldRow, int? newRow)
        {
            if (oldRow != null)
            {
                var oldElement = _vstackp.Children.ElementAt(oldRow.Value);
                var textBlockOld = oldElement as TextBlock;
                textBlockOld.Color = TextColor;
            }

            if (newRow != null)
            {
                var newElement = _vstackp.Children.ElementAt(newRow.Value);
                var textBlockNew = newElement as TextBlock;
                textBlockNew.Color = SelectedTextColor;
            }
        }
    }
}