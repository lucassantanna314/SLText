using Silk.NET.Input;
using SLText.View.UI.Input;
using TextCopy;

namespace SLText.View.UI;

public partial class WindowManager
{
    private void OnKeyDown(IKeyboard k, Key key, int arg3)
    {
        if (_explorer.IsFocused && _explorer.IsVisible)
        {
            if (key == Key.Up) { _explorer.HandleKeyDown("Up"); return; }
            if (key == Key.Down) { _explorer.HandleKeyDown("Down"); return; }
            if (key == Key.Enter) { _explorer.HandleKeyDown("Enter"); return; }
            if (key == Key.Escape) { _explorer.ClearSearch(); return; }
        }
        
        if (_signatureHelp.IsVisible)
        {
            if (key == Key.Left || key == Key.Right || key == Key.Home || key == Key.End || key == Key.PageUp || key == Key.PageDown)
            {
                _signatureHelp.IsVisible = false;
            }
        }
        
        if (_autocomplete.IsVisible)
        {
            if (key == Key.Up) { _autocomplete.MoveSelection(-1); return; }
            if (key == Key.Down) { _autocomplete.MoveSelection(1); return; }
            
            if (key == Key.Tab || key == Key.Enter)
            {
                ApplyAutocomplete();
                return; 
            }
            
            if (key == Key.Escape) { _autocomplete.IsVisible = false; return; }

            if (key == Key.Left || key == Key.Right || key == Key.Home || key == Key.End || key == Key.PageUp || key == Key.PageDown)
            {
                _autocomplete.IsVisible = false;
            }
        }
        
        if (IsNavigationOnly(key) || key == Key.Backspace || key == Key.Delete)
        {
            _lastPressedKey = key;
            _repeatTimer = 0;
            _isFirstRepeat = true;
            
            if (key == Key.Backspace || key == Key.Delete)
            {
                RequestDiagnostics();
            }
        }
        else
        {
            _lastPressedKey = null; 
        }

        ProcessKeyPress(key);
        
        if (key == Key.Backspace || key == Key.Delete || (key == Key.V && _activeKeyboard!.IsKeyPressed(Key.ControlLeft)))
        {
            RequestDiagnostics();
        }
        
    }
    private void ProcessKeyPress(Key key)
    {
        bool ctrl = _activeKeyboard.IsKeyPressed(Key.ControlLeft) || _activeKeyboard.IsKeyPressed(Key.ControlRight);
        bool shift = _activeKeyboard.IsKeyPressed(Key.ShiftLeft) || _activeKeyboard.IsKeyPressed(Key.ShiftRight);

        if (_isTerminalFocused && _terminal.IsVisible)
        {
            if (key == Key.Up || key == Key.Down)
            {
                _terminal.HandleSpecialKey(key);
                return;
            }

            if (key == Key.Escape)
            {
                _isTerminalFocused = false;
                return;
            }

            if (key == Key.Enter)
            {
                _terminal.HandleKeyDown("\n");
                return;
            }

            if (key == Key.Backspace)
            {
                _terminal.HandleKeyDown("Backspace");
                return;
            }

            if (IsNavigationOnly(key)) return;

            if (!ctrl) return;
        }

        if (_commandPalette.IsVisible)
        {
            if (key == Key.Escape) { _commandPalette.IsVisible = false; return; }
            if (key == Key.Up) { _commandPalette.MoveSelection(-1); return; }
            if (key == Key.Down) { _commandPalette.MoveSelection(1); return; }
            if (key == Key.Backspace) { _commandPalette.HandleInput("", true); return; }
            if (key == Key.Enter)
            {
                var cmd = _commandPalette.GetSelectedCommand();
                if (cmd != null)
                {
                    _commandPalette.IsVisible = false;
                    cmd.Action.Invoke();
                }
                return;
            }
        }

        if (ctrl && shift && key == Key.P)
        {
            _commandPalette.IsVisible = true;
            _commandPalette.LoadCommands(_inputHandler.GetRegisteredCommands());
            return;
        }

        if (_explorer.IsFocused)
        {
            if (key == Key.Escape)
            {
                _explorer.IsFocused = false;
                _explorer.ClearSearch();
            }

            if (key == Key.Backspace)
            {
                _explorer.HandleSearchInput("", true);
                return;
            }
            return;
        }

        if (_search.IsVisible)
        {
            if (key == Key.Escape)
            {
                _search.IsVisible = false;
                _editor.PerformSearch("");
                _cursor.ClearSelection();
                return;
            }

            if (key == Key.Backspace)
            {
                _search.HandleInput("", true);

                _editor.PerformSearch(_search.SearchText);

                var searchResult = _buffer.FindNext(_search.SearchText, _cursor.Line, 0);
                if (searchResult.HasValue)
                {
                    _cursor.SetSelection(searchResult.Value.line, searchResult.Value.col,
                        searchResult.Value.line, searchResult.Value.col + _search.SearchText.Length);
                }
                return;
            }

            if (key == Key.Enter)
            {
                // Procura a partir da posição atual do cursor + 1
                var nextResult = _buffer.FindNext(_search.SearchText, _cursor.Line, _cursor.Column + 1);

                if (nextResult.HasValue)
                {
                    _cursor.SetSelection(nextResult.Value.line, nextResult.Value.col,
                        nextResult.Value.line, nextResult.Value.col + _search.SearchText.Length);

                    _editor.RequestScrollToCursor();
                }
                else
                {
                    var firstResult = _buffer.FindNext(_search.SearchText, 0, 0);
                    if (firstResult.HasValue)
                    {
                        _cursor.SetSelection(firstResult.Value.line, firstResult.Value.col,
                            firstResult.Value.line, firstResult.Value.col + _search.SearchText.Length);
                        _editor.RequestScrollToCursor();
                    }
                }
            }

            return;
        }

        string? mappedKey = KeyboardMapper.Normalize(key);
        if (mappedKey == null) return;

        if (_modal.IsVisible)
        {
            if (_modal.HandleKeyDown(mappedKey)) return;
        }

        if (ctrl && key == Key.C) { _inputHandler.HandleCopy(); return; }
        if (ctrl && key == Key.V)
        {
            string? text = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                _inputHandler.HandlePaste(text);
                _isDirty = true;
                UpdateTitle();
            }
            return;
        }

        _inputHandler.HandleShortcut(ctrl, shift, mappedKey);

        _editor.RequestScrollToCursor();

        if (!IsNavigationOnly(key) && !ctrl && !_isDirty)
        {
            _isDirty = true;
            UpdateTitle();
        }
    }
}