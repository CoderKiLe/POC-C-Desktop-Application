using C1.WPF.Input;
using C1.WPF.RichTextBox.Documents;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;

namespace C1.WPF.RichTextBox
{
    /// <summary>
    /// Powerful rich text editor that allows you to load, edit, and save formatted text as HTML documents.
    /// </summary>
    [C1Control]
    [ContentProperty("Document")]
    [C1TemplatePart(Name = "Content", Type = typeof(Grid))]
    [C1TemplatePart(Name = "Root", Type = typeof(Panel))]
    [C1TemplatePart(Name = "HorizontalScrollBar", Type = typeof(ScrollBar))]
    [C1TemplatePart(Name = "VerticalScrollBar", Type = typeof(ScrollBar))]
    [C1TemplatePart(Name = "ScrollBars", Type = typeof(FrameworkElement))]
    [C1TemplatePart(Name = "Placeholder", Type = typeof(FrameworkElement))]
    [C1TemplatePart(Name = "Container", Type = typeof(Grid))]
    [C1VisualState(Name = "ReadOnly", GroupName = "Writable", Condition = "IsReadOnly")]
    [C1VisualState(Name = "Writable", GroupName = "Writable")]
    [StyleTypedProperty(Property = nameof(ScrollBarStyle), StyleTargetType = typeof(ScrollBar))]
    [StyleTypedProperty(Property = nameof(ValidationDecoratorStyle), StyleTargetType = typeof(C1ValidationDecorator))]
    public partial class C1RichTextBox : C1View
    {
        #region fields
        // dependency properties used for listening to changes in font properties
        static DependencyProperty InternalFontFamily =
            DependencyProperty.Register("InternalFontFamily", typeof(FontFamily), typeof(C1RichTextBox), new PropertyMetadata(HandleFontProperty(C1TextElement.FontFamilyProperty)));
        static DependencyProperty InternalFontSize =
            DependencyProperty.Register("InternalFontSize", typeof(double), typeof(C1RichTextBox), new PropertyMetadata(HandleFontProperty(C1TextElement.FontSizeProperty)));
        static DependencyProperty InternalFontWeight =
            DependencyProperty.Register("InternalFontWeight", typeof(FontWeight), typeof(C1RichTextBox), new PropertyMetadata(HandleFontProperty(C1TextElement.FontWeightProperty)));
        static DependencyProperty InternalFontStyle =
            DependencyProperty.Register("InternalFontStyle", typeof(FontStyle), typeof(C1RichTextBox), new PropertyMetadata(HandleFontProperty(C1TextElement.FontStyleProperty)));
        //static DependencyProperty InternalForeground =
        //    DependencyProperty.Register("InternalForeground", typeof(Brush), typeof(C1RichTextBox), new PropertyMetadata(HandleFontProperty(C1TextElement.ForegroundProperty)));

        static PropertyChangedCallback HandleFontProperty(StyleProperty property)
        {
            return new PropertyChangedCallback((s, e) =>
            {
                var rtb = (C1RichTextBox)s;
                var doc = rtb.Document;
                if (object.Equals(property.DefaultValue, e.NewValue))
                {
                    doc.ClearValue(property);
                }
                else
                {
                    doc.SetValue(property, e.NewValue);
                }
            });
        }

        int _selectionStart = 0;
        int _selectionLength = 0;
        const int runSplitLength = 500; // number of characters per run when importing text
        internal FlowDirection LastFlowDirection { get; set; }
        internal PageViewMode PageViewMode;
        C1RichTextViewManager _viewManager;
        DocumentHistory _documentHistory;
        C1TextRange _selection;
        LogicalDirection _selectionExtend;
        Point _floatingTail;
        C1SelectionPainter _selectionPainter = new C1SelectionPainter();
        C1ResizePainter _resizePainter = new C1ResizePainter();
        PlaceholderPainter _dragTargetPainter;
        C1TextPointer _positionHover;
        string _highSurrogate = null;
        C1StyleOverrideMerger _styleOverrideMerger = new C1StyleOverrideMerger();
        C1PainterMerger _painterMerger = new C1PainterMerger();
        C1TextElementStyle _formatClip;
        Binding _documentBindingExpression = null;
        C1DragHelper _dragHelper = null;
        internal bool IsInput = false;
        internal bool _isTypingEnglish = false;
        bool _isCutOperation = false;
        bool _isIgnoreTextChanged = false;
        // The times that TextChanged fired
        int _textChangedTimes = 0;
        // The arguments for TextChanged event
        C1TextChangedEventArgs _c1TextChangedEventArgs = null;
        bool _isTableResized = false;
        private const string HyperlinkFormat = @"^(((http|https|ftp|file)://)|(mailto:)|(\\)|(onenote:)|(www\.))(\S+)$";

        AsYouTypeSpellCheck _spellCheck;
        C1TextElement _elementHover;
        Dictionary<Shortcut, Action> _shortcuts;
        bool _dragged;

#if WINDOWS_APP
        C1IMETextBox _txtBox;
#else
        TextBox _txtBox;
#endif

#if UWP
        bool _hasStartedComposition = false;
#endif

#if WINDOWS_APP || UWP
        bool _hasShownContextMenu = false;
        UIElement _uiElementHover;
#endif
#if WINRT
        bool _textBoxIsFocused;
        bool _ignoreTextChanged;
        C1TextRange _manipulatedSelection;
        Point _manipulationStart;

#if WINRT
        int _enterKeyDownCount = 0;
#if WINDOWS_APP || UWP
        bool _isTouchInput = false;
        // For double-click
        Point _lastPos;
        DateTime _lastTime;
        /// <summary>
        /// Gets or sets the context menu object to show on the right tap.
        /// </summary>
        /// <remarks>
        /// The default value is Null. If this value is not changes, the <see cref="C1RichTextBox"/> control will show the simple 
        /// <see cref="Windows.UI.Popups.PopupMenu"/> with the set of clipboard-related commands.
        /// You can set this property to the custom <see cref="Windows.UI.Popups.PopupMenu"/> object, or any object implementing
        /// <see cref="C1.Xaml.IC1ContextMenu"/> interface. In such case the <see cref="C1RichTextBox"/> control will show your custom 
        /// context menu instead of default one.
        /// </remarks>
        public object ContextMenu { get; set; }
        PopupMenu _contextMenu;
        UICommand _copyCommand;
        UICommand _pasteCommand;
        UICommand _cutCommand;
        UICommand _selectallCommand;
#endif
        bool _needsShowThumbs = false;        
        double _keyboardHeight = 0.0;
#endif
#endif
#if WPF || WINRT
        RtfFilter _rtfFilter = null;
#endif
        #endregion

        #region intialization

        partial void InitializeModel()
        {
#if WINRT
            if (!this.IsDesignTime())
#endif
            HtmlFilter = new HtmlFilter { Dispatcher = Dispatcher };
            IsEnabledChanged += (s, e) => { OnIsEnabledChanged(); };
            IsEnabled = true;
            LastFlowDirection = FlowDirection;
            PageViewMode = PageViewMode.Normal;
            Document = new C1Document { new C1Paragraph { new C1Run() } };
            TextWrapping = TextWrapping.Wrap;
#if WINRT
            NavigationMode = NavigationMode.Always;
#else
            NavigationMode = NavigationMode.OnControlKey;
#endif

            ClipboardMode = ClipboardMode.RichText;
            CreateShortcuts();
            GotFocus += OnGotFocus;
            SetBinding(InternalFontFamily, new Binding().From(this, x => x.FontFamily));
            SetBinding(InternalFontSize, new Binding().From(this, x => x.FontSize));
            SetBinding(InternalFontWeight, new Binding().From(this, x => x.FontWeight));
            SetBinding(InternalFontStyle, new Binding().From(this, x => x.FontStyle));

#if WINRT
            IsTabStop = true;
#else
            IsTabStop = false;
#endif

#if WPF || WINRT
            _rtfFilter = new RtfFilter();
#endif
        }

        internal void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource == this && _txtBox != null)
            {
                _txtBox.Focus();
            }
        }

        private void ResetTextBox()
        {
            if (_txtBox == null)
                return;
            if (!string.IsNullOrEmpty(_txtBox.Text))
            {
                _txtBox.Text = "";
            }

            if (_txtBox.HorizontalAlignment != HorizontalAlignment.Left)
            {
                _txtBox.HorizontalAlignment = HorizontalAlignment.Left;
            }

#if WPF || WINDOWS_APP || UWP
            if (isKoreanCharacter)
            {
                isKoreanCharacter = false;
            }

            if (_txtBox.FlowDirection != FlowDirection.LeftToRight)
            {
                _txtBox.FlowDirection = FlowDirection.LeftToRight;
            }
#if WINDOWS_APP || UWP
            _txtBox.MinHeight = 0.0;
#endif
#endif
        }

        private Size MeasureTextboxText(string text, TextBox txtBox)
        {
            return TextHelper.Measure(text, txtBox.FontFamily,
                    txtBox.FontWeight, txtBox.FontStyle, txtBox.FontSize);
        }

        void _txtBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != 0 && e.PreviousSize.Width == 0)
            {
                this.UpdateSelectionVisibility(false);
            }
            else if (IsTextFocused && e.NewSize.Width == 0 && e.PreviousSize.Width != 0)
            {
                UpdateSelectionVisibility();
            }

            // If input CJK characters, we should show the caret in the internal textbox.
            // In other cases, we should hide it.
        }

#if UWP
        void _txtBox_TextCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args)
#else
        void _txtBox_TextInput(object sender, TextCompositionEventArgs e)
#endif
        {
            var text = _txtBox.Text;
#if UWP
            var isCtrlText = false;
            _hasStartedComposition = false;
#else
            var isCtrlText = !string.IsNullOrEmpty(e.Text) &&
#if WINRT
                char.IsControl(e.Text[0]);
#else
                char.GetUnicodeCategory(e.Text[0]) == UnicodeCategory.Control;
#endif

#if WPF
            // There is no composition text if you type something from handwriting keyboard in Windows 10,
            // This is opposite with other virtual keyboard layout and real keyboard.
            // e.TextComposition is FrameworkTextComposition in such case.
            var isHandWritingKeyboardUsed = e.TextComposition is FrameworkTextComposition;
#endif

            if (string.IsNullOrEmpty(text.Trim())
#if WPF
                // Do not use e.Text when type something from handwriting keyboard in Windows 10.
                && !isHandWritingKeyboardUsed
#endif
                )
            {
                text = e.Text;
            }
#endif

#if WPF || WINRT
            if (curIMELanguage == IMELanguage.Korean)
            {
                if (string.IsNullOrEmpty(compositionText) || isCtrlText)
                {
                    return;
                }

                text = compositionText;
                isKoreanCharacter = true;
#if UWP
                compositionText = "";
                curIMELanguage = IMELanguage.Other;
                this.ResetTextBox();
#endif
            }
            else
            {
#if WPF
                if (e.Text == "\r")
                {
                    // Press Enter key from handwriting keyboard in Windows 10, the composition inputting is not finished in textbox.
                    // If you type something first, then we will insert the text and one line break.
                    // If you only type Enter key, then we will insert one line break.
                    if (text != "\r")
                    {
                        this.InputText(text);
                        HandleEnterKeyDown();
                    }
                    this.ResetTextBox();
                    return;
                }
#endif
                this.ResetTextBox();
#if !UWP
                if ((string.IsNullOrEmpty(e.Text)
#if WPF
                    // Do not use e.Text when type something from handwriting keyboard in Windows 10.
                    && !isHandWritingKeyboardUsed
#endif
                    ) || isCtrlText)
                {
                    return;
                }
#endif
            }
#else
                this.ResetTextBox();

            if (string.IsNullOrEmpty(e.Text) || isCtrlText)
            {
                return;
            }
#endif
            if (text.Length > 0 && text.ToCharArray().Last().IsHighSurrogate())
            {
                _highSurrogate = text.ToCharArray().Last().ToString();
                return;
            }
            if (_highSurrogate != null)
            {
                text = _highSurrogate + text;
                _highSurrogate = null;
            }

            this.InputText(text);
#if WINRT
            _txtBox.MinHeight = 0.0;
#endif
        }
#if WPF
        void _txtBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(_shortcuts.ContainsKey(new Shortcut(ModifierKeys.Shift, Key.Insert)))
                && Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Insert)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Insert && Keyboard.Modifiers != ModifierKeys.Control && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                return;
            }
        }
#endif

        void _txtBox_KeyDown(object sender, KeyEventArgs e)
        {
#if WINRT
            // Control KeyDown event is fired twice when Enter key is pressed.
            // http://social.msdn.microsoft.com/Forums/en-US/winappswithcsharp/thread/734d6c7a-8da2-48c6-9b3d-fa868b4dfb1d
            if (e.Key == Key.Enter)
            {
                _enterKeyDownCount++;
                if (_enterKeyDownCount == 2)
                    _enterKeyDownCount = 0;
                else
                    return;
            }
#endif
            e.Handled = false;
            this.OnC1KeyDown(e);
        }


#if WPF || WINRT
        private enum IMELanguage
        {
            Korean = 0,
            Other = 1
        }

        private IMELanguage curIMELanguage = IMELanguage.Other;
        private string compositionText = string.Empty;
        private bool isKoreanCharacter = false;

#if UWP
        void _txtBox_TextCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args)
#else
        void _txtBox_TextInputStart(object sender, TextCompositionEventArgs e)
#endif
        {
            _isTypingEnglish = true;
#if WINRT
            _txtBox.ClearValue(TextBox.MinHeightProperty);
#endif
#if UWP
            var text = _txtBox.Text;
            _hasStartedComposition = true;
#else
            var text = e.TextComposition.CompositionText;
#endif
            _txtBox.FontSize = this.Selection.End.Element.FontSize;
            _txtBox.Width = 0;

            //Only using Korean IME, e.TextComposition.CompositionText cann't be empty string.
            if (!string.IsNullOrEmpty(text) && TextHelper.IsKoreanCharacter(text[0]))
            {
                compositionText = text;
                this.curIMELanguage = IMELanguage.Korean;
                _txtBox.FlowDirection = FlowDirection.RightToLeft;
                Size desiredSize = this.MeasureTextboxText(compositionText, _txtBox);
                _txtBox.MaxWidth = desiredSize.Width;
                _txtBox.MaxHeight = desiredSize.Height;
            }
            else
            {
                this.curIMELanguage = IMELanguage.Other;

                if (isKoreanCharacter)
                {
                    this.ResetTextBox();
                }
            }
        }

#if UWP
        void _txtBox_TextCompositionChanged(TextBox sender, TextCompositionChangedEventArgs args)
#else
        void _txtBox_TextInputUpdate(object sender, TextCompositionEventArgs e)
#endif
        {
            _isTypingEnglish = false;
#if UWP
            compositionText = _txtBox.Text;
#else
            compositionText = e.TextComposition.CompositionText;
            Size desiredSize = this.MeasureTextboxText(compositionText, _txtBox);
            _txtBox.MaxWidth = desiredSize.Width;
            _txtBox.MaxHeight = desiredSize.Height;
#endif
#if WINRT
            // Need to call UpdateLayout method when input text for WinRT platform to get the correct ActualWidth.
            // Fixed the issue #45251.
            _txtBox.UpdateLayout();
#endif
        }
#endif

        void _txtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
#if WPF || WINRT
            if (curIMELanguage == IMELanguage.Korean)
            {
                return;
            }
#endif

#if UWP
            _isTypingEnglish = true;
            if (!_hasStartedComposition && !String.IsNullOrEmpty(_txtBox.Text))
            {
                var text = _txtBox.Text;
                this.ResetTextBox();
                if (text.Length > 0 && text.ToCharArray().Last().IsHighSurrogate())
                {
                    _highSurrogate = text.ToCharArray().Last().ToString();
                    return;
                }
                if (_highSurrogate != null)
                {
                    text = _highSurrogate + text;
                    _highSurrogate = null;
                }
                this.InputText(text);
            }
#endif

            if (string.IsNullOrEmpty(_txtBox.Text))
            {
                this.ResetTextBox();
            }
            else if (_txtBox.Width != Double.NaN)
            {
                _txtBox.Width = Double.NaN;
            }

            if (_txtBox.Width != 0)
            {
                Thickness margin = _txtBox.Margin;

#if WINRT
                var width = MeasureTextboxText(_txtBox.Text, _txtBox).Width;

                if (width + margin.Left > this._elementContent.ActualWidth)
#else
                if (_txtBox.ExtentWidth + margin.Left > this._elementContent.ActualWidth)
#endif
                {
                    if (_txtBox.HorizontalAlignment != HorizontalAlignment.Right)
                    {
                        _txtBox.HorizontalAlignment = HorizontalAlignment.Right;
                    }

                    if (_txtBox.Margin.Left != 0)
                    {
                        _txtBox.Margin = new Thickness(0, margin.Top, 0, 0);
                    }
                }
            }
        }

        void _txtBox_GotFocus(object sender, RoutedEventArgs e)
        {
#if WINRT
            if (e.OriginalSource != this._txtBox)
            {
                return;
            }
            _textBoxIsFocused = true;
#endif

#if WINDOWS_APP || UWP
            // If the focus was set when DragDelta event is fired, do nothing.
            if (_dragged)
                return;
#endif
            if (Document.IsLeaf && !IsReadOnly)
            {
                Document = new C1Document { new C1Paragraph { new C1Run() } };
                Selection = new C1TextRange(Document.ContentStart, Document.ContentStart);
            }

            UpdateSelectionVisibility();

            SetWatermark(false);
        }

        void _txtBox_LostFocus(object sender, RoutedEventArgs e)
        {
#if WINRT
            if (e.OriginalSource != this._txtBox)
            {
                return;
            }

            _textBoxIsFocused = false;
#endif

#if WINDOWS_APP || UWP
            // If the focus was set when DragDelta event is fired, do nothing.
            if (_dragged)
                return;
#endif
            this.ResetTextBox();
            CalculatedDependencyProperty.Update(this, TextProperty);
            CalculatedDependencyProperty.Update(this, HtmlProperty);

            UpdateSelectionVisibility();
            SetWatermark(true);
        }

        partial void InitializeUI()
        {
            InitializeScroll();

            _scrollPresenter.RemoveFromParent();
            _elementContent.Children.Add(_scrollPresenter);
#if WPF
            Style style = XamlReaderEx.Parse<Style>(
                                @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" TargetType=""TextBlock"">
                        <Setter Property=""TextOptions.TextFormattingMode"" Value=""Ideal""/>
                    </Style>");

            _elementContent.Resources.Add(typeof(TextBlock), style);
#endif
            if (!this.IsDesignTime())
            {
                _txtBox = new TextBoxWithoutKeyboardShortcuts
                {
#if !WINRT
                    IsUndoEnabled = false,
#endif
                    Width = 0,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Left,
#if UWP
                    // Automatic capitalization: enabled if IsSpellCheckEnabled = true, disabled if IsSpellCheckEnabled = false
                    // https://msdn.microsoft.com/en-us/library/windows/apps/mt280229.aspx?f=255&MSPPError=-2147217396
                    IsSpellCheckEnabled = false,
#endif
                    VerticalAlignment = VerticalAlignment.Top
                };
#if WINRT
                _txtBox.Margin = new Thickness(0);
                _txtBox.MinWidth = 0.0;
                _txtBox.MinHeight = 0.0;
#else
                _txtBox.BindInputMethodProperties(this);
#endif

#if WINDOWS_APP
                _txtBox.IsTextCompositionEnabled = true;
                
#endif
                _txtBox.SetBinding(Control.BackgroundProperty, new Binding() { Path = new PropertyPath("Background"), Source = this });
                _txtBox.SetBinding(Control.RenderTransformProperty, new Binding() { Path = new PropertyPath("Zoom"), Source = this, Converter = new ZoomToScaleTransformConverter() });
                _txtBox.SizeChanged += new SizeChangedEventHandler(_txtBox_SizeChanged);
#if !WINRT
                _txtBox.AddHandler(TextBox.TextInputEvent, new TextCompositionEventHandler(_txtBox_TextInput), true);
#endif
                _txtBox.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(_txtBox_KeyDown), true);
#if WPF
                _txtBox.PreviewKeyDown += _txtBox_PreviewKeyDown;
                TextCompositionManager.AddTextInputStartHandler(_txtBox, _txtBox_TextInputStart);
                TextCompositionManager.AddTextInputUpdateHandler(_txtBox, _txtBox_TextInputUpdate);
#endif

#if UWP
                _txtBox.TextCompositionStarted += _txtBox_TextCompositionStarted;
                _txtBox.TextCompositionChanged += _txtBox_TextCompositionChanged;
                _txtBox.TextCompositionEnded += _txtBox_TextCompositionEnded;
#endif

#if WINDOWS_APP
                _txtBox.TextInput += _txtBox_TextInput;
                _txtBox.TextInputStart += _txtBox_TextInputStart;
                _txtBox.TextInputUpdate += _txtBox_TextInputUpdate;
#endif

                _txtBox.TextChanged += new TextChangedEventHandler(_txtBox_TextChanged);
                _txtBox.GotFocus += new RoutedEventHandler(_txtBox_GotFocus);
                _txtBox.LostFocus += new RoutedEventHandler(_txtBox_LostFocus);
                _elementContent.Children.Add(_txtBox);
                _selectionPainter.TextBox = _txtBox;

#if WPF
                var tapHelper = new C1TapHelper(_elementContent);
                var scrollHelper = new C1ScrollHelper(_elementContent, continuousScroll: false, handleMouseWheel: false);
                _dragHelper = new C1DragHelper(_elementContent, captureElementOnPointerPressed: true);
                if (_elementContainer != null)
                {
                    tapHelper = new C1TapHelper(_elementContainer);
                    if (scrollHelper != null)
                        scrollHelper.FinalizeHelper();
                    scrollHelper = new C1ScrollHelper(_elementContainer, continuousScroll: false, handleMouseWheel: false);
                    if (_dragHelper != null)
                        _dragHelper.FinalizeHelper();
                    _dragHelper = new C1DragHelper(_elementContainer, captureElementOnPointerPressed: true);
                }
                if (_elementContainer != null)
                {
                    _elementContainer.MouseLeftButtonDown += OnMouseLeftButtonDown;
                    _elementContainer.MouseLeftButtonUp += OnMouseLeftButtonUp;
                }
                else
                {
                    _elementContent.MouseLeftButtonDown += OnMouseLeftButtonDown;
                    _elementContent.MouseLeftButtonUp += OnMouseLeftButtonUp;
                }
                _elementContent.MouseMove += OnMouseMove;
                _elementContent.MouseLeave += OnMouseLeave;
#else
                var tapHelper = new C1TapHelper(this);

#if WINRT
#if WINDOWS_APP || UWP
                var scrollHelper = new C1ScrollHelper(_elementContent, continuousScroll: false, handleMouseWheel: false);
                _dragHelper = new C1DragHelper(_elementContainer, C1DragHelperMode.Inertia | C1DragHelperMode.TranslateXY);
                _elementContent.PointerReleased += OnPointerReleased;
                _elementContent.PointerMoved += OnPointerMoved;
                _elementContainer.PointerExited += OnPointerExited;
                _elementContainer.PointerEntered += OnPointerEntered;
                _elementContent.DoubleTapped += OnDoubleTapped;
#else
                _dragHelper = new C1DragHelper(this, C1DragHelperMode.Inertia | C1DragHelperMode.TranslateXY, useManipulationEvents : true);
#endif
                if (_elementCursorThumb != null)
                {
                    _elementCursorThumb.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
                }
                if (_elementSelectionStartThumb != null)
                {
                    _elementSelectionStartThumb.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
                }
                if (_elementSelectionEndThumb != null)
                {
                    _elementSelectionEndThumb.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
                }
#else
                _dragHelper = new C1DragHelper(this, C1DragHelperMode.Inertia | C1DragHelperMode.TranslateXY);
#endif
#endif
                tapHelper.Tapped += OnTapped;
                _dragHelper.DragStarted += OnDragStarted;
                _dragHelper.DragDelta += OnDragDelta;
                _dragHelper.DragCompleted += OnDragCompleted;
#if WINDOWS_APP || UWP
                tapHelper.RightTapped += OnRightTapped;
#else
                tapHelper.DoubleTapped += OnDoubleTapped;
#endif
                scrollHelper.ScrollDelta += OnScrollDelta;
            }

            CreateViewManager();

            OnIsEnabledChanged();
            OnSelectionBackgroundChanged();
            OnSelectionForegroundChanged();
            OnUIContainerResizeTemplateChanged();
            OnZoomChanged();
            UpdateFontProperties();

            if (_documentBindingExpression != null)
            {
                SetBinding(DocumentProperty, _documentBindingExpression);
                _documentBindingExpression = null;
            }

            SetWatermark(true);

#if WINRT
            if (!this.IsDesignTime())
            {
#if WINDOWS_APP || UWP
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.IBeam, 3);

                _lastPos = new Point();
                _lastTime = new DateTime();

                _contextMenu = new PopupMenu();
                _copyCommand = new UICommand(C1_Silverlight_RichTextBox.Copy, (command) =>
                {
                    ClipboardCopy();
                    Focus(FocusState.Programmatic);
                });
                _pasteCommand = new UICommand(C1_Silverlight_RichTextBox.Paste, (command) =>
                {
                    ClipboardPasteUser();
                    Focus(FocusState.Programmatic);
                });
                _cutCommand = new UICommand(C1_Silverlight_RichTextBox.Cut, (command) =>
                {
                    ClipboardCutUser();
                    Focus(FocusState.Programmatic);
                });
                _selectallCommand = new UICommand(C1_Silverlight_RichTextBox.SelectAll, (command) =>
                {
                    SelectAll();
                    Focus(FocusState.Programmatic);
                });
#else
                _keyboardHeight = 0.0;
                Windows.UI.ViewManagement.InputPane.GetForCurrentView().Showing += (s, args) =>
                {
                    _keyboardHeight = args.OccludedRect.Height;
                };
                Windows.UI.ViewManagement.InputPane.GetForCurrentView().Hiding += (s, args) =>
                {
                    _keyboardHeight = 0.0;
                };

                //if (_elementCopy != null)
                //{
                //    _elementCopy.Visibility = Visibility.Collapsed;
                //    _elementCopy.Tapped += (s, e) =>
                //    {
                //        ClipboardCopy();
                //        e.Handled = true;
                //    };
                //    _elementCopy.GotFocus += (s, e) =>
                //    {
                //        _textBoxIsFocused = true;
                //    };
                //}

                //if (_elementCut != null)
                //{
                //    _elementCut.Visibility = Visibility.Collapsed;
                //    _elementCut.Tapped += (s, e) =>
                //    {
                //        ClipboardCut();
                //        e.Handled = true;

                //        if (_elementCopy != null)
                //        {
                //            _elementCopy.Visibility = Visibility.Collapsed;
                //        }
                //        if (_elementCut != null)
                //        {
                //            _elementCut.Visibility = Visibility.Collapsed;
                //        }
                //    };
                //    _elementCut.GotFocus += (s, e) =>
                //    {
                //        _textBoxIsFocused = true;
                //    };
                //}
#endif
            }
#endif
        }

        #endregion

        #region object model

        /// <summary>
        /// Gets or sets whether the user is allowed to modify the text in the control.
        /// </summary>
        [C1DependencyProperty]
        bool _isReadOnly;

        /// <summary>
        /// Gets or sets whether the control should highlight the selection when not in focus.
        /// </summary>
        [C1DependencyProperty]
        bool _hideSelection;

        /// <summary>
        /// Returns true if the control has the focus.
        /// </summary>
        [C1DependencyProperty]
        bool _isFocused;

        /// <summary>
        /// Gets or sets whether the control should wrap the text to fit its width.
        /// </summary>
        [C1DependencyProperty]
        TextWrapping _textWrapping;

        /// <summary>
        /// Gets or sets a value that indicates whether a horizontal <see cref="ScrollBar"/> should be displayed.
        /// </summary>
        [C1DependencyProperty]
        ScrollBarVisibility _horizontalScrollBarVisibility = ScrollBarVisibility.Auto;

        /// <summary>
        /// Gets or sets a value that indicates whether a horizontal <see cref="ScrollBar"/> should be displayed.
        /// </summary>
        [C1DependencyProperty]
        ScrollBarVisibility _verticalScrollBarVisibility = ScrollBarVisibility.Auto;

        /// <summary>
        /// Gets or sets a value that indicates how the text editing control responds when the user presses the ENTER key.
        /// </summary>
        [C1DependencyProperty]
        bool _acceptsReturn = true;

        /// <summary>
        /// Gets or sets the brush used to fill the background of the selected text.
        /// </summary>
        [C1DependencyProperty]
        Brush _selectionBackground;

        /// <summary>
        /// Gets or sets the brush used to fill the foreground of the selected text.
        /// </summary>
        [C1DependencyProperty]
        Brush _selectionForeground;

        /// <summary>
        /// Gets or sets a value of <see cref="ReturnMode"/> that indicates how the <see cref="C1RichTextBox"/> handles the Return key.
        /// </summary>
        [C1DependencyProperty]
        ReturnMode _returnMode = ReturnMode.Default;

        /// <summary>
        /// Gets or sets whether tabs are accepted.
        /// </summary>
        [C1DependencyProperty]
        bool _acceptsTab = true;

        /// <summary>
        /// Gets or sets the size of a tab character in spaces.
        /// </summary>
        [C1DependencyProperty]
        int _tabSize = 4;

        /// <summary>
        /// Gets or sets whether the caret is hidden.
        /// </summary>
        [C1DependencyProperty]
        bool _hideCaret;

        /// <summary>
        /// Gets or sets whether text selection is disabled.
        /// </summary>
        [C1DependencyProperty]
        bool _disableSelection;

        /// <summary>
        /// Gets or sets a value that indicates whether the horizontal <see cref="ScrollBar"/> is visible.
        /// </summary>
        [C1DependencyProperty]
        Visibility _computedHorizontalScrollBarVisibility;

        /// <summary>
        /// Gets or sets a value that indicates whether the vertical <see cref="ScrollBar"/> is visible.
        /// </summary>
        [C1DependencyProperty]
        Visibility _computedVerticalScrollBarVisibility;

        /// <summary>
        /// Gets or sets a value that indicates the horizontal offset of the scrolled content.
        /// </summary>
        [C1DependencyProperty]
        double _horizontalOffset;

        /// <summary>
        /// Gets or sets a value that indicates the vertical offset of the scrolled content.
        /// </summary>
        [C1DependencyProperty]
        double _verticalOffset;

        /// <summary>
        /// Gets or sets a value that represents the vertical size of the area that can be scrolled. The difference between the width of the extent and the width of the viewport.
        /// </summary>
        [C1DependencyProperty]
        double _scrollableWidth;

        /// <summary>
        /// Gets or sets a value that represents the horizontal size of the area that can be scrolled. The difference between the height of the extent and the height of the viewport.
        /// </summary>
        [C1DependencyProperty]
        double _scrollableHeight;

        /// <summary>
        /// Gets or sets the horizontal size of all the content for display in the <see cref="C1RichTextBox"/>;.
        /// </summary>
        [C1DependencyProperty]
        double _extentWidth;

        /// <summary>
        /// Gets or sets the vertical size of all the content for display in the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty]
        double _extentHeight;

        /// <summary>
        /// Gets or sets a value that contains the horizontal size of the viewable content.
        /// </summary>
        [C1DependencyProperty]
        double _viewportWidth;

        /// <summary>
        /// Gets or sets a value that contains the vertical size of the viewable content.
        /// </summary>
        [C1DependencyProperty]
        double _viewportHeight;

        /// <summary>
        /// Gets or sets the <see cref="Documents.C1Document"/> for the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty(OnChangedNeedsValues = true)]
        Documents.C1Document _document;

        /// <summary>
        /// Gets or sets the <see cref="TextViewMode"/> of the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty(OnChangedEvent = true)]
        TextViewMode _viewMode = TextViewMode.Draft;

        /// <summary>
        /// Gets or sets the <see cref="DataTemplate"/> used to present the <see cref="C1RichTextBox"/> in print mode.
        /// </summary>
        /// <remarks>The DataTemplate should have a <see cref="C1RichTextPresenter"/> whose Source property is binded to the data context.</remarks>
        [C1DependencyProperty]
        DataTemplate _printTemplate;

        /// <summary>
        /// Gets or sets the layout used for the pages when <see cref="C1RichTextBox.ViewMode"/> is <see cref="TextViewMode.Print"/>.
        /// </summary>
        [C1DependencyProperty]
        C1PageLayout _printPageLayout;

        /// <summary>
        /// Gets or sets a zoom value applied to the contents of the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty]
        double _zoom = 1.0;

        /// <summary>
        /// Gets or sets the <see cref="ItemsPanelTemplate"/> used for the panel containing the pages in print view mode.
        /// </summary>
        [C1DependencyProperty]
        ItemsPanelTemplate _printPanelTemplate;

        /// <summary>
        /// Gets or sets the <see cref="HtmlFilter"/> used by this <see cref="C1RichTextBox"/> to serialize and deserialize from HTML.
        /// </summary>
        [C1DependencyProperty]
        Documents.HtmlFilter _htmlFilter = new Documents.HtmlFilter();

        /// <summary>
        /// Gets or sets the page number for the currently displayed page.
        /// </summary>
        [C1DependencyProperty]
        int _pageNumber;

        /// <summary>
        /// Gets or sets the current number of display pages for the content hosted by the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty]
        int _pageCount;

        /// <summary>
        /// Gets or sets the Style used by the two ScrollBar controls contained in the default template.
        /// </summary>
        [C1DependencyProperty]
        Style _scrollBarStyle;

        /// <summary>
        /// Gets or sets the <see cref="DataTemplate"/> that generates the thumbs for resizing <see cref="Documents.C1InlineUIContainer"/>.
        /// </summary>
        [C1DependencyProperty]
        DataTemplate _uIContainerResizeTemplate;

        /// <summary>
        /// Gets or sets the brush of blinking cursor of the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty]
        Brush _caretBrush = new SolidColorBrush(Colors.Black);

        /// <summary>
        /// Gets or sets the default outer margin of a <see cref="Documents.C1Paragraph"/> element.
        /// </summary>
        [C1DependencyProperty]
        Thickness _defaultParagraphMargin = new Thickness(0, 0, 0, 10);

        /// <summary>
        /// Gets or sets the placeholder text shown when there is not text in the control.
        /// </summary>
        [C1DependencyProperty]
        string _placeholder;

#if WINUI
        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="C1SpellChecker"/> is enabled.
        /// </summary>
        [C1DependencyProperty]
        bool _isSpellCheckerEnabled=true;
#endif

        /// <summary>
        /// Gets or sets a value that indicates whether url strings would be converted to hyperlink when the user presses the ENTER or SPACE key.
        /// </summary>
        [C1DependencyProperty]
        bool _autoFormatHyperlinks = true;

        /// <summary>
        /// Gets or sets a value of <see cref="AutoCapitalizationMode"/> that indicates how the <see cref="C1RichTextBox"/> handles the auto capitalization.
        /// </summary>
        [C1DependencyProperty]
        AutoCapitalizationMode _autoCapitalizationMode = AutoCapitalizationMode.None;

        /// <summary>
        /// Gets or sets the line-number mode of the <see cref="C1RichTextBox"/>.
        /// </summary>
        [C1DependencyProperty(OnChangedEvent = true)]
        TextLineNumberMode _lineNumberMode = TextLineNumberMode.None;

        /// <summary>
        /// Gets or sets the style that is applied to the inner <see cref="C1ValidationDecorator" />.
        /// </summary>
        [C1DependencyProperty]
        Style _validationDecoratorStyle;

        /// <summary>
        /// Gets or sets the <see cref="Brush" /> used to highlight the border of the control when it has the mouse over.
        /// </summary>
        [C1DependencyProperty]
        Brush _mouseOverBorderBrush;

        #region WP Editing Mode

#if WINRT

        internal static readonly DependencyProperty TranslateYProperty = DependencyProperty.Register("TranslateY", typeof(double), typeof(C1RichTextBox), new PropertyMetadata(0d, OnRenderXPropertyChanged));

        internal double TranslateY
        {
            get { return (double)GetValue(TranslateYProperty); }
        }

        private static void OnRenderXPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rtb = d as C1RichTextBox;
            if (!rtb._textBoxIsFocused && rtb.IsEnabled && !rtb.IsReadOnly)
            {
                rtb.InvalidateMeasure();
            }
        }

#if !WINRT
        // Comment out the codes as we cannot use the same way to get the height of keyboard on Windows Phone 8.1.
        private void BindToKeyboardFocus()
        {
            var frame = Application.Current.RootVisual as FrameworkElement;
            if (frame != null)
            {
                double systemTrayHeight = 0;
                try
                {
                    systemTrayHeight = Microsoft.Phone.Shell.SystemTray.IsVisible ? 32 : 0;
                }
                catch { }
                _topOffset = this.C1TransformToVisual(frame).Transform(new Point()).Y - systemTrayHeight;
                var group = frame.RenderTransform as TransformGroup;
                if (group != null)
                {
                    var translate = group.Children[0] as TranslateTransform;
                    var translateYBinding = new Binding();
                    translateYBinding.Path = new PropertyPath("Y");
                    translateYBinding.Source = translate;
                    SetBinding(TranslateYProperty, translateYBinding);
                }
            }
        }
#endif

        double _topOffset = 0;

        /// <summary>
        /// Provides the behavior for the Measure pass of Silverlight layout. Classes can override this method to define their own Measure pass behavior.
        /// </summary>
        /// <param name="availableSize">The available size that this object can give to child objects. Infinity (<see cref="F:System.Double.PositiveInfinity"/>) can be specified as a value to indicate that the object will size to whatever content is available.</param>
        /// <returns>
        /// The size that this object determines it needs during layout, based on its calculations of the allocated sizes for child objects; or based on other considerations, such as a fixed container size.
        /// </returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if(this.IsDesignTime())
                return base.MeasureOverride(availableSize);
//#if !WINRT
//            var frame = Application.Current.RootVisual as FrameworkElement;
//#endif
            if (!double.IsInfinity(availableSize.Height))
            {
                availableSize = new Size(availableSize.Width, Math.Max(availableSize.Height - Math.Max(0, -TranslateY - _topOffset), 0));
            }
            var size = base.MeasureOverride(availableSize);
            if (!double.IsInfinity(availableSize.Height))
            {
                return availableSize;
            }
            else
            {
                return size;
            }
        }

        /// <summary>
        /// Provides the behavior for the Arrange pass of Silverlight layout. Classes can override this method to define their own Arrange pass behavior.
        /// </summary>
        /// <param name="finalSize">The final area within the parent that this object should use to arrange itself and its children.</param>
        /// <returns>
        /// The actual size that is used after the element is arranged in layout.
        /// </returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if(this.IsDesignTime())
                return base.ArrangeOverride(finalSize);

            this.RenderTransform = new TranslateTransform { Y = Math.Max(0, -TranslateY - _topOffset) / 2.0 };
            finalSize = new Size(finalSize.Width, Math.Max(finalSize.Height - Math.Max(0, -TranslateY - _topOffset), 0));
            var size = base.ArrangeOverride(finalSize);
#if WINRT
            Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
#else
            Dispatcher.BeginInvoke(() =>
#endif
            {
                ScrollSelection();
            });
            return size;
        }

#endif
        #endregion

        /// <summary>
        /// Fires when the <see cref="Document"/> property changes.
        /// </summary>
        public event EventHandler DocumentChanged;

        /// <summary>
        /// Fires when the IsReadOnly property changes.
        /// </summary>
        internal event EventHandler IsReadOnlyChanged;

        /// <summary>
        /// Gets the <see cref="C1RichTextViewManager"/> currently used by this <see cref="C1RichTextBox"/>.
        /// </summary>
        public C1RichTextViewManager ViewManager
        {
            get { return _viewManager; }
        }

        /// <summary>
        /// Gets the <see cref="DocumentHistory"/> associated with current <see cref="Document"/>, used for undoing and redoing actions.
        /// </summary>
        public DocumentHistory DocumentHistory
        {
            get { return _documentHistory; }
        }

        /// <summary>
        /// Occurs when a keyboard key is pressed while the <see cref="C1RichTextBox"/> has focus.
        /// </summary>
        /// <remarks>
        /// This event overrides <see cref="UIElement"/>'s KeyDown in order to allow handlers to prevent the default
        /// behavior of a key press.
        /// </remarks>
        public new event KeyEventHandler KeyDown;

#if !WINRT
        /// <summary>
        ///  Occurs when the left mouse button is pressed (or when the tip of the stylus touches the tablet PC) while the mouse pointer is over a <see cref="UIElement"/>.
        /// </summary>
        /// <remarks>
        /// This event overrides <see cref="UIElement"/>'s MouseLeftButtonDown in order to allow handlers to prevent the default
        /// behavior of a mouse down.
        /// </remarks>
        public new event MouseButtonEventHandler MouseLeftButtonDown;
#endif

#if WINRT
        /// <summary>
        ///  Occurs when the pointer device initiates a Press action within this element.
        /// </summary>
        /// <remarks>
        /// This event overrides <see cref="UIElement"/>'s PointerPressed in order to allow handlers to prevent the default
        /// behavior of a Press action.
        /// </remarks>
        public new event PointerEventHandler PointerPressed;
#endif

        /// <summary>
        /// Gets a <see cref="C1TextRange"/> that represents a range in the current 
        /// <see cref="Document"/>.
        /// </summary>
        /// <param name="start">Offset from the start of the document.</param>
        /// <param name="length">Length of the range in characters.</param>
        /// <returns>A <see cref="C1TextRange"/> that represents the requested range.</returns>
        /// <remarks>
        /// You can use the <see cref="C1TextRange"/> returned to format parts of the
        /// document without moving the <see cref="Selection"/>.
        /// </remarks>
        public C1TextRange GetTextRange(int start, int length)
        {
            if (Document == null)
                return null;
            var range = new C1TextRange(Document, start, length);
            return new C1TextRange(range.Start.ClosestCaret, range.End.ClosestCaret);
        }

        /// <summary>
        /// Fired when there is a change in the document.
        /// </summary>
        public event EventHandler<C1TextChangedEventArgs> TextChanged;
        void OnTextChanged(C1TextChangedEventArgs args)
        {
            if (TextChanged != null)
            {
                if (!_isIgnoreTextChanged)
                {
                    TextChanged(this, args);
#if WPF || WINRT
                    UpdateDependecyProperty(TextProperty);
                    UpdateDependecyProperty(HtmlProperty);
#endif
                }
                else
                {
                    if (_c1TextChangedEventArgs == null)
                    {
                        _c1TextChangedEventArgs = args;
                    }
                    _textChangedTimes++;
                }
            }
            else
            {
#if WPF || WINRT
                UpdateDependecyProperty(TextProperty);
                UpdateDependecyProperty(HtmlProperty);
#endif
            }
        }

        /// <summary>
        /// Identifies the <see cref="Text"/> dependency property. 
        /// </summary>
        public static readonly DependencyProperty TextProperty = CalculatedDependencyProperty.Register<string, C1RichTextBox>("Text",
            (rtb, value) =>
            {
#if WPF || WINRT
                rtb.UpdateDependecyProperty(HtmlProperty);
#endif
                value = value ?? string.Empty;
                var paragraph = new C1Paragraph();
                var document = new C1Document { paragraph };
                for (int i = 0; i < value.Length; i += runSplitLength)
                {
                    paragraph.Inlines.Add(new C1Run { Text = value.Substring(i, Math.Min(runSplitLength, value.Length - i)) });
                }
                if (paragraph.Inlines.Count == 0)
                {
                    paragraph.Inlines.Add(new C1Run());
                }
                rtb.Document = document;
                //rtb.Selection = new C1TextRange(rtb.Document.ContentStart, rtb.Document.ContentStart);
            },
            rtb =>
            {
                return rtb.Document.ContentRange.Text;
            });

        /// <summary>
        /// Gets or sets the text inside the <see cref="C1Document"/> of the <see cref="C1RichTextBox"/>.
        /// </summary>
        /// <remarks>
        /// <para>When setting the range text, all line breaks (\n, \r and \r\n) are converted to a single new line character (\n).</para>
        /// </remarks>
        public string Text
        {
            get
            {
                return CalculatedDependencyProperty.GetValue<string>(this, TextProperty);
            }
            set
            {
                // force dependency property update, otherwise if the text dependency property
                // has the same value that is being set, on changed is never called
                CalculatedDependencyProperty.Update(this, TextProperty, true);

                SetValue(TextProperty, value);
            }
        }

        /// <summary>
        /// Fires when <see cref="Selection"/> changes.
        /// </summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// Gets or sets the <see cref="C1TextRange"/> selected in the <see cref="C1RichTextBox"/>'s document.
        /// </summary>
        public C1TextRange Selection
        {
            get
            {
                var range = new C1TextRange(new C1TextPointer(_selection.Start), new C1TextPointer(_selection.End));
                range.IsTableSelected = _selection.IsTableSelected;
                range.SelectedColumnStartIndex = _selection.SelectedColumnStartIndex;
                range.SelectedColumnEndIndex = _selection.SelectedColumnEndIndex;
                range.IsRowsSelected = _selection.IsRowsSelected;
                return range;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                SetSelection(value, false);
            }
        }

        private IEnumerable<C1TextRange> _editRanges;
        /// <summary>
        /// Gets the editable ranges in selection.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IEnumerable<C1TextRange> EditRanges
        {
            get
            {
                return _editRanges;
            }
        }

        /// <summary>
        /// Identifies the <see cref="Html"/> dependency property. 
        /// </summary>
        public static readonly DependencyProperty HtmlProperty = CalculatedDependencyProperty.Register<string, C1RichTextBox>("Html",
            (rtb, value) =>
            {
                rtb.Document = rtb.HtmlFilter.ConvertToDocument(value ?? string.Empty);
#if WPF || WINRT
                rtb.UpdateDependecyProperty(TextProperty);
#endif
            },
            rtb =>
            {
                return rtb.HtmlFilter.ConvertFromDocument(rtb.Document, HtmlEncoding.StyleSheet);
            });

        /// <summary>
        /// Gets or sets the content of the <see cref="C1RichTextBox"/> in HTML format.
        /// </summary>
        /// <remarks>
        /// The HTML returned when getting uses <see cref="HtmlEncoding.StyleSheet" /> encoding.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public string Html
        {
            get
            {
                return CalculatedDependencyProperty.GetValue<string>(this, HtmlProperty);
            }
            set
            {
                SetValue(HtmlProperty, value);
            }
        }

        /// <summary>
        /// Get the HTML content with the selected encoding.
        /// </summary>
        /// <param name="encoding">The <see cref="HtmlEncoding"/> to use.</param>
        /// <returns>A string containing an HTML version of the <see cref="C1RichTextBox"/> contents.</returns>
        public string GetHtml(HtmlEncoding encoding)
        {
            return HtmlFilter.ConvertFromDocument(Document, encoding);
        }

        /// <summary>
        /// Sets the HTML content.
        /// </summary>
        /// <param name="html">A string containing HTML content.</param>
        /// <param name="baseUri">The base <see cref="Uri"/> used to resolve links and image sources.</param>
        public void SetHtml(string html, Uri baseUri)
        {
            Document = HtmlFilter.ConvertToDocument(html ?? string.Empty, baseUri);
        }

        /// <summary>
        /// Selects all the text in the control.
        /// </summary>
        public void SelectAll()
        {
            _selectionExtend = LogicalDirection.Forward;
            Selection = new C1TextRange(FirstInsertionPoint, LastInsertionPoint);
        }

        /// <summary>
        /// Returns the closest text position to a given point in the document layout.
        /// </summary>
        /// <param name="point">A <see cref="Point"/>.</param>
        /// <returns>The closest text position to a given point in the document layout.</returns>
        public C1TextPointer GetPositionFromPoint(Point point)
        {
            if (_viewManager == null)
            {
                return Document.ContentStart;
            }
            point = _viewManager.GetPointFromRelativePoint(this, point);
            return _viewManager.GetPositionFromPoint(point);
        }

        /// <summary>
        /// Returns the <see cref="C1TextElement"/> directly below a given point.
        /// </summary>
        /// <param name="point">A <see cref="Point"/>.</param>
        /// <returns>The <see cref="C1TextElement"/> directly below a given point.</returns>
        public C1TextElement GetElementFromPoint(Point point)
        {
            if (_viewManager == null)
            {
                return Document;
            }
            point = _viewManager.GetPointFromRelativePoint(this, point);
            return _viewManager.GetElementFromPoint(point);
        }

        /// <summary>
        /// Returns the <see cref="Rect"/> of screen coordinates for a given position.
        /// </summary>
        /// <param name="position">A <see cref="C1TextPointer"/>.</param>
        /// <returns>The <see cref="Rect"/> of screen coordinates for a given position.</returns>
        public Rect GetRectFromPosition(C1TextPointer position)
        {
            if (_viewManager == null)
            {
                return new Rect();
            }
            var rect = _viewManager.GetRectFromPosition(position);
            var presenter = _viewManager.GetPresenterAtHeight(rect.Y);
            if (presenter.Instance == null)
            {
                return new Rect();
            }

            Point point;
            try
            {
#if WINRT
                point = presenter.Instance.TransformToVisual(this).TransformPoint(new Point(rect.X, rect.Y));
#else
                point = presenter.Instance.TransformToVisual(this).Transform(new Point(rect.X, rect.Y));
#endif
            }
            catch
            {
                return Rect.Empty;
            }
            return new Rect(point, new Size(rect.Width, rect.Height));
        }

#if WINRT
        /// <summary>
        /// Fires when the tapped a text element.
        /// </summary>
#else
        /// <summary>
        /// Fires when the tapped a text element.
        /// </summary>
#endif
#if WINRT
        public event TappedEventHandler ElementTapped;
        void OnElementTapped(C1TextElement el, TappedRoutedEventArgs args)
        {
            if (ElementTapped != null)
            {
                ElementTapped(el, args);
            }
        }
#else
        public event MouseButtonEventHandler ElementMouseLeftButtonDown;
        void OnElementMouseLeftButtonDown(C1TextElement el, MouseButtonEventArgs args)
        {
            if (ElementMouseLeftButtonDown != null)
            {
                ElementMouseLeftButtonDown(el, args);
            }
        }
#endif

        /// <summary>
        /// Fires when the left mouse button is released over a text element.
        /// </summary>
#if WINRT
        public event PointerEventHandler ElementPointerReleased;
        void OnElementPointerReleased(C1TextElement el, PointerRoutedEventArgs args)
        {
            if (ElementPointerReleased != null)
            {
                ElementPointerReleased(el, args);
            }
        }
#else
        public event MouseButtonEventHandler ElementMouseLeftButtonUp;
        void OnElementMouseLeftButtonUp(C1TextElement el, MouseButtonEventArgs args)
        {
            if (ElementMouseLeftButtonUp != null)
            {
                ElementMouseLeftButtonUp(el, args);
            }
        }
#endif

        /// <summary>
        /// Fires when the mouse moves over a text element.
        /// </summary>
#if WINRT
        public event PointerEventHandler ElementPointerMoved;
        void OnElementPointerMoved(C1TextElement el, PointerRoutedEventArgs args)
        {
            if (ElementPointerMoved != null)
            {
                ElementPointerMoved(el, args);
            }
        }
#else
        public event MouseEventHandler ElementMouseMove;
        void OnElementMouseMove(C1TextElement el, MouseEventArgs args)
        {
            if (ElementMouseMove != null)
            {
                ElementMouseMove(el, args);
            }
        }
#endif

        /// <summary>
        /// Fires when text dragging starts. Allows canceling the drag.
        /// </summary>
        public event EventHandler<CancelEventArgs> TextDragStart;
        void OnTextDragStart(CancelEventArgs args)
        {
            if (TextDragStart != null)
            {
                TextDragStart(this, args);
            }
        }
        /// <summary>
        /// Fires when the mouse moves while dragging text.
        /// </summary>
        public event EventHandler<TextDragMoveEventArgs> TextDragMove;
        void OnTextDragMove(TextDragMoveEventArgs args)
        {
            if (TextDragMove != null)
            {
                TextDragMove(this, args);
            }
        }
        /// <summary>
        /// Fires when dragged text is dropped.
        /// </summary>
        public event EventHandler<TextDropEventArgs> TextDrop;
        void OnTextDrop(TextDropEventArgs args)
        {
            if (TextDrop != null)
            {
                TextDrop(this, args);
            }
        }

        /// <summary>
        /// Fired when the user completes a selection using a mouse.
        /// </summary>
        public event EventHandler MouseSelectionCompleted;
        void OnMouseSelectionCompleted()
        {
            if (MouseSelectionCompleted != null) MouseSelectionCompleted(this, EventArgs.Empty);
        }

        /// <summary>
        /// Fires when the user clicks on a <see cref="C1Hyperlink"/>.
        /// </summary>
        public event EventHandler<RequestNavigateEventArgs> RequestNavigate;

        /// <summary>
        /// Indicates when the <see cref="RequestNavigate"/> event is fired.
        /// </summary>
        public NavigationMode NavigationMode { get; set; }

        /// <summary>
        /// Indicates how the <see cref="C1RichTextBox"/> uses the clipboard.
        /// </summary>
        public ClipboardMode ClipboardMode { get; set; }

        /// <summary>
        /// Gets or sets the starting position of the text selected in the rich text box.
        /// </summary>
        /// <remarks>
        /// The positions represented in this property correspond to positions in the <see cref="Text"/> property's content.
        /// </remarks>
        public int SelectionStart
        {
            get { return IsUIInitialized && !this.IsDesignTime() ? Document.ContentStart.GetOffsetToPosition(Selection.Start, C1TextRange.TextTagFilter) : _selectionStart; }
            set
            {
                if (IsUIInitialized && !this.IsDesignTime())
                    Select(value, SelectionLength);
                else
                    _selectionStart = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of characters in the current selection in the rich text box.
        /// </summary>
        /// <remarks>
        /// The positions represented in this property correspond to positions in the <see cref="Text"/> property's content.
        /// </remarks>
        public int SelectionLength
        {
            get { return IsUIInitialized && !this.IsDesignTime() ? Selection.Start.GetOffsetToPosition(Selection.End, C1TextRange.TextTagFilter) : _selectionLength; }
            set
            {
                if (IsUIInitialized && !this.IsDesignTime())
                    Selection = new C1TextRange(Selection.Start, Selection.Start.GetPositionAtOffset(value, C1TextRange.TextTagFilter));
                else
                    _selectionLength = value;
            }
        }
        /// <summary>
        /// Selects a range of text in the rich text box.
        /// </summary>
        /// <param name="start">The zero-based index of the first character in the selection.</param>
        /// <param name="length">The length of the selection, in characters.</param>
        /// <remarks>
        /// The positions received by this method correspond to positions in the <see cref="Text"/> property's content.
        /// </remarks>
        public void Select(int start, int length)
        {
            Func<C1TextPointer, C1TextPointer> closestCaret = p =>
                p.Closest(p1 => p1.IsCaretPosition, (p1, p2) => Math.Abs(p1.GetOffsetToPosition(p2, C1TextRange.TextTagFilter)));
            var range = new C1TextRange(Document, start, length);
            Selection = new C1TextRange(closestCaret(range.Start), closestCaret(range.End));

            // If the target position is not in ViewPort, C1RTB should scroll to it. Like, Find/SpellCheck.
            // But I am not sure whether this change is safe or not. Comment out it and will take more deeper investigation when get customer request.
            // ScrollSelection();
        }

        /// <summary>
        /// Gets or sets the content of the current selection in the rich text box.
        /// </summary>
        public string SelectedText
        {
            get { return Selection.Text; }
            set { Selection.Text = value; }
        }

        /// <summary>
        /// Gets the collection of <see cref="IStyleOverride"/> associated with this <see cref="C1RichTextBox"/>.
        /// </summary>
        public Collection<IStyleOverride> StyleOverrides
        {
            get
            {
                return _styleOverrideMerger.Overrides;
            }
        }

        /// <summary>
        /// Gets the collection of <see cref="IRichTextPainter"/> associated with this <see cref="C1RichTextBox"/>.
        /// </summary>
        public Collection<IRichTextPainter> Painters
        {
            get
            {
                return _painterMerger.Painters;
            }
        }

        #region SpellChecker
        /// <summary>
        /// Gets or sets the <see cref="ISpellChecker"/> used for as-you-type spell checking.
        /// </summary>
        public ISpellChecker SpellChecker
        {
            get
            {
                if (_spellCheck != null)
                {
                    return _spellCheck.SpellChecker;
                }
                return null;
            }
            set
            {
                if (_spellCheck != null)
                {
                    _spellCheck.Detach();
                    _spellCheck = null;
                }
                if (value != null)
                {
                    _spellCheck = new AsYouTypeSpellCheck(this, value);
#if WINRT
                    _spellCheck.IsEnabledSpellChecker = IsSpellCheckerEnabled;
#endif
                }
            }
        }
        #endregion

        #region Clipboard
        /// <summary>
        /// Fired when a clipboard copy has been triggered.
        /// </summary>
        public event EventHandler<ClipboardEventArgs> ClipboardCopying;

#if !WINRT
        private DispatcherOperation _copyOperation = null;
        private DispatcherOperation _pasteOperation = null;
#endif

        /// <summary>
        /// Copies the selection to the clipboard.
        /// </summary>
        public void ClipboardCopy()
        {


            if (Selection.IsEmpty)
                return;
            if (ClipboardCopying != null)
            {
                var args = new ClipboardEventArgs();
                ClipboardCopying(this, args);
                if (args.Handled) return;
            }

            if (Clipboard.IsEnabled && ClipboardMode == ClipboardMode.RichText)
            {
                C1Document doc = Selection.GetFragment(true);
                doc.RichTextBox = this;
#if !WINRT
                if (_copyOperation != null)
                {
                    _copyOperation.Abort();
                    _copyOperation = null;
                }
#endif
                var html = HtmlFilter.ConvertFromDocument(doc, HtmlEncoding.Inline);
                html = Regex.Match(html, @"<body[^>]*>(.*?)</body>", RegexOptions.Singleline).Groups[1].Value.Trim();

                var htmlMatch = new Regex("<").Match(html);
                if (!htmlMatch.Success)
                {
                    html = "<p>" + html + "</p>";
                }

#if WPF || WINRT
                var rtf = _rtfFilter.ConvertFromDocument(doc);
#endif

#if !WINRT
                // dispatch clipboard call, otherwise it crashes the browser if this is called
                // from a key event handler and the browser needs to ask the user to allow clipboard access
                _copyOperation = DispatcherEx.BeginInvoke(Dispatcher, () =>
#endif
                {
                    try
                    {
                        Clipboard.SetData(html, Selection.GetText(true).Replace("\n", "\r\n"), ClipboardMode == ClipboardMode.RichText
#if WPF || WINRT
                            , rtf
#endif
                            );
                        if (_isCutOperation)
                        {
                            bool isEmpty = Selection.IsEmpty;
                            using (new DocumentHistoryGroup(DocumentHistory))
                            {
                                Selection.Delete(needDeleteTable: true);
                            }
                            if (!isEmpty && SelectionChanged != null)
                            {
                                SelectionChanged(this, EventArgs.Empty);
                            }
                            _isCutOperation = false;
                            EndTextChanged();
#if WINRT
                            Focus(FocusState.Programmatic);
#else
                            base.Focus();
#endif
                        }
                    }
                    catch (SecurityException) { }
                    catch (COMException) { }
                }
#if !WINRT
                );
#endif
            }
            else
            {
                try
                {
#if WINRT
                    Clipboard.SetData("", Selection.GetText(true).Replace("\n", "\r\n"), false);
#else
                    System.Windows.Clipboard.SetText(Selection.GetText(true).Replace("\n", "\r\n"));
#endif
                    if (_isCutOperation)
                    {
                        using (new DocumentHistoryGroup(DocumentHistory))
                        {
                            Selection.Delete();
                        }
                        _isCutOperation = false;
                        EndTextChanged();
#if WINRT
                        Focus(FocusState.Programmatic);
#else
                        base.Focus();
#endif
                    }
                }
                catch (SecurityException) { }
                catch (COMException) { }
            }
        }

        /// <summary>
        /// Cuts the selection to the clipboard.
        /// </summary>
        public void ClipboardCut()
        {
            if (Selection.IsEmpty)
                return;
            BeginTextChanged();
            using (new DocumentHistoryGroup(DocumentHistory))
            {
                _isCutOperation = true;
                ClipboardCopy();
            }
        }

        /// <summary>
        /// Fired when a clipboard paste has been triggered.
        /// </summary>
        public event EventHandler<ClipboardEventArgs> ClipboardPasting;

        /// <summary>
        /// Pastes the current content of the clipboard.
        /// </summary>
#if WINRT
        public async Task ClipboardPaste()
#else
        public void ClipboardPaste()
#endif
        {
            BeginTextChanged();
            if (ClipboardPasting != null)
            {
                var args = new ClipboardEventArgs();
                ClipboardPasting(this, args);
                if (args.Handled) return;
            }

#if WINRT
            var dpv = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent() as DataPackageView;
#endif

            if (Clipboard.IsEnabled && ClipboardMode == ClipboardMode.RichText)
            {

#if !WINRT
                if (_pasteOperation != null)
                {
                    _pasteOperation.Abort();
                    _pasteOperation = null;
                }
#endif

#if !WINRT
                // dispatch clipboard call, otherwise it crashes the browser if this is called
                // from a key event handler and the browser needs to ask the user to allow clipboard access
                _pasteOperation = DispatcherEx.BeginInvoke(Dispatcher, () =>
#endif
                {
#if WINRT
                    string html = null;
                    try
                    {
                        html = await dpv.GetHtmlFormatAsync();
                    }
                    catch
                    { }
                    if (html == null)
                    {
                        try
                        {
                            html = await dpv.GetRtfAsync();
                        }
                        catch
                        { }
                    }
                    
                    if (html == null && dpv.Contains(StandardDataFormats.Bitmap))
                    {
                        // If there is image steam in the Clipboard, we need to get it.
                        try
                        {

                            var streamRef = await Windows.ApplicationModel.DataTransfer.Clipboard.GetContent().GetBitmapAsync() as RandomAccessStreamReference;
                            var stream = await streamRef.OpenReadAsync();

                            var bytes = new byte[stream.Size];
                            using (var dataReader = new DataReader(stream))
                            {
                                await dataReader.LoadAsync((uint)stream.Size);
                                dataReader.ReadBytes(bytes);
                            }

                            if (bytes.Length > 0)
                            {
                                StringBuilder imgSource = new StringBuilder();
                                imgSource.Append("<p><img src=\"data:image/png;base64,");
                                imgSource.Append(Convert.ToBase64String(bytes));
                                imgSource.Append("\"/></p>");
                                html = imgSource.ToString();
                            }
                        }
                        catch { }
                    }

                    html = Clipboard.GetHtmlData(html, true);
#else
                    var html = Clipboard.GetClipboardData();
#endif
                    if (!string.IsNullOrEmpty(html) && ClipboardMode == RichTextBox.ClipboardMode.RichText)
                    {
                        C1Document doc = null;
#if WPF || WINRT
                        if (html.StartsWith("{"))
                            doc = _rtfFilter.ConvertToDocument(html);
                        else
#endif
                            doc = HtmlFilter.ConvertToDocument(html ?? string.Empty);

                        if (doc.Children.LastOrDefault() is C1Table)
                        {
                            doc.Children.Add(new C1Paragraph { new C1Run() });
                        }

                        SetDocument(doc);
                        this.Focus();
                    }
                    else
                    {
                        try
                        {
                            //#423009 Do not paste if rtb has no focus and no selection
                            if (Selection.End.GetPositionAtOffset(-1) == null)
                            {
                                return;
                            }

                            string text = null;
#if WINRT
                            try
                            {
                                text = await dpv.GetTextAsync();
                            }
                            catch
                            { }
#else
                            text = System.Windows.Clipboard.GetText();
#endif
                            InputClipboardText(text);
                        }
                        catch (SecurityException) { }
                        catch (COMException) { }
                    }
                    EndTextChanged();
                    this.Focus();
                }
#if !WINRT
                );
#endif
            }
            else
            {
                try
                {
                    string text = null;
#if WINRT
                    try
                    {
                        text = await dpv.GetTextAsync();
                    }
                    catch
                    {
                    }
#else
                    text = System.Windows.Clipboard.GetText();
#endif
                    InputClipboardText(text);
                    EndTextChanged();
                    this.Focus();
                }
                catch (SecurityException) { }
                catch (COMException) { }
            }
        }

        void SetDocument(C1Document doc)
        {
            using (new DocumentHistoryGroup(DocumentHistory))
            {
                if (!AcceptsReturn)
                {
                    doc.ContentRange.RemoveBreaks();
                }
                var fragmentStart = doc.ContentStart;
                var range = Selection;
                var insertPos = range.Delete(!Selection.IsEmpty);
                var parents = insertPos.Element.GetParents().Select(e => e.GetType()).ToList();
                parents.Add(insertPos.Element.GetType());
                while (fragmentStart.Symbol is StartTag
                    && (fragmentStart.Symbol as Tag).Element is C1Block
                    && !((fragmentStart.Symbol as Tag).Element is C1TableRowGroup)
                    && parents.Contains((fragmentStart.Symbol as Tag).Element.GetType()))
                {
                    fragmentStart = fragmentStart.GetPositionAtOffset(1);
                }
                var fragmentEnd = doc.ContentEnd;
                while (fragmentEnd.PreviousSymbol is EndTag)
                {
                    fragmentEnd = fragmentEnd.GetPositionAtOffset(-1);
                }
                doc.FragmentRange = new C1TextRange(fragmentStart, fragmentEnd);
                range.Fragment = doc;
                Selection = new C1TextRange(range.End);
            }
        }
#if WINRT
        async 
#endif
        void InputClipboardText(string text)
        {
            using (new DocumentHistoryGroup(DocumentHistory))
            {
                if (!string.IsNullOrEmpty(text))
                {
                    string[] list = text.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    for (int i = 0; i < list.Length; i++)
                    {
                        InputText(list[i]);
                        if (i != list.Length - 1)
                            EditExtensions.InsertHardLineBreak(Selection.Start);
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// Causes the editor to jump to a specified page number.
        /// </summary>
        /// <param name="pageNumber">The number of the page to jump to.</param>
        public void GoToPage(int pageNumber)
        {
            var itemsControl = _scrollPresenter.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null) return;
            var item = itemsControl.ItemContainerGenerator.ContainerFromIndex(pageNumber - 1) as UIElement;
            if (item == null) return;
#if WINRT
            VerticalOffset += item.TransformToVisual(_scrollPresenter).TransformPoint(new Point()).Y;
#else
            VerticalOffset += item.TransformToVisual(_scrollPresenter).Transform(new Point()).Y;
#endif
        }

        /// <summary>
        /// Copies the format of the current selection to the format clipboard.
        /// </summary>
        public void FormatCopy()
        {
            var stylePos = Selection.Start;
            while (stylePos.Symbol is Tag && (stylePos.Symbol as Tag).Element is C1Inline && stylePos < Selection.End)
            {
                stylePos = stylePos.GetPositionAtOffset(1);
            }
            _formatClip = stylePos.ClosestCaret.Element.ComputedStyle;
        }

        /// <summary>
        /// Applies the content of the format clipboard to the current selection.
        /// </summary>
        public void FormatPaste()
        {
            if (_formatClip == null) return;
            var properties = new[]
            {
                C1TextElement.BackgroundProperty,
                C1TextElement.BorderBrushProperty,
                C1TextElement.BorderThicknessProperty,
                C1TextElement.CornerRadiusProperty,
                C1TextElement.FontFamilyProperty,
                C1TextElement.FontSizeProperty,
                C1TextElement.FontStyleProperty,
                C1TextElement.FontWeightProperty,
                C1TextElement.ForegroundProperty,
                C1TextElement.TextDecorationsProperty,
                C1TextElement.TextEffectProperty,
            };
            var range = Selection.IsEmpty ? GetWord(Selection.Start) : Selection;
            foreach (var property in properties)
            {
                object value;
                if (!_formatClip.TryGetValue(property, out value))
                {
                    value = property.DefaultValue;
                }
                range.SetRunsValue(property, value);
            }
        }

        /// <summary>
        /// Boldface the selected text, or turn boldfacing on or off. 
        /// </summary>
        void BoldfaceSelection()
        {
            using (new DocumentHistoryGroup(DocumentHistory))
            {
                foreach (var r in Selection.ExactEditRanges)
                {
                    if (r.FontWeight.Equals(FontWeights.Bold))
                        r.FontWeight = FontWeights.Normal;
                    else
                        r.FontWeight = FontWeights.Bold;
                }
            }
        }

        /// <summary>
        /// Italicize the selected text, or turn italics on or off.
        /// </summary>
        void ItalicizeSelection()
        {
            using (new DocumentHistoryGroup(DocumentHistory))
            {
                foreach (var r in Selection.ExactEditRanges)
                {
#if WINRT
                if (r.FontStyle.Equals(FontStyle.Italic))
                    r.FontStyle = FontStyle.Normal;
                else
                    r.FontStyle = FontStyle.Italic;
#else
                    if (r.FontStyle.Equals(FontStyles.Italic))
                        r.FontStyle = FontStyles.Normal;
                    else
                        r.FontStyle = FontStyles.Italic;
#endif
                }
            }
        }

        /// <summary>
        /// Underline the selected text, or turn underlining on or off.
        /// </summary>
        void UnderlineSelection()
        {
            using (new DocumentHistoryGroup(DocumentHistory))
            {
                bool isUnderline = Selection.Runs.Any() && Selection.Runs.All(r => r.TextDecorations != null && r.TextDecorations.Contains(C1TextDecorations.Underline[0]));
                Selection.TrimRuns();
                foreach (var range in Selection.ExactEditRanges)
                {
                    foreach (var run in range.Runs)
                    {
                        var collection = new C1TextDecorationCollection();
                        if (isUnderline)
                        {
                            if (run.TextDecorations != null && run.TextDecorations.Count > 0 && run.TextDecorations.Contains(C1TextDecorations.Underline[0]))
                            {
                                foreach (var decoration in run.TextDecorations)
                                    collection.Add(decoration);
                                collection.Remove(C1TextDecorations.Underline[0]);
                                if (collection.Count == 0)
                                    collection = null;
                            }
                        }
                        else
                        {
                            if (run.TextDecorations == null)
                            {
                                collection.Add(C1TextDecorations.Underline[0]);
                            }
                            else
                            {
                                foreach (var decoration in run.TextDecorations)
                                    collection.Add(decoration);
                                if (!collection.Contains(C1TextDecorations.Underline[0]))
                                    collection.Add(C1TextDecorations.Underline[0]);
                            }
                        }
                        run.TextDecorations = null;
                        run.TextDecorations = collection;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to set the focus on the control.
        /// </summary>
        /// <returns>
        /// true if focus was set to the control, or focus was already on the control.
        /// false if the control is not focusable.
        /// </returns>
        public new bool Focus(
#if WINRT
            FocusState value
#endif
            )
        {
            if (this._txtBox != null)
            {
#if WINRT
                return this._txtBox.Focus(value);
#else
                return this._txtBox.Focus();
#endif
            }

#if WINRT
            return base.Focus(value);
#else
            return base.Focus();
#endif
        }
        #endregion

        #region property change handlers

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly")]
        partial void OnDocumentChanged(C1Document oldDocument, C1Document newValue)
        {
#if WINRT
            if (Document == null || Document.Children.Count == 0)
#else
            if (Document == null)
#endif
            {
                // If the Document property was changed from an instance(called doc1) to null, the TextChanging event would never removed.
                // So we need to remove the TextChanging event of the oldDocument before we set new value to Document.
                if (oldDocument != null)
                {
                    oldDocument.TextChanging -= OnDocumentTextChanging;
                    _documentHistory.Dispose();
                    oldDocument.RichTextBox = null;
                    if (oldDocument.ContentRange.Text.Length > 0)
                        _selectionStart = _selectionLength = 0;
                }
                if (!IsUIInitialized && this.ReadLocalValue(DocumentProperty) is BindingExpressionBase)
                {
                    _documentBindingExpression = ((BindingExpression)(ReadLocalValue(DocumentProperty))).ParentBinding;
                }
                Document = new C1Document { new C1Paragraph { new C1Run() } };
                return;
            }
            Document.RichTextBox = this;

            if (IsUIInitialized)
            {
                // Only update font properties after the template is applied to allow Style values to take effect.
                UpdateFontProperties();
            }

            Document.TextChanging += OnDocumentTextChanging;

            if (_elementContent != null)
            {
                CreateViewManager();
            }

            _documentHistory = new DocumentHistory(Document, () => Selection, s => Selection = s);
            bool isStartPosition = true;
            if (_selectionStart != 0 || _selectionLength != 0)
            {
                if (!this.IsDesignTime())
                {
                    if (Text.Length > 0)
                        isStartPosition = false;
                }
            }

            if (isStartPosition)
                Selection = new C1TextRange(Document.ContentStart, Document.ContentStart);
            else
                Select(_selectionStart, _selectionLength);

            _elementHover = null;
#if WINDOWS_APP || UWP
            _uiElementHover = null;
#endif
#if !WINRT
            _positionHover = null;
#endif

            if (Document != oldDocument && DocumentChanged != null)
            {
                DocumentChanged(this, EventArgs.Empty);
            }
        }

        partial void OnIsFocusedChanged()
        {
            UpdateSelectionVisibility();
        }

        void OnIsEnabledChanged()
        {
#if WINRT
            if (_txtBox != null)
            {
                _txtBox.IsTabStop = IsEnabled;
                _txtBox.IsReadOnly = IsReadOnly || !IsEnabled;
            }
#endif
            if (_elementContent != null)
            {
                _elementContent.IsHitTestVisible = IsEnabled;
            }
            UpdateSelectionVisibility();
        }

        partial void OnIsReadOnlyChanged()
        {
#if WINRT
            if (_txtBox != null)
            {
                _txtBox.IsReadOnly = IsReadOnly || !IsEnabled;
            }
#endif
            UpdateSelectionVisibility();
            if (IsReadOnlyChanged != null)
            {
                IsReadOnlyChanged(this, EventArgs.Empty);
            }
            ChangeVisualStateWritable(true);
        }

        partial void OnHideSelectionChanged()
        {
            UpdateSelectionVisibility();
        }

        partial void OnHideCaretChanged()
        {
            UpdateSelectionVisibility();
        }

        partial void OnDisableSelectionChanged()
        {
            UpdateSelectionVisibility();
        }

        partial void OnSelectionBackgroundChanged()
        {
            if (_resizePainter.Element == null)
            {
                _selectionPainter.Background = SelectionBackground;
                Redraw();
            }
        }

        partial void OnUIContainerResizeTemplateChanged()
        {
            _resizePainter.ResizeTemplate = UIContainerResizeTemplate;
            Redraw();
        }

        partial void OnSelectionForegroundChanged()
        {
            if (_viewManager != null)
            {
                _viewManager.InvalidateLayout(Selection);
            }
            Redraw();
        }

        partial void OnTabSizeChanged()
        {
            if (_viewManager != null)
            {
                _viewManager.TabSize = TabSize;
                _viewManager.InvalidateLayout(Document.ContentRange);
            }
        }

        partial void OnViewModeChanged()
        {
            if (_scrollPresenter != null)
            {
                CreateViewManager();
            }
        }

        partial void OnLineNumberModeChanged()
        {
            if (_scrollPresenter != null)
            {
                CreateViewManager();
            }
        }

        partial void OnPrintTemplateChanged()
        {
            if (_scrollPresenter != null)
            {
                CreateViewManager();
            }
        }

        partial void OnPrintPanelTemplateChanged()
        {
            if (_scrollPresenter != null)
            {
                CreateViewManager();
            }
        }

        partial void OnPrintPageLayoutChanged()
        {
            if (_viewManager != null)
            {
                _viewManager.PresenterInfo = PrintPageLayout;
            }
        }

        partial void OnZoomChanged()
        {
            if (PrintPageLayout != null)
            {
                PrintPageLayout.Zoom = Zoom;
            }
        }

        /// <summary>
        /// Fired when CaretBrush is changed.
        /// </summary>
        /// <param name="old">The old value of the CaretBrush</param>
        partial void OnCaretBrushChanged()
        {
            _selectionPainter.CaretBrush = CaretBrush;
        }

#if WINDOWS_APP || UWP
        void OnIsSpellCheckerEnabledChanged()
#else
        void OnIsEnabledSpellCheckerChanged()
#endif
        {
#if WINDOWS_APP || UWP
            if (_spellCheck != null)
                _spellCheck.IsEnabledSpellChecker = IsSpellCheckerEnabled;
#endif
        }

        partial void OnPlaceholderChanged()
        {
            SetWatermark(!IsTextFocused);
        }
        partial void OnDefaultParagraphMarginChanged()
        {
            var newThickness = DefaultParagraphMargin;
            if (!double.IsNaN(DefaultParagraphMargin.Left))
            {
                newThickness.Left = DefaultParagraphMargin.Left;
            }
            if (!double.IsNaN(DefaultParagraphMargin.Right))
            {
                newThickness.Right = DefaultParagraphMargin.Right;
            }
            if (!double.IsNaN(DefaultParagraphMargin.Top))
            {
                newThickness.Top = DefaultParagraphMargin.Top;
            }
            if (!double.IsNaN(DefaultParagraphMargin.Bottom))
            {
                newThickness.Bottom = DefaultParagraphMargin.Bottom;
            }

            if (!DefaultParagraphMargin.Equals(newThickness))
                DefaultParagraphMargin = newThickness;

            if (Document != null)
            {
#if !WINRT
                _isIgnoreTextChanged = true;
#endif
                foreach (var block in Document.Blocks)
                {
                    if (block is C1Paragraph && !block.HasSetMargin)
                    {
                        if (newThickness != block.Margin)
                        {
                            block.Margin = newThickness;
                            block.HasSetMargin = false;
                        }
                    }
                }
#if !WINRT
                _isIgnoreTextChanged = false;
#endif
            }
        }

        #endregion

        #region implementation

        void Redraw()
        {
            if (_viewManager != null)
            {
                _viewManager.InvalidatePaint();
            }
        }

        internal void CreateViewManager()
        {
#if WINRT
            // Fixed the exception in designer when drop C1RTB onto the page.
            if (this.IsDesignTime())
                return;
#endif
            if (_viewManager != null)
            {
                // clear style overrides and painters to avoid memory leaks
                var styleMerger = _viewManager.StyleOverride as C1StyleOverrideMerger;
                if (styleMerger != null) styleMerger.Overrides.Clear();
                var painterMerger = _viewManager.Painter as C1PainterMerger;
                if (painterMerger != null) painterMerger.Painters.Clear();
                ((INotifyCollectionChanged)_viewManager.Presenters).CollectionChanged -= OnPresentersCollectionChanged;
                _viewManager.Dispose();
            }

            if (Document.PrintPageLayout != null)
            {
                // If the PrintPageLayout of Document has been set,
                // we should clone its settings to the PrintPageLayout instance of C1RichTextBox.
                if (PrintPageLayout.Background != Document.PrintPageLayout.Background)
                    PrintPageLayout.Background = Document.PrintPageLayout.Background;
                if (PrintPageLayout.Height != Document.PrintPageLayout.Height)
                    PrintPageLayout.Height = Document.PrintPageLayout.Height;
                if (PrintPageLayout.Width != Document.PrintPageLayout.Width)
                    PrintPageLayout.Width = Document.PrintPageLayout.Width;
                if (PrintPageLayout.Margin != Document.PrintPageLayout.Margin)
                    PrintPageLayout.Margin = Document.PrintPageLayout.Margin;
                if (PrintPageLayout.Padding != Document.PrintPageLayout.Padding)
                    PrintPageLayout.Padding = Document.PrintPageLayout.Padding;
                if (PrintPageLayout.Zoom != Document.PrintPageLayout.Zoom)
                    PrintPageLayout.Zoom = Document.PrintPageLayout.Zoom;
            }

            _viewManager = new C1RichTextViewManager
            {
                Document = Document,
                TabSize = TabSize,
                StyleOverride = new C1StyleOverrideMerger
                {
                    Overrides =
                    {
                        _styleOverrideMerger,
                        new DelegateStyleOverride(SelectionStyleOverride)
                    }
                },
                Painter = new C1PainterMerger
                {
                    Painters =
                    {
                        _painterMerger,
                        _selectionPainter,
                        _resizePainter,
                    }
                },
                // We should always keep using PrintPageLayout of C1RichTextBox to render layout.
                // Otherwise, the settings of PrintPageLayout will not take any effect after they are changed.
                //PresenterInfo = Document.PrintPageLayout ?? PrintPageLayout
                PresenterInfo = PrintPageLayout,
                LineNumberMode = LineNumberMode,
            };
            UpdatePageNumber();
            UpdatePageCount();
            ((INotifyCollectionChanged)_viewManager.Presenters).CollectionChanged += OnPresentersCollectionChanged;

            _scrollPresenter.Children.Clear();
            FrameworkElement presenter = null;
            if (_elementPlaceholder != null)
            {
                _elementPlaceholder.ClearValue(Control.MarginProperty);
            }
            switch (ViewMode)
            {
                case TextViewMode.Draft:
                    presenter = new C1LayoutTransformer
                    {
                        Content = new C1RichTextPresenter { Source = ViewManager.Presenters.First() }
                    };
                    (presenter as C1LayoutTransformer).SetBinding(C1LayoutTransformer.LayoutTransformProperty,
                        new Binding { Converter = new ZoomToScaleTransformConverter() }.From(this, x => x.Zoom));
                    if (_elementPlaceholder != null)
                    {
                        _elementPlaceholder.SetBinding(Control.MarginProperty, new Binding().From(this, x => x.Padding));
                    }
                    break;
                case TextViewMode.Print:
                    presenter = new ItemsControl
                    {
                        ItemTemplate = PrintTemplate,
                        ItemsSource = _viewManager.Presenters,
                        ItemsPanel = PrintPanelTemplate
                    };
                    if (_elementPlaceholder != null)
                    {
                        _elementPlaceholder.SetBinding(Control.MarginProperty, new Binding().From(this, x => x.PrintPageLayout.Padding));
                    }
                    break;
            }
            if (presenter != null)
            {
                presenter.SetBinding(FrameworkElement.VerticalAlignmentProperty, new Binding().From(this, x => x.VerticalContentAlignment));
                presenter.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new Binding().From(this, x => x.HorizontalContentAlignment));
                _scrollPresenter.Children.Add(presenter);
                SetWatermark(!IsTextFocused);
            }
        }

        void OnPresentersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdatePageCount();
        }

        IEnumerable<C1RangeStyle> SelectionStyleOverride(C1TextRange range)
        {
            if (!Selection.IsEmpty && DrawSelection && SelectionForeground != null)
            {
                var style = new C1TextElementStyle();
                style[C1TextElement.ForegroundProperty] = SelectionForeground;
                foreach (var r in _editRanges)
                {
                    yield return new C1RangeStyle(r, style);
                }
            }
        }

        internal bool IsTextFocused
        {
            get
            {
#if WINRT
                return _textBoxIsFocused;
#else
                var focusedElement = this.C1GetLogicalFocusedElement();
                return focusedElement != null && this != null && (focusedElement == this || focusedElement == _txtBox);
#endif
            }
        }

        bool DrawSelection
        {
            get
            {
                return IsEnabled && (IsTextFocused || !HideSelection) && !DisableSelection;
            }
        }

        void OnDocumentTextChanging(object sender, C1TextChangedEventArgs args)
        {
            var oldSelection = Selection;
            Action fixSelection = () =>
            {
                if (oldSelection != Selection || !Selection.Start.IsCaretPosition || !Selection.End.IsCaretPosition)
                {
                    SetSelection(Selection, true);
                }
            };

            args.OnChanged(() =>
            {
                fixSelection();
                OnTextChanged(args);
            });
        }

        void UpdateSelectionVisibility()
        {
            this.UpdateSelectionVisibility(IsEnabled && IsTextFocused && !HideCaret && !DisableSelection);
        }

        void UpdateSelectionVisibility(bool paintCaret)
        {
#if WINRT
            if (_resizePainter.Element == null)
            {
#endif
            _selectionPainter.PaintCaret = paintCaret;
#if WINRT
#if WINDOWS_APP || UWP
                if (_isTouchInput)
#endif
                    _needsShowThumbs = paintCaret;
            }
#endif
            _selectionPainter.PaintSelection = DrawSelection;
            if (_viewManager != null)
            {
                _viewManager.InvalidateLayout(Selection);
            }
        }

#if !WINRT
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToolTipHelper.CloseToolTip(e);
            if (MouseLeftButtonDown != null)
            {
                MouseLeftButtonDown(this, e);
                if (e.Handled)
                    return;
            }
            //Focus();
            this._txtBox.Focus();
            var point = GetPosition(e);
            var current = GetSpecialLanguageStartPoint(_viewManager.GetPositionFromPoint(point), LogicalDirection.Forward);
            _dragged = false;
            _dragTargetPainter = null;
            var element = _viewManager.GetElementFromPoint(point);

            var prevElement = current.Element.PreviousSibling as C1Run;
            var currentElement = current.Element as C1Run;
            bool isFirstRun = current.Offset == 0 && currentElement != null;
            if (isFirstRun)
            {
                C1TextElement nextElement = null;

                if (!string.IsNullOrEmpty(currentElement.Text) && prevElement != null && !string.IsNullOrEmpty(prevElement.Text))
                {
                    current = new C1TextPointer(prevElement, prevElement.Text.Length);
                    element = prevElement;
                }
                else if (string.IsNullOrEmpty(currentElement.Text) && (nextElement = currentElement.NextSibling) != null)
                {
                    current = new C1TextPointer(nextElement, 0);
                    element = nextElement;
                }
            }

            if (Selection.Contains(current) && IsEnabled && !IsReadOnly)
            {
                var eventArgs = new CancelEventArgs();
                OnTextDragStart(eventArgs);
                if (!eventArgs.Cancel)
                {
                    _dragTargetPainter = new PlaceholderPainter();
                }
            }

            if (_dragTargetPainter == null)
            {
                if (element is C1InlineUIContainer || element is C1BlockUIContainer)
                {
                    Selection = element.ContentRange;
                }
                else
                {
                    var table = element.GetClosestParent(el => el is C1Table);
                    if (_resizePainter.Element is C1Table)
                    {
                        // If the element is for table, show the resizer element.
                        _resizePainter.Element = null;
                        _selectionPainter.Background = SelectionBackground;
                        _selectionPainter.PaintCaret = true;
                        Redraw();
                    }
                    else if (table != null)
                    {
                        _isTableResized = true;
                        _selectionPainter.PaintCaret = false;
                    }
                    MoveSelection(current, KeyboardUtil.Shift);
                }
            }

            OnElementMouseLeftButtonDown(element, e);

            // need to handle mouseleftbuttondown or the focus is stolen when the richtextbox is inside a ContentControl (SL3)
            e.Handled = true;
        }
#endif

#if WINRT
        void OnPointerReleased(object sender, PointerRoutedEventArgs e)
#else
        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
#endif
        {
            var point = GetPosition(e);
#if !WINRT
            if (!_dragged)
            {
                if (_dragTargetPainter != null)
                {
                    MoveSelection(_viewManager.GetPositionFromPoint(point), KeyboardUtil.Shift);
                }
                else
                {
                    OnMouseSelectionCompleted();
                }
            }
            OnElementMouseLeftButtonUp
#else
            OnElementPointerReleased            
#endif
            (_viewManager.GetElementFromPoint(point), e);
        }

#if WINRT
        void OnPointerMoved(object sender, PointerRoutedEventArgs e)
#else
        void OnMouseMove(object sender, MouseEventArgs e)
#endif
        {
            ToolTipHelper.OnMouseMove(e);

            var point = GetPosition(e);
            var element = _viewManager.GetElementFromPoint(point);
#if WINRT
            _isTouchInput = e.Pointer.PointerDeviceType == PointerDeviceType.Touch;
            OnElementPointerMoved(element, e);
#else
            OnElementMouseMove(element, e);
#endif
            if (element != _elementHover)
            {
#if WINRT
                OnElementPointerExited(e);
#else
                OnElementMouseLeave(e);
#endif
                _elementHover = element;
#if WINRT
                _uiElementHover = e.OriginalSource as UIElement;
                OnElementPointerEnter(e);
#else
                OnElementMouseEnter(e);
#endif
            }
            _positionHover = _viewManager.GetPositionFromPoint(point);
#if WINRT
            if (!_resizePainter.HasSettedCursor)
#endif
            UpdateCursor();
        }

#if WINRT
        void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            OnElementPointerExited(e);
#else
        void OnMouseLeave(object sender, MouseEventArgs e)
        {
            OnElementMouseLeave(e);
#endif
            _elementHover = null;
#if WINRT
            _uiElementHover = null;
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 3);
            _elementHorizontalScrollBar.IndicatorMode = ScrollingIndicatorMode.None;
            _elementVerticalScrollBar.IndicatorMode = ScrollingIndicatorMode.None;
#endif
        }

#if WINRT
        void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
#else
        void OnDoubleTapped(object sender, C1TappedEventArgs e)
#endif
        {
            if (e.PointerDeviceType == C1PointerDeviceType.Touch)
            {
                //C1XAML-26625 the 2nd click of WPF using mouse doesn't have PointerReleased event because it has e.Handled=true in below line.
                //It was from a very old bug TFS-35357 which fixed issue with Touch. So I added condition to only set e.Handled=true when using touch.
                //Touch has different flow with mouse, even e.Handled=true it still has fire PointerReleased event.
                e.Handled = true;
            }
#if WINRT
            _isTouchInput = e.PointerDeviceType == PointerDeviceType.Touch;
            if (!_isTouchInput)
                return;

            _needsShowThumbs = true;
            var point = e.GetPosition(this);
#else
            Point point;
            if (e.PointerDeviceType == C1PointerDeviceType.Touch)
            {
                point = GetPosition(e.OriginalEventArgs as TouchEventArgs);
            }
            else
            {
                point = GetPosition(e.OriginalEventArgs as MouseButtonEventArgs);
            }
#endif
            Selection = GetWord(_viewManager.GetPositionFromPoint(point));
        }

        bool IsCtrl(Key key)
        {
#if WINRT
            return key == Key.Control || key == Key.LeftControl || key == Key.RightControl;
#else
            return key == Key.LeftCtrl || key == Key.RightCtrl;
#endif
        }


        void UpdateCursor()
        {
            if (_elementHover == null)
            {
#if WINRT
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.IBeam, 3);
#else
                _elementContent.Cursor = null;
                if (_elementContainer != null)
                {
                    _elementContainer.Cursor = null;
                }
#endif
                return;
            }

            var hyperlink = _elementHover.GetParents().Concat(new[] { _elementHover }).FirstOrDefault(e => e is C1Hyperlink) as C1Hyperlink;
            if (hyperlink != null && (NavigationMode == NavigationMode.Always
#if !WINRT
                || (NavigationMode == NavigationMode.OnControlKey && KeyboardUtil.Command)
#endif
))
            {
#if WINRT
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 2);
#else
                _elementContent.Cursor = Cursors.Hand;
                if (_elementContainer != null)
                {
                    _elementContainer.Cursor = Cursors.Hand;
                }
#endif
            }
            else if (_elementHover.Cursor == null && _positionHover != null && Selection.Contains(_positionHover) && !_dragged)
            {
#if WINRT
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
#else
                _elementContent.Cursor = Cursors.Arrow;
                if (_elementContainer != null)
                {
                    _elementContainer.Cursor = Cursors.Arrow;
                }
#endif
            }
            else
            {
#if WINRT
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.IBeam, 3);
#else
                _elementContent.Cursor = _elementHover.Cursor;
                if (_elementContainer != null)
                {
                    _elementContainer.Cursor = _elementHover.Cursor;
                }
#endif
            }
        }

        /// <summary>
        /// Fires when the mouse enters a text element.
        /// </summary>
#if WINRT
        public event PointerEventHandler ElementPointerEnter;
        void OnElementPointerEnter(PointerRoutedEventArgs args)
#else
        public event MouseEventHandler ElementMouseEnter;
        void OnElementMouseEnter(MouseEventArgs args)
#endif
        {
            if (_elementHover != null)
            {
                if (_elementHover.ToolTip != null && _elementHover.Visibility == C1Visibility.Visible)
                {
                    ToolTipHelper.OnMouseEnter(_elementHover, _elementHover.ToolTip
#if WINRT
, _uiElementHover ?? this
#endif
);
                }
#if WINRT
                if (ElementPointerEnter != null)
                {
                    ElementPointerEnter(_elementHover, args);
                }
#else
                if (ElementMouseEnter != null)
                {
                    ElementMouseEnter(_elementHover, args);
                }
#endif
            }
        }

        /// <summary>
        /// Fires when the mouse leaves a text element.
        /// </summary>
#if WINRT
        public event PointerEventHandler ElementPointerExited;
        void OnElementPointerExited(PointerRoutedEventArgs args)
#else
        public event MouseEventHandler ElementMouseLeave;
        void OnElementMouseLeave(MouseEventArgs args)
#endif
        {
            if (_elementHover != null)
            {
                if (_elementHover.ToolTip != null && _elementHover.Visibility == C1Visibility.Visible)
                {
                    ToolTipHelper.OnMouseLeave();
                }
#if WINRT
                if (ElementPointerExited != null)
                {
                    ElementPointerExited(_elementHover, args);
                }
#else
                if (ElementMouseLeave != null)
                {
                    ElementMouseLeave(_elementHover, args);
                }
#endif
            }
        }

#if WINDOWS_APP || UWP
        void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var pdt = e.Pointer.PointerDeviceType;
            _isTouchInput = pdt == PointerDeviceType.Touch;
            _elementHorizontalScrollBar.IndicatorMode = _isTouchInput ? ScrollingIndicatorMode.TouchIndicator : ScrollingIndicatorMode.MouseIndicator;
            _elementVerticalScrollBar.IndicatorMode = _isTouchInput ? ScrollingIndicatorMode.TouchIndicator : ScrollingIndicatorMode.MouseIndicator;
        }

        /// <summary>
        /// when a right-tap input stimulus happens while the pointer is over the <see cref="C1RichTextBox"/>.
        /// </summary>
        public event EventHandler<ContextMenuOpeningEventArgs> ContextMenuOpening;

        void OnRightTapped(object sender, C1TappedEventArgs e)
        {
            var arg = e.OriginalEventArgs as RightTappedRoutedEventArgs;
            // Change the selection when right tapped if the selection is empty.
            if (Selection.IsEmpty)
                MoveSelection(_viewManager.GetPositionFromPoint(_viewManager.GetPointFromRelativePoint(this, arg.GetPosition(this))), KeyboardUtil.Shift);
            ShowContextMenu(e.GetPosition(this), arg);
            e.Handled = true;
        }

        void ShowContextMenu(Point p, RightTappedRoutedEventArgs originalArgs = null)
        {
            if (Double.IsNegativeInfinity(p.X) || Double.IsNegativeInfinity(p.Y))
                // If Shift+F10/Application downs, get the inside hidden textbox position.
                p = _txtBox.TransformToVisual(null).TransformPoint(new Point());
            // Do spell check first
            // NOTE: Do spell check only when the selection is empty. This is the same behavior with native MS RichEditBox.
            if (_spellCheck != null && Selection.IsEmpty)
            {
                if (!_spellCheck.ShowContextMenu(new ContextMenuOpeningEventArgs(Selection, p, originalArgs)))
                    return;
            }

            if (ContextMenuOpening != null)
            {
                var args = new ContextMenuOpeningEventArgs(Selection);
                ContextMenuOpening(this, args);
                if (args.Handled) return;
            }
#if WINDOWS_APP || UWP
            if (ContextMenu != null && ContextMenu is IC1ContextMenu)
            {
                ((IC1ContextMenu)ContextMenu).Show(this, p);
            }
            else
#endif
            {
                _hasShownContextMenu = false;
                PopupMenu popupMenu = ContextMenu as PopupMenu;
                if (popupMenu != null && popupMenu.Commands.Count > 0)
                {
                    if (!_hasShownContextMenu)
                    {
                        _hasShownContextMenu = true;
                        popupMenu.ShowAsync(p).Completed += (s, e) => { _hasShownContextMenu = false; };
                    }
                }
                else
                {
                    _contextMenu.Commands.Clear();
                    _copyCommand.Label = C1_Silverlight_RichTextBox.Copy;
                    _pasteCommand.Label = C1_Silverlight_RichTextBox.Paste;
                    _cutCommand.Label = C1_Silverlight_RichTextBox.Cut;
                    _selectallCommand.Label = C1_Silverlight_RichTextBox.SelectAll;
                    if (Selection.Text.Length > 0)
                    {
                        _contextMenu.Commands.Add(_copyCommand);
                    }
#if WINDOWS_APP || UWP
                    if (!IsReadOnly)
                    {
                        var dpv = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                        if (dpv.Contains(StandardDataFormats.Bitmap)
                            || dpv.Contains(StandardDataFormats.Html)
                            || dpv.Contains(StandardDataFormats.Text))
                        {
                            _contextMenu.Commands.Add(_pasteCommand);
                        }
                        if (Selection.Text.Length > 0)
                        {
                            _contextMenu.Commands.Add(_cutCommand);
                        }
                    }
#endif
                    _contextMenu.Commands.Add(_selectallCommand);
                    if (!_hasShownContextMenu)
                    {
                        _hasShownContextMenu = true;
                        // The ContextMenu shows at the position which is relative with the current window.
                        // Hereby transform the point again.
                        p = this.TransformToVisual(null).TransformPoint(p);
                        _contextMenu.ShowAsync(p).Completed += (s, e) => { _hasShownContextMenu = false; };
                    }
                }
            }
        }
#endif

#if WINRT
        void ShowFrameworkElement(FrameworkElement fe, double left, double top, double right, double bottom)
        {
            if (fe != null)
            {
                if (top < 0 || 
#if WINRT || WP8
                    left < -fe.Width / 2
#else
                    left < 0
#endif
                    )
                {
                    fe.Visibility = Visibility.Collapsed;
                    return;
                }
                fe.Margin = new Thickness(left, top, right, bottom);
                fe.Visibility = Visibility.Visible;
                fe.SetValue(Canvas.ZIndexProperty, 1000);
            }
        }

        void HideFrameworkElement(FrameworkElement fe)
        {
            if (fe != null && fe.Visibility == Visibility.Visible)
            {
                fe.Visibility = Visibility.Collapsed;
            }
        }
#endif

        void OnTapped(object sender, C1TappedEventArgs e)
        {
#if WINDOWS_APP || UWP
            base.OnTapped(e.OriginalEventArgs as TappedRoutedEventArgs);
            _isTouchInput = e.PointerDeviceType == C1PointerDeviceType.Touch;
#endif

#if WINRT
            if (!this.IsInVisualTree())
            {
                return;
            }
#endif

#if WINRT
#if WINDOWS_APP || UWP
            ToolTipHelper.CloseToolTip(e.OriginalEventArgs as RoutedEventArgs);
            _dragged = false;
#endif
            base.Focus(FocusState.Programmatic);
            this.ResetTextBox();
#endif
            var point = _viewManager.GetPointFromRelativePoint(this, e.GetPosition(this));
            var element = _viewManager.GetElementFromPoint(point);
            var hyperlink = element.GetParents().Concat(new[] { element }).FirstOrDefault(el => el is C1Hyperlink) as C1Hyperlink;

            if (hyperlink != null)
            {
                if (RequestNavigate != null &&
#if WINRT
                    (NavigationMode == NavigationMode.Always))
#else
                    (NavigationMode == NavigationMode.Always || (NavigationMode == NavigationMode.OnControlKey && KeyboardUtil.Command)))
#endif
                {
                    RequestNavigate(this, new RequestNavigateEventArgs(hyperlink));
                }
            }
#if WINRT
            else
            {
                var current = _viewManager.GetPositionFromPoint(point);
#if WINDOWS_APP || UWP
                if (!_isTouchInput)
                {
                    DateTime now = DateTime.Now;

                    if (now.Subtract(_lastTime).TotalMilliseconds <= SystemInformation.DoubleClickTime)
                    {
                        double dx = _lastPos.X - point.X;
                        double dy = _lastPos.Y - point.Y;
                        if (dx * dx + dy * dy <= SystemInformation.DoubleClickDistance)
                        {
                            Selection = GetWord(current);
                            return;
                        }
                    }
                }
#endif

                if (IsEnabled && !IsReadOnly)
                {
                    if (Selection.IsEmpty || !Selection.Contains(current))
                    {
                        // IF you tap a word that already contains the cursor, then the word is selected.
                        var range = GetWord(current);
                        if (range.Contains(Selection.Start)
#if WINDOWS_APP || UWP
                            && _isTouchInput
#endif
                            )
                        {
                            _needsShowThumbs = true;
                            Selection = GetWord(current);
                            OnElementTapped(element, e.OriginalEventArgs as TappedRoutedEventArgs);
                            return;
                        }
                    }

                    if (_dragTargetPainter == null)
                    {
                        if (element is C1InlineUIContainer || element is C1BlockUIContainer)
                        {
                            // If the element is used for image, show the resizer element.
                            _needsShowThumbs = false;
                            Selection = element.ContentRange;
                        }
                        else
                        {
                            var table = element.GetParents().LastOrDefault(el => el is C1Table) as C1Table;
                            if (_resizePainter.Element is C1Table)
                            {
                                // If the element is for table, show the resizer element.
                                _resizePainter.Element = null;
                                _selectionPainter.Background = SelectionBackground;
                                _selectionPainter.PaintCaret = true;
                                Redraw();
#if WINDOWS_APP || UWP
                                if (_isTouchInput)
#endif
                                    _needsShowThumbs = true;
#if WINDOWS_APP || UWP
                                else
                                    _needsShowThumbs = false;
#endif
                            }
                            else if (table != null)
                            {
                                _isTableResized = true;
                                _selectionPainter.PaintCaret = false;
                                _needsShowThumbs = false;
                            }
                            else
                            {
#if WINDOWS_APP || UWP
                                if (_isTouchInput)
#endif
                                    _needsShowThumbs = true;
#if WINDOWS_APP || UWP
                                else
                                    _needsShowThumbs = false;
#endif
                            }
                            if (
#if WINDOWS_APP || UWP
                                _isTouchInput && 
#endif
                                (!IsEnabled || IsReadOnly))
                            {
                                // If the control is readonly, all the word is selected when you tap a word.
                                Selection = GetWord(current);
                            }
                            else
#if WINDOWS_APP || UWP
                                MoveSelection(current, KeyboardUtil.Shift);
#else
                                MoveSelection(current, false);
#endif
                        }
                    }
                }

                OnElementTapped(element, e.OriginalEventArgs as TappedRoutedEventArgs);
#if WINDOWS_APP || UWP
                if (!_isTouchInput)
                {
                    _lastTime = DateTime.Now;
                    _lastPos = point;
                }
#endif
            }

#endif
        }

#if WINRT

        Rect ExpandTouchRect(Rect rect
#if WINRT
            , FrameworkElement fe
#endif
            )
        {

#if WINRT
            rect.X -= fe.Width;
            rect.Width += fe.Width * 2;
            rect.Height += fe.Height;
#else
            rect.X -= 22;
            rect.Width += 44;
            rect.Height += 22;
#endif
            return rect;
        }

#endif

        void OnDragStarted(object sender, C1DragStartedEventArgs e)
        {
#if WINRT
            if (_resizePainter.Element != null && e.OriginalEventArgs != null && e.OriginalEventArgs.OriginalSource == _resizePainter.Resize)
                return;
#endif

#if WINDOWS_APP || UWP
            _isTouchInput = e.PointerDeviceType == C1PointerDeviceType.Touch;
            if (e.PointerDeviceType == C1PointerDeviceType.Mouse)
            {
#endif
            _dragged = true;
#if WINDOWS_APP || UWP
                _dragTargetPainter = null;
                var current = _viewManager.GetPositionFromPoint(GetPosition(e.OriginalEventArgs as PointerRoutedEventArgs));
                if (!Selection.IsEmpty && Selection.Contains(current))
                {
                    // For changing the position of the word.
                    var eventArgs = new CancelEventArgs();
                    OnTextDragStart(eventArgs);
                    if (!eventArgs.Cancel)
                    {
                        _dragTargetPainter = new PlaceholderPainter();
                    }
                }
                else
                {
                    Selection = new C1TextRange(current);
                }
#endif

            if (_dragTargetPainter != null)
            {
                Painters.Add(_dragTargetPainter);
            }
#if WINDOWS_APP || UWP
            }
#endif

#if WINRT
#if WINDOWS_APP || UWP
            if (_isTouchInput)
            {
#endif
            {
                _needsShowThumbs = false;
                // Please note: this position is where the delta starts, not where drag stars.
                var point = e.GetPosition(this);
                point = _viewManager.GetPointFromRelativePoint(this, point);
                var rect = ExpandTouchRect(_viewManager.GetRectFromPosition(Selection.Start)
                    , Selection.IsEmpty ? _elementCursorThumb : _elementSelectionStartThumb);
                _selectionExtend = LogicalDirection.Backward;
                if (!rect.Contains(point))
                {
                    rect = ExpandTouchRect(_viewManager.GetRectFromPosition(Selection.End), _elementSelectionEndThumb);
                    _selectionExtend = LogicalDirection.Forward;
                }
                if (rect.Contains(point))
                {
                    _manipulationStart = new Point(_selectionExtend == LogicalDirection.Backward ? rect.X : rect.Right, rect.Y);

                    _manipulatedSelection = Selection;
                }
                else
                {
                    _manipulatedSelection = null;
                }
            }
#if WINDOWS_APP || UWP
            }
#endif
#endif
        }

        void OnDragDelta(object sender, C1DragDeltaEventArgs e)
        {
#if WPF
            if (_dragged && _dragTargetPainter != null)
            {
                _dragged = false;

                if ((e.OriginalEventArgs as MouseEventArgs).LeftButton == MouseButtonState.Pressed)
                {
                    DataObject data = new DataObject();
                    string text = SelectedText;
                    if (text != string.Empty)
                    {
                        data.SetData(DataFormats.Text, text);
                        data.SetData(DataFormats.UnicodeText, text);
                    }

                    C1Document doc = Selection.GetFragment(true);
                    doc.RichTextBox = this;

                    var html = HtmlFilter.ConvertFromDocument(doc, HtmlEncoding.Inline);
                    html = Regex.Match(html, @"<body[^>]*>(.*?)</body>", RegexOptions.Singleline).Groups[1].Value.Trim();

                    var htmlMatch = new Regex("<").Match(html);
                    if (!htmlMatch.Success)
                    {
                        html = "<p>" + html + "</p>";
                    }
                    var rtf = _rtfFilter.ConvertFromDocument(doc);
                    data.SetData(DataFormats.Rtf, rtf);
                    data.SetData(DataFormats.Html, html);
                    var selection = new C1TextRange(Selection.Start, Selection.End);
                    DragDropEffects copy = DragDropEffects.Copy;
                    if (!IsReadOnly)
                    {
                        copy |= DragDropEffects.Move;
                    }
                    DragDropEffects effects = DragDrop.DoDragDrop(this, data, copy);
                    if (effects == DragDropEffects.Move)
                    {
                        selection.Delete(needDeleteTable: true);
                    }
                }
            }

            if (!_dragged)
            {
                return;
            }
#endif

#if WINRT
            if (_resizePainter.Element != null && e.OriginalEventArgs != null && e.OriginalEventArgs.OriginalSource == _resizePainter.Resize)
                return;
#endif

#if WINDOWS_APP || UWP
            _isTouchInput = e.PointerDeviceType == C1PointerDeviceType.Touch;
            if (_isTouchInput)
            {
                _needsShowThumbs = false;
            }
            if (e.PointerDeviceType == C1PointerDeviceType.Mouse)
            {
                if (e.IsInertial)
                    return;
#endif
            var current = _viewManager.GetPositionFromPoint(GetPosition(e.OriginalEventArgs as
#if WINDOWS_APP || UWP
                PointerRoutedEventArgs
#else
                MouseEventArgs
#endif
                ));

            if (_dragTargetPainter != null)
            {
                var args = new TextDragMoveEventArgs { Position = current };
                OnTextDragMove(args);
                _dragTargetPainter.Position = args.Position.ClosestCaret;
            }
            else
            {
                MoveSelection(current, true);
            }
#if WINDOWS_APP || UWP
            }
#endif

#if WINRT
#if WINDOWS_APP || UWP
            if (_isTouchInput)
            {
#endif
                if (!IsReadOnly)
                {
                    if (_manipulatedSelection == null) //if (!IsTextFocused)
                    {
                        HorizontalOffset -= e.DeltaTranslation.X;
                        VerticalOffset -= e.DeltaTranslation.Y;
                        //e.Handled = true;
#if !WINRT
                        var margin = _txtBox.Margin;
                        if (ViewportHeight + VerticalOffset == ExtentHeight)
                        {
                            _txtBox.Height = ViewportHeight;
                            _txtBox.Margin = new Thickness(margin.Left, ViewportHeight - _txtBox.ActualHeight, margin.Right, margin.Bottom);
                            // If arrives at the bottom of the document, stop drag action.
                            if (e.IsInertial)
                                _dragHelper.Complete();
                        }
                        else if (VerticalOffset == 0)
                        if (VerticalOffset == 0)
                        {
                            _txtBox.Height = ViewportHeight;
                            _txtBox.Margin = new Thickness(margin.Left, -_topOffset, margin.Right, margin.Bottom);
                            if (e.IsInertial)
                                _dragHelper.Complete();
                        }
                        ShowScrollBars();
#endif

                    }
#if WINRT
                    // If the virtual keyboard is shown, change the margin of hidden text to 
                    // make it at the bottom of the viewport when srcoll.
                    var margin = _txtBox.Margin;
                    if (ViewportHeight + VerticalOffset == ExtentHeight)
                    {
                        _txtBox.Margin = new Thickness(margin.Left, ViewportHeight - _txtBox.ActualHeight, margin.Right, margin.Bottom);
                        // If arrives at the bottom of the document, stop drag action.
                        if (e.IsInertial)
                            _dragHelper.Complete();
                        ResetFocus();
                    }
                    else if (VerticalOffset == 0)
                    {
                        _txtBox.Margin = new Thickness(margin.Left, -_keyboardHeight, margin.Right, margin.Bottom);
                        // If arrives at the top of the document, stop drag action.
                        if (e.IsInertial)
                            _dragHelper.Complete();
                        ResetFocus();
                    }
                    
#endif
                }
                else
                {
                    HorizontalOffset -= e.DeltaTranslation.X;
                    VerticalOffset -= e.DeltaTranslation.Y;
#if !WINRT
                    ShowScrollBars();
#endif
                }
            if (_manipulatedSelection != null)
            {
                _manipulationStart.X += e.DeltaTranslation.X;
                _manipulationStart.Y += e.DeltaTranslation.Y;
                var position = _viewManager.GetPositionFromPoint(_manipulationStart);
                MoveSelection(position, true);
            }
#if WINDOWS_APP || UWP
            }
#endif
#endif
        }

        void OnDragCompleted(object sender, C1DragCompletedEventArgs e)
        {
#if WINRT

            if (_resizePainter.Element != null && e.OriginalEventArgs != null && e.OriginalEventArgs.OriginalSource == _resizePainter.Resize)
                return;
#endif
#if WINRT
            _isTouchInput = e.PointerDeviceType == C1PointerDeviceType.Touch;
            if (_isTouchInput)
            {
                _needsShowThumbs = true;
                PaintThumbs();
            }
            if (e.PointerDeviceType == C1PointerDeviceType.Mouse)
            {
#endif
            _dragged = false;
            if (_dragTargetPainter != null)
            {
                RemovePlacePainter();
                if (e.OriginalEventArgs == null) return;

#if WINRT
                    var prea = e.OriginalEventArgs as PointerRoutedEventArgs;
                    if (prea == null) return;
                    var point = GetPosition(prea);
#else
                var point = GetPosition(e.OriginalEventArgs as MouseEventArgs);
#endif

                var current = _viewManager.GetPositionFromPoint(point);
                var args = new TextDropEventArgs(current);
                OnTextDrop(args);
                if (args.Handled || (current >= Selection.Start && current <= Selection.End))
                {
                    return;
                }
                using (new DocumentHistoryGroup(DocumentHistory))
                {
                    var fragment = Selection.Fragment;
                    Selection.Delete();
                    var range = new C1TextRange(current);
                    range.Fragment = fragment;
                    Selection = range;
                }
            }
            else
            {
                OnMouseSelectionCompleted();
            }
#if WINRT
            }

#endif
        }

        internal static C1TextRange GetWord(C1TextPointer position)
        {
            var previousStart = position.IsWordStart ? position : null;
            if (previousStart == null)
                previousStart = position.GetClosestPointer(LogicalDirection.Backward, (p) => p.IsWordStart);
            var previousEnd = position.GetClosestPointer(LogicalDirection.Backward, (p) => p.IsWordEnd);
            var selectWord = previousEnd == null || (previousStart != null && previousStart > previousEnd);
            var start = selectWord ? previousStart ?? position : previousEnd;
            var end = start.GetClosestPointer(LogicalDirection.Forward, (p) => selectWord ? p.IsWordEnd : p.IsWordStart) ?? position;
            return new C1TextRange(start, end);
        }

        void ResetFocus()
        {
#if WINRT
            base.Focus(FocusState.Programmatic);
#else
            base.Focus();
#endif
            this.Focus();
        }

        void CreateShortcuts()
        {
            _shortcuts = new Dictionary<Shortcut, Action>
            {
                { new Shortcut(ModifierKeys.Control, Key.A), SelectAll },
                { new Shortcut(ModifierKeys.Control, Key.C), ClipboardCopy },
                { new Shortcut(ModifierKeys.Control, Key.Insert), ClipboardCopy },

                { new Shortcut(ModifierKeys.Control, Key.X), ClipboardCutUser },
                { new Shortcut(ModifierKeys.Shift, Key.Delete), ClipboardCutUser },

                { new Shortcut(ModifierKeys.Control, Key.V), ClipboardPasteUser },
                { new Shortcut(ModifierKeys.Shift, Key.Insert), ClipboardPasteUser },
#if WINRT
                { new Shortcut(ModifierKeys.Shift, Key.F10), () => ShowContextMenu(new Point(Double.NegativeInfinity, Double.NegativeInfinity)) },
#endif
                { new Shortcut(ModifierKeys.Control, Key.Z), Undo },

                { new Shortcut(ModifierKeys.Control, Key.Y), Redo },

                { new Shortcut(ModifierKeys.Control | ModifierKeys.Shift, Key.C), FormatCopy },
                { new Shortcut(ModifierKeys.Control | ModifierKeys.Shift, Key.V), FormatPaste },

                { new Shortcut(ModifierKeys.Control, Key.B), BoldfaceSelection },
                { new Shortcut(ModifierKeys.Control, Key.I), ItalicizeSelection },
                { new Shortcut(ModifierKeys.Control, Key.U), UnderlineSelection },

            };
        }

        /// <summary>
        /// Remove the shortcut for some action.
        /// </summary>
        /// <param name="modifier">Modifier key of the shortcut.</param>
        /// <param name="key">Key of the shortcut.</param>
        public void RemoveShortcut(ModifierKeys modifier, Key key)
        {
            Shortcut shortcut = new Shortcut(modifier, key);
            if (_shortcuts.ContainsKey(shortcut))
            {
                _shortcuts.Remove(shortcut);
            }
        }

        void ClipboardPasteUser()
        {
            if (!IsReadOnly)
            {
#if WINRT
                ResetTextBox();
#endif
                ClipboardPaste();
            }
        }

        void ClipboardCutUser()
        {
            if (IsReadOnly)
                ClipboardCopy();
            else
                ClipboardCut();
        }

        void Undo()
        {
            _documentHistory.Undo();
            ResetFocus();
        }

        void Redo()
        {
            _documentHistory.Redo();
            ResetFocus();
        }

#pragma warning disable 1591
        private void InputText(string text)
        {
            this.InputText(Selection, text);
        }

        private void InputText(C1TextRange selection, string text)
        {
            if (!IsEnabled || IsReadOnly) return;

            C1TextRange range = null;
            try // wrap below line into try/catch
            {
                // Get the range that starts with base level Thai language character, or whitespace, or some not Thai language charater before Selection 
                range = new C1TextRange(GetSpecialLanguageStartPoint(Selection.End.GetPositionAtOffset(-1), LogicalDirection.Backward), Selection.End);
            }
            catch // if there is exception, try to search for the first position
            {
                range = new C1TextRange(GetSpecialLanguageStartPoint(selection.Start.ClosestCaret, LogicalDirection.Forward), Selection.End);
            }

            // Base level Thai character has been inputted.
            bool hasThaiBaseLevel = false;
            // Thai vowel character has been inputted.
            bool hasThaiVowel = false;
            // Thai intonation character has been inputted.
            bool hasThaiIntonation = false;

            char current;
            if (!char.TryParse(text, out current)) current = default;
            if (current != ' ' || TextHelper.IsThaiCharacter(current))
            {
                // For Thai language 
                for (int i = 0; i < range.Text.Length; i++)
                {
                    char ch = range.Text[i];

                    if (TextHelper.IsThaiAlphabetical(ch))
                    {
                        hasThaiBaseLevel = true;
                    }
                    else if (TextHelper.IsThaiVowel(ch))
                    {
                        hasThaiVowel = true;
                    }
                    else if (TextHelper.IsThaiIntonation(ch))
                    {
                        hasThaiIntonation = true;
                    }
                }
                if (TextHelper.IsThaiVowel(current)
                    && (!hasThaiBaseLevel || hasThaiVowel)
                    && !TextHelper.IsThaiLeadingVowel(current)
                    && !TextHelper.IsThaiDiphthongs(range.Text.LastOrDefault(), current))
                {
                    // Only one vowel can be inputted with alphabetical.
                    return;
                }
                else if (TextHelper.IsThaiIntonation(current) && (!hasThaiBaseLevel || hasThaiIntonation))
                {
                    // Only one intonation can be inputted with alphabetical.
                    return;
                }
            }

            _documentHistory.BeginGroup();
            var curRange = EditExtensions.ReplaceText(selection, text);
            if (AutoCapitalizationMode == AutoCapitalizationMode.Sentence && _isTypingEnglish)
            {
                C1TextRange word = null;
                if (text.Equals(" "))
                    word = GetWord(selection.Start);
                else if (text.Equals("'"))
                    word = GetWord(selection.Start.GetPositionAtOffset(-1));
                if (word != null && !String.IsNullOrWhiteSpace(word.Text))
                {
                    var par = selection.Start.Element.GetClosestParent(p => p is C1Paragraph);
                    if (par != null)
                    {
                        range = new C1TextRange(par.ContentStart, word.Start);
                        if (String.IsNullOrWhiteSpace(range.Text))
                            word.CapitalizeWords();
                        else
                        {
                            var pointer = word.Start.GetClosestPointer(LogicalDirection.Backward,
                                p => (p.Symbol is char && !Char.IsWhiteSpace((char)p.Symbol)) || (p.Symbol is Tag));
                            if ((pointer.Symbol is Tag) || (pointer.Symbol is char && TextHelper.IsSentanceEndPunctuation((char)pointer.Symbol)))
                            {
                                word.CapitalizeWords();
                            }
                        }
                    }
                }
            }

            if (selection.IsEmpty)
            {
                selection = new C1TextRange(Selection.Start.Closest(p1 => p1.IsPreferredCaretPosition, (p1, p2) => p1.DistanceTo(p2)));
            }

            if (curRange.Start == curRange.End && curRange.Start.IsHyperlinkPosition)
            {
                Selection = curRange;
            }

            _documentHistory.EndGroup();
            IsInput = true;
            ScrollSelection();
            IsInput = false;
            _isTypingEnglish = false;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
#if WINRT
            if (e.Key == Key.Enter)
            {
                this.ResetTextBox();
                _ignoreTextChanged = false;
            }

#if WINRT
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                _ignoreTextChanged = false;
            }
#endif
#endif
            if (IsCtrl(e.Key))
            {
                UpdateCursor();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
        }

        private void OnC1KeyDown(KeyEventArgs e)
        {
            ToolTipHelper.CloseToolTip(e);

            if (KeyDown != null)
            {
                KeyDown(this, e);
            }

#if WINRT
            if (!e.Handled && IsTextFocused)
#elif true //CJKSupport
            if (!e.Handled)
#else
            if (!e.Handled && IsFocused)
#endif
            {
#if WINDOWS_APP || UWP
                OnKeyDown(new C1KeyEventArgs { Key = e.Key, Handled = e.Handled });
                if (e.Key == Key.Tab && AcceptsTab)
                {
                    e.Handled = true;
                }
#if UWP
                else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
                    e.Handled = true;
#endif
#else
                OnKeyDown(new C1KeyEventArgs(e));
#endif
            }
        }

#pragma warning restore 1591

        internal void OnKeyDown(C1KeyEventArgs e)
        {

            using (new DocumentHistoryGroup(_documentHistory))
            {
#if WINRT
                _ignoreTextChanged = e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Delete;
#endif
                if (IsCtrl(e.Key))
                {
                    UpdateCursor();
                }
                Action action;
                if (_shortcuts.TryGetValue(new Shortcut(Keyboard.Modifiers, e.Key), out action))
                {
#if WPF
                    if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Insert)
                    {
                        ResetTextBox();
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
                    {
                        ResetTextBox();
                        e.Handled = true;
                    }
#endif
                    action();
                    return;
                }

#if UWP
                // if paste or insert not in _shortcuts, clear _txt.Text.
                if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V) || (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Insert))
                {
                    ResetTextBox();
                    e.Handled = true;
                    return;
                }
#endif

                var isFunKey = e.Key == Key.Back ||
                    e.Key == Key.Delete ||
                    e.Key == Key.Left ||
                    e.Key == Key.Right ||
                    e.Key == Key.Up ||
                    e.Key == Key.Down ||
                    e.Key == Key.PageUp ||
                    e.Key == Key.PageDown ||
                    e.Key == Key.Home ||
                    e.Key == Key.End ||
                    e.Key == Key.Tab;

                if (_txtBox != null && !string.IsNullOrEmpty(_txtBox.Text))
                {
                    if (isFunKey)
                    {
#if WPF || WINRT
                        if (curIMELanguage == IMELanguage.Korean)
                        {
                            this.ResetTextBox();
                        }
                        else
                        {
                            e.Handled = true;
                        }
#endif
                    }
#if WPF
                    if (e.Key == Key.Space)
                        InsertHyperlink();
#endif

#if WPF || WINRT
                    if (curIMELanguage == IMELanguage.Other)
                    {
                        return;
                    }
#endif
                }

                switch (e.Key)
                {
                    case Key.Back:
                    case Key.Delete:
                        HandleDeleteKeyDown(e);
                        break;
                    case Key.Enter:
                        InsertHyperlink();
                        HandleEnterKeyDown();
                        break;
                    case Key.Left:
                    case Key.Right:
                        HandleLeftRightKeyDown(e);
                        break;
                    case Key.Up:
                    case Key.Down:
                        HandleUpDownKeyDown(e);
                        break;
                    case Key.PageUp:
                    case Key.PageDown:
                        HandlePageUpDownKeyDown(e);
                        break;
                    case Key.Home:
                    case Key.End:
                        HandleHomeEndKeyDown(e);
                        break;
                    case Key.Tab:
                        HandleTabKeyDown(e);
                        break;
#if !WPF
                    case Key.Space:
                        InsertHyperlink();
                        break;
#endif
#if WINDOWS_APP || UWP
                    case Key.Application:
                        ShowContextMenu(new Point(Double.NegativeInfinity, Double.NegativeInfinity));
                        break;
#endif
                }
            }
        }

        #region Specific key handlers

        private void HandleHomeEndKeyDown(C1KeyEventArgs e)
        {
            if (!KeyboardUtil.Ctrl)
            {
                var line = ViewManager.GetLine(SelectionTail);
                if (SelectionTail == line.Start)
                {
                    line = SelectionTail.LogicalDirection == line.Start.LogicalDirection ? line : (line.Previous ?? line);
                }
                if (line != null)
                {
                    var caretPosition = line.LastCaretPosition;
                    if (e.Key == Key.End)
                    {
                        caretPosition.LogicalDirection = LogicalDirection.Backward;
                    }
                    e.Handled = !SelectionTail.Equals(e.Key == Key.Home ? line.Start : caretPosition);
                    MoveSelection(e.Key == Key.Home ? line.Start : caretPosition, KeyboardUtil.Shift);
                }
            }
            else
            {
                e.Handled = !SelectionTail.Equals(e.Key == Key.Home ? FirstInsertionPoint : LastInsertionPoint);
                MoveSelection(e.Key == Key.Home ? FirstInsertionPoint : LastInsertionPoint, KeyboardUtil.Shift);
            }

            if (this._elementHorizontalScrollBar != null && this._elementHorizontalScrollBar.Maximum > 0)
            {
                e.Handled = true;
            }
        }

        private void HandlePageUpDownKeyDown(C1KeyEventArgs e)
        {
            var offset = ViewportHeight * (e.Key == Key.PageDown ? 1 : -1);
            var pos = _viewManager.GetPositionFromPoint(new Point(_floatingTail.X, _floatingTail.Y + offset + VerticalOffset));

            if (!KeyboardUtil.Ctrl)
            {
                e.Handled = !pos.Equals(SelectionTail);
            }
            VerticalOffset += offset;
            var dir = e.Key == Key.PageUp ? LogicalDirection.Backward : LogicalDirection.Forward;
            if (!pos.IsCaretPosition)
            {
                // this solves some problems with uneditable lines
                pos = pos.GetClosestPointer(dir, p => p.IsCaretPosition) ?? pos;
            }
            MoveSelection(pos, KeyboardUtil.Shift);
            if (this._elementVerticalScrollBar != null && this._elementVerticalScrollBar.Maximum > 0)
            {
                e.Handled = true;
            }
        }

        private void HandleUpDownKeyDown(C1KeyEventArgs e)
        {
            var dir = e.Key == Key.Up ? LogicalDirection.Backward : LogicalDirection.Forward;
            var pos = _viewManager.GetPositionInNextLine(dir, SelectionTail, (float)_floatingTail.X);
            if (KeyboardUtil.Shift && pos.Equals(SelectionTail))
            {
                var line = ViewManager.GetLine(SelectionTail);
                pos = e.Key == Key.Up ? line.Start : line.LastCaretPosition;
            }
            e.Handled = !pos.Equals(SelectionTail);
            if (!pos.IsCaretPosition)
            {
                // this solves some problems with uneditable lines
                pos = pos.GetClosestPointer(dir, p => p.IsCaretPosition) ?? pos;
            }

            var oldX = _floatingTail.X;
            MoveSelection(pos, KeyboardUtil.Shift);
            _floatingTail.X = oldX;

            e.Handled = true;
        }

        private void HandleLeftRightKeyDown(C1KeyEventArgs e)
        {
            bool isLeft = (e.Key == Key.Left && FlowDirection == FlowDirection.LeftToRight)
                || (e.Key == Key.Right && FlowDirection == FlowDirection.RightToLeft);
            if (!KeyboardUtil.Ctrl && !KeyboardUtil.Shift && !Selection.IsEmpty)
            {
                MoveSelection(isLeft ? Selection.Start : Selection.End, false);
                e.Handled = true;
            }
            else
            {
                var dir = isLeft ? LogicalDirection.Backward : LogicalDirection.Forward;
                var pos = GetSpecialLanguageStartPoint(SelectionTail.EnumerateCarets(dir).FirstOrDefault(WordStartIfControl), dir);
                e.Handled = pos != null;
                if (pos != null)
                {
                    MoveSelection(pos, KeyboardUtil.Shift);
                }
            }
            if (this._elementHorizontalScrollBar != null && this._elementHorizontalScrollBar.Maximum > 0)
            {
                e.Handled = true;
            }
        }

        static bool WordStartIfControl(C1TextPointer p)
        {
            return !KeyboardUtil.Ctrl || p.IsWordStart;
        }

        private void HandleEnterKeyDown()
        {
            if (AcceptsReturn && !IsReadOnly)
            {
                // We need to ignore TextChanged when enter key was pressed.
                BeginTextChanged();
                if (!Selection.IsEmpty)
                {
                    Selection.Delete();
                }
                if (ReturnMode == ReturnMode.SoftLineBreak || (KeyboardUtil.Shift && ReturnMode == ReturnMode.Default))
                {
                    _selection.Start.LogicalDirection = LogicalDirection.Forward;
                    EditExtensions.InsertSoftLineBreak(Selection.Start);
                }
                else
                {
                    var start = Selection.Start;
                    // Press Enter: Add a new paragraph above the table
                    // Press Shift + Enter: Add a new paragraph in the table cell
                    // Undo
                    // Redo
                    if (Document.ContentStart.ClosestCaret == start && start.Element.GetClosestParent(p => p is C1Table) != null)
                    {
                        using (new DocumentHistoryGroup(DocumentHistory))
                        {
                            var newPara = new C1Paragraph();
                            newPara.Children.Add(new C1Run());
                            Document.Children.Insert(0, newPara);
                            Selection = new C1TextRange(newPara.ContentStart);
                        }
                    }
                    else
                        EditExtensions.InsertHardLineBreak(start);
                }
                ScrollSelection();
                // We need to call EndTextChanged when cut action was occurred.
                EndTextChanged();
            }
        }

        private void HandleDeleteKeyDown(C1KeyEventArgs e)
        {
            if (!IsReadOnly)
            {
                // We need to ignore TextChanged when delete action was occurred.
                BeginTextChanged();

                C1TextRange range = Selection.IsEmpty ?
                    (e.Key == Key.Back ?
                        new C1TextRange(Selection.Start.EnumerateCarets(LogicalDirection.Backward).FirstOrDefault(WordStartIfControl) ?? Document.ContentStart, Selection.End)
                        : new C1TextRange(Selection.Start, GetSpecialLanguageStartPoint(Selection.Start.EnumerateCarets(LogicalDirection.Forward).FirstOrDefault(WordStartIfControl), LogicalDirection.Forward) ?? Document.ContentEnd))
                        : Selection;

                C1ListItem item = range.Elements.FirstOrDefault(el => el is C1ListItem) as C1ListItem;
                bool isStartOfListItem = range.Runs.IsEmpty() && item != null;
                var caret = EditExtensions.Delete(GetDeleteRange(range, e.Key == Key.Delete), false, e.Key == Key.Back);
                if (Selection.IsEmpty && e.Key == Key.Back && isStartOfListItem)
                    caret = Selection.End;
                Selection = new C1TextRange(caret);
                ScrollSelection();
                // We need to call EndTextChanged when cut action was occurred.
                EndTextChanged();
            }
            if (e.Key == Key.Back)
                e.Handled = true;
        }

        /// <summary>
        /// Get the range used to delete by the original range.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="isDeleteKey"></param>
        /// <returns></returns>
        C1TextRange GetDeleteRange(C1TextRange range, bool isDeleteKey = false)
        {
            C1TextPointer start = range.Start;
            C1TextPointer end = range.End;
            var startCell = start.Element.GetTopParent(p => p is C1TableCell);
            var endCell = end.Element.GetTopParent(p => p is C1TableCell);

            if (startCell == null && endCell != null)
            {
                // If the start is outside of the table, and the end is inside the table.
                var table = endCell.GetTopParent(p => p is C1Table);
                if (endCell.ContentEnd.ClosestCaret.DistanceTo(end) == 0 && endCell.ContentEnd.DistanceTo(table.ContentEnd) == 3)
                {
                    // We need confirm the end is the end position of the cell.
                    // Because the first three cell's parents are TableRow, TableRowGroup, Table,
                    // therefore the distance between endCell and table is 3.
                    end = table.ContentEnd.ClosestCaret;
                }
                else if (isDeleteKey && _selection.IsEmpty)
                {
                    start = Selection.Start.EnumerateCarets(LogicalDirection.Backward).FirstOrDefault(WordStartIfControl) ?? Document.ContentStart;
                }
            }
            else if (startCell != null && endCell == null)
            {
                // If the end is outside of the table, and the start is inside the table.
                var table = startCell.GetTopParent(p => p is C1Table);
                if (startCell.ContentStart.ClosestCaret.DistanceTo(start) == 0 && startCell.ContentStart.DistanceTo(table.ContentStart) == 3)
                {
                    if (isDeleteKey && end.Offset == 0)
                    {
                        var lastCaret = end.EnumerateCarets(LogicalDirection.Backward).FirstOrDefault();
                        if (lastCaret != null && lastCaret.Element.GetClosestParent(p => p is C1TableCell) != null)
                        {
                            start = range.Start;
                            end = lastCaret;
                        }
                    }
                    else
                    {
                        // We need confirm the start is the start position of the cell.
                        // Because the first three cell's parents are TableRow, TableRowGroup, Table,
                        // therefore the distance between startCell and table is 3.
                        start = table.ContentStart.ClosestCaret;
                    }
                }
            }

            // If the current position is at the start of C1Run element, and its previous sibling is C1Run which ends with "\n",
            // Then move the current position just before the "\n".
            var preRun = start.Element.PreviousSibling as C1Run;
            if (preRun != null && start.Element is C1Run && preRun.Text.Length > 0 && preRun.Text.Substring(preRun.Text.Length - 1) == "\n" && start.Offset == 0)
            {
                // Insert a empty whitespace text \u200b at the start of the previous C1Run element's text if it is "\n". 
                if (preRun.Text.Length == 1)
                    preRun.Text = "\u200b\n";
                var newStart = start.GetClosestPointer(LogicalDirection.Backward, p => p.Symbol.Equals('\n'));
                if (newStart != null)
                    start = newStart;
            }
            return new C1TextRange(start, end);
        }

        void HandleTabKeyDown(C1KeyEventArgs e)
        {
            if (AcceptsTab)
            {
                C1TableCell start, end;
                var table = Selection.FindTable(out start, out end);
                if (table != null && start != null)
                {
                    var dir = KeyboardUtil.Shift ? LogicalDirection.Backward : LogicalDirection.Forward;
                    var nextCell = FindNextCell(table, start, dir);
                    if (nextCell != null)
                    {
                        // If the start of the nextCell contains C1List items, set its ContentStart as the start of Selection.
                        var list = nextCell.ContentRange.Start.Element.Children.FirstOrDefault(el => el is C1List);
                        if (list == null)
                        {
                            Selection = nextCell.ContentRange;
                        }
                        else
                        {
                            Selection = new C1TextRange(list.ContentStart, nextCell.ContentRange.End);
                        }
                    }
                    else if (dir == LogicalDirection.Forward && !IsReadOnly)
                    {
                        using (new DocumentHistoryGroup(DocumentHistory))
                        {
                            Selection.InsertRowsBelow();
                            nextCell = FindNextCell(table, start, dir);
                            if (nextCell != null) Selection = nextCell.ContentRange;
                        }
                    }
                    e.Handled = true;

                    ScrollIntoView(Selection);
                }
                else if (!IsReadOnly)
                {
                    EditExtensions.ReplaceText(Selection, "\t");
                    e.Handled = true;

                    ScrollIntoView(Selection);
                }
            }
        }

        static C1TableCell FindNextCell(C1Table table, C1TableCell cell, LogicalDirection dir)
        {
            foreach (var el in cell.Enumerate(dir))
            {
                var parent = el.Parent;
                bool isInTable = true;
                while (parent != null && parent != table)
                {
                    isInTable = isInTable && (parent is C1TableRow || parent is C1TableRowGroup);
                    parent = parent.Parent;
                }
                if (parent != table) break;
                var nextCell = el as C1TableCell;
                if (isInTable && nextCell != null) return nextCell;
            }
            return null;
        }

        #endregion Specific key handlers

        C1TextPointer SelectionTail
        {
            get
            {
                return _selectionExtend == LogicalDirection.Forward ? Selection.End : Selection.Start;
            }
        }

        C1TextPointer LastInsertionPoint
        {
            get
            {
                return Document.ContentEnd.ClosestCaret;
            }
        }

        C1TextPointer FirstInsertionPoint
        {
            get
            {
                return Document.ContentStart.ClosestCaret;
            }
        }

        void InsertHyperlink()
        {
            if (AutoFormatHyperlinks && Selection.IsEmpty)
            {
                var position = Selection.Start;
                var run = position.Element as C1Run;
                if (run != null && run.GetClosestParent(e => e is C1Hyperlink) == null)
                {
                    var url = run.Text.Trim();
                    int lastSpaceIndex = url.LastIndexOf(' ') + 1;
                    if (lastSpaceIndex > 0)
                        url = url.Substring(lastSpaceIndex);
                    if (Regex.Match(url, HyperlinkFormat).Success)
                    {
                        var range = new C1TextRange(run.ContentStart.GetPositionAtOffset(lastSpaceIndex), position);
                        url = range.Text;
                        using (new DocumentHistoryGroup(DocumentHistory))
                        {
                            range.TrimRuns();
                            Uri uri = null;
                            try
                            {
                                uri = new Uri(url, UriKind.Absolute);
                            }
                            catch (UriFormatException)
                            {
                                try
                                {
                                    uri = new Uri("http://" + url, UriKind.Absolute);
                                }
                                catch { }
                            }
                            range.MakeHyperlink(uri);
                        }
                    }
                }
            }
        }

        void MoveSelection(C1TextPointer tail, bool extend)
        {
            if (DisableSelection)
            {
                return;
            }
            var oldSelection = Selection;
            // If the position is inside an emoji, move to the start or end of the emoji.
            int offset = _selectionExtend == LogicalDirection.Forward ? 1 : -1;
            if (tail.Symbol is char && ((char)tail.Symbol).IsLowSurrogate())
            {
                tail = tail.GetPositionAtOffset(offset);
            }
            if (!extend)
            {
                Selection = new C1TextRange(tail, tail);
            }
            else
            {
                var anchor = _selectionExtend == LogicalDirection.Forward ? Selection.Start : Selection.End;
                if (tail > anchor)
                {
                    Selection = new C1TextRange(anchor, tail);
                    _selectionExtend = LogicalDirection.Forward;
                }
                else
                {
                    Selection = new C1TextRange(tail, anchor);
                    _selectionExtend = LogicalDirection.Backward;
                }
            }

            //// remove empty old elements that may be left by the toolbar
            //var oldElement = oldSelection.Start.Element;
            //if (oldElement is C1Run && oldElement.ContentRange.IsEmpty && !oldElement.IsRoot)
            //{
            //    var nextCaret = oldElement.ContentEnd.GetClosestPointer(LogicalDirection.Forward, p => p.IsCaretPosition);
            //    var preCaret = oldElement.ContentStart.GetClosestPointer(LogicalDirection.Backward, p => p.IsCaretPosition);
            //    if ((nextCaret != null && nextCaret.DistanceTo(oldElement.ContentEnd) == 0) ||
            //        (preCaret != null && preCaret.DistanceTo(oldElement.ContentStart) == 0))
            //    {
            //        oldElement.Parent.Children.RemoveAt(oldElement.Index);
            //    }
            //}

            if (Selection.IsEmpty && Selection.Start.Element is C1Block
                && (!(Selection.Start.Element is C1TableRow) || Selection.Start.Element.Children.Where(e => e.IsEditable).Any()))
            {

                Selection = EditExtensions.InsertText(Selection.Start, string.Empty);
            }

            ScrollSelection();
        }

        void UpdateFontProperties()
        {
            BeginTextChanged();
            if (!Document.Style.ContainsKey(C1TextElement.FontFamilyProperty)
                && !C1TextElement.FontFamilyProperty.DefaultValue.Equals(FontFamily))
            {
                Document.FontFamily = FontFamily;
            }

            if (!Document.Style.ContainsKey(C1TextElement.FontSizeProperty)
                && !C1TextElement.FontSizeProperty.DefaultValue.Equals(FontSize))
            {
                Document.FontSize = FontSize;
            }

            if (!Document.Style.ContainsKey(C1TextElement.FontWeightProperty)
                && !C1TextElement.FontWeightProperty.DefaultValue.Equals(FontWeight))
            {
                Document.FontWeight = FontWeight;
            }

            if (!Document.Style.ContainsKey(C1TextElement.FontStyleProperty)
                && !C1TextElement.FontStyleProperty.DefaultValue.Equals(FontStyle))
            {
                Document.FontStyle = FontStyle;
            }

            if (IsUIInitialized)
            {
                // Don't update the Foreground before the constructor is complete.
                if (!Document.Style.ContainsKey(C1TextElement.ForegroundProperty)
                    && !C1TextElement.ForegroundProperty.DefaultValue.Equals(Foreground))
                {
                    //Document.Foreground = Foreground;
                    Document.Style[C1TextElement.ForegroundProperty] = Foreground;
                }
            }
            EndTextChanged();
        }

        private void UpdatePageNumber()
        {
            PageNumber = _viewManager != null ? _viewManager.MostVisiblePresenterIndex + 1 : 0;
        }

        private void UpdatePageCount()
        {
            PageCount = _viewManager != null ? _viewManager.Presenters.Count : 0;
        }

        void SetSelection(C1TextRange value, bool oldValueInvalid)
        {
            if (!oldValueInvalid && _selection == value)
            {
                return;
            }

            C1TextRange oldSelection = _selection;

            if (oldSelection != null)
            {
                oldSelection.Start.IsHyperlinkPosition = oldSelection.End.IsHyperlinkPosition = false;
                if (!(_resizePainter.Element is C1Table) && oldSelection.Table != null)
                {
                    oldSelection.Table.IsSelected = false;
                }
            }

            var start = value.Start.IsHyperlinkPosition ? value.Start : (value.Start.ClosestCaretInRange(Document) ?? value.Start);
            var end = value.End.IsHyperlinkPosition ? value.End : (value.End.ClosestCaretInRange(Document) ?? value.End);

            _selection = new C1TextRange(start, end);

            _selection.Start.InsertBefore = _selection.End.InsertBefore = true;

            if (_selection.Start > _selection.End)
            {
                _selection = new C1TextRange(_selection.Start, _selection.Start);
            }
            if (_txtBox != null)
            {
                _txtBox.Width = 0;
            }
            _selection.IsTableSelected = value.IsTableSelected;
            _selection.SelectedColumnStartIndex = value.SelectedColumnStartIndex;
            _selection.SelectedColumnEndIndex = value.SelectedColumnEndIndex;
            _selection.IsRowsSelected = value.IsRowsSelected;
            _selectionPainter.Selection = Selection;
            _editRanges = _selection.ExactEditRanges;
            if (_viewManager != null && SelectionForeground != null)
            {
                if (oldValueInvalid || oldSelection == null || oldSelection.Start.Element.Root != Selection.Start.Element.Root)
                {
                    _viewManager.InvalidateLayout(Selection);
                }
                else
                {
                    foreach (var range in Algorithms.SymmetricDifference(oldSelection.ExactEditRanges, _editRanges))
                    {
                        _viewManager.InvalidateLayout(range);
                    }
                }
            }
            else
            {
                Redraw();
            }
            PaintResizer();
            if (SelectionChanged != null)
            {
                SelectionChanged(this, EventArgs.Empty);
            }
        }

        void PaintResizer()
        {
            C1TextElement element = null;
            if (!IsReadOnly && IsEnabled)
            {
                var ui = Selection.ContainedElements.FirstOrDefault(el => el is C1InlineUIContainer || el is C1BlockUIContainer);
                if (ui != null
                    && (Selection.Start.DistanceTo(ui.ContentStart) == 0 || new C1TextRange(Selection.Start, ui.ContentStart).Runs.IsEmpty() || object.Equals(Selection.Start.Symbol, '\n'))
                    && (ui.ContentEnd.DistanceTo(Selection.End) == 0 || new C1TextRange(ui.ContentEnd, Selection.End).Runs.IsEmpty()))
                {
                    element = ui;
                }

                C1Table table = null;
                if (ui == null && Selection.Start.DistanceTo(Selection.End) == 0 && _isTableResized)
                    table = Selection.Start.Element.GetClosestParent(el => el is C1Table) as C1Table;
                if (table != null)
                {
                    element = table;
                    _isTableResized = false;
                }
            }

            if (_resizePainter.Element != element)
            {
                _selectionPainter.Background = element != null ? null : SelectionBackground;
                var table = _resizePainter.Element as C1Table;
                if (table != null && element == null)
                {
                    table.IsSelected = false;
                }
                _resizePainter.Element = element;
                Redraw();
            }

            var tab = _resizePainter.Element as C1Table;
            if (tab != null)
            {
                tab.IsSelected = true;
            }

        }

        Point GetPosition(
#if WINRT
            PointerRoutedEventArgs
#else
            MouseEventArgs
#endif
            mouseEvent)
        {
            return _viewManager.
#if WINRT
                GetPointFromPointerEvent
#else
                GetPointFromMouseEvent
#endif
                (this, mouseEvent);
        }

        Point GetPosition(TouchEventArgs touchEventArgs)
        {
            return _viewManager.GetPointFromTouchEvent(this, touchEventArgs);
        }

        /// <summary>
        /// Before TextChanged was fired.
        /// </summary>
        internal void BeginTextChanged()
        {
            _isIgnoreTextChanged = true;
            _c1TextChangedEventArgs = null;
            _textChangedTimes = 0;
        }
        /// <summary>
        /// TextChanged event was called.
        /// </summary>
        internal void EndTextChanged()
        {
            _isIgnoreTextChanged = false;
            if (_textChangedTimes > 1)
            {
                // If TextChanged event was fired for multiple times, we should call OnTextChanged just once as Reset action.
                OnTextChanged(new C1TextChangedEventArgs(Document, TextChangedAction.Reset, _c1TextChangedEventArgs == null ? Selection : _c1TextChangedEventArgs.Range, null, null, null));
            }
            else
            {
                // If TextChanged event was fired only once, we should call OnTextChanged as previously setted action.
                if (_c1TextChangedEventArgs != null)
                {
                    OnTextChanged(_c1TextChangedEventArgs);
                    _c1TextChangedEventArgs = null;
                    _textChangedTimes = 0;
                }
            }
        }

        /// <summary>
        /// Set watermark to the element.
        /// </summary>
        /// <param name="needsShowWatermark">Indicates whether need set watermark to the element</param>
        internal void SetWatermark(bool needsShowWatermark)
        {
            if (_elementPlaceholder != null)
            {
                _elementPlaceholder.SetValue(Canvas.ZIndexProperty, 1000);
                if (needsShowWatermark && Placeholder != null && Placeholder.Trim() != "" && Text == ""
                    && Document.ContentRange.Tables.IsEmpty() && Document.ContentRange.Lists.IsEmpty())
                {
                    _elementPlaceholder.Visibility = Visibility.Visible;

                }
                else
                {
                    _elementPlaceholder.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Get the correct end C1TextPointer after the start point with LogicalDirection.
        /// </summary>
        /// <param name="start">The start C1TextPointer</param>
        /// <param name="dir">The logical direction in which to perform certain text</param>
        /// <returns>The correct end C1TextPointer after the start point with LogicalDirection.</returns>
        internal static C1TextPointer GetSpecialLanguageStartPoint(C1TextPointer start, LogicalDirection dir)
        {
            if (start == null)
            {
                return start;
            }

            var pos = start;

            if (pos.Offset != 0)
            {
                for (; ; )
                {
                    if (pos.Symbol is char)
                    {
                        char c = (char)pos.Symbol;
                        if (TextHelper.IsThaiCharacter(c))
                        {
                            // For Thai Language
                            if (TextHelper.IsThaiAlphabetical(c) || TextHelper.IsThaiLeadingVowel(c))
                            {
                                break;
                            }
                            else
                            {
                                // If the char is not base level Thai character, we need continue to get the forward/backward C1TextPointer.
                                pos = pos.EnumerateCarets(dir).FirstOrDefault(WordStartIfControl);
                            }
                        }
                        else if (TextHelper.IsNiqqudCharacter(c))
                        {
                            // For Hebrew Language
                            // If the char is a Hebrew vowel, we need continue to get the forward/backward C1TextPointer.
                            var next = pos.EnumerateCarets(dir).FirstOrDefault(WordStartIfControl);
                            if (next != null)
                                pos = next;
                            else
                                break;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return pos;
        }

        #endregion

        #region ISupportInitialize Members

#pragma warning disable 1591
        public override void EndInit()
        {
            base.EndInit();
            // The FontFamily binding isn't updated when setting in XAML
            // we use ISupportInitialize to correctly initialize font
            // properties set in XAML.
            UpdateFontProperties();
        }
#pragma warning restore 1591

        #endregion

        void UpdateDependecyProperty(DependencyProperty property)
        {
            if (ReadLocalValue(property) is BindingExpressionBase)
            {
                var binding = ((BindingExpression)(ReadLocalValue(property))).ParentBinding;
                if (binding.UpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
                {
                    CalculatedDependencyProperty.Update(this, property, true);
                }
            }

        }

#pragma warning disable CS1591
        protected override void OnDragEnter(DragEventArgs e)
        {
            e.Handled = true;
            if (!IsEnabled)
            {
                e.Effects = DragDropEffects.None;
            }
            else if (e.Data == null)
            {
                e.Effects = DragDropEffects.None;
            }

            var formats = (e.Data as DataObject).GetFormats();
            if (!formats.Contains(DataFormats.Html)
                && !formats.Contains(DataFormats.Text)
                && !formats.Contains(DataFormats.UnicodeText)
                && !formats.Contains(DataFormats.Rtf))
            {
                e.Effects = DragDropEffects.None;
            }

            if (ValidateEffects(e) && _dragTargetPainter == null)
            {
                _dragTargetPainter = new PlaceholderPainter();
                Painters.Add(_dragTargetPainter);
            }
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            e.Handled = true;
            if (!IsEnabled)
            {
                e.Effects = DragDropEffects.None;
            }

            if (ValidateEffects(e) && _dragTargetPainter != null)
            {
                RemovePlacePainter();
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            e.Handled = true;
            if (!IsEnabled)
            {
                e.Effects = DragDropEffects.None;
            }
            else if (e.Data == null)
            {
                e.Effects = DragDropEffects.None;
            }
            var formats = (e.Data as DataObject).GetFormats();
            if (!formats.Contains(DataFormats.Html)
                && !formats.Contains(DataFormats.Text)
                && !formats.Contains(DataFormats.UnicodeText)
                && !formats.Contains(DataFormats.Rtf))
            {
                e.Effects = DragDropEffects.None;
            }

            if (ValidateEffects(e) && _dragTargetPainter != null)
            {
                // Change the position of drag thumb.
                var current = GetPositionFromPoint(e.GetPosition(this));
                var args = new TextDragMoveEventArgs { Position = current };
                OnTextDragMove(args);
                _dragTargetPainter.Position = args.Position.ClosestCaret;
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Handled)
                return;

            if (IsEnabled && !IsReadOnly)
            {
                if ((e.Data == null) || (e.AllowedEffects == DragDropEffects.None))
                {
                    e.Effects = DragDropEffects.None;
                }
                else
                {
                    if ((e.KeyStates & DragDropKeyStates.ControlKey) != DragDropKeyStates.None)
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                    else if (e.Effects != DragDropEffects.Copy)
                    {
                        e.Effects = DragDropEffects.Move;
                    }

                    var current = GetPositionFromPoint(e.GetPosition(this));
                    if (current != null)
                    {
                        if (!Selection.IsEmpty && Selection.Contains(current))
                        {
                            MoveSelection(current, false);
                            e.Effects = DragDropEffects.None;
                            RemovePlacePainter();
                            e.Handled = true;
                        }
                        else if (e.Effects != DragDropEffects.None)
                        {
                            MoveSelection(current, false);
                            C1Document doc = null;
                            var data = e.Data.GetData(DataFormats.Html);
                            if (data == null)
                            {
                                data = e.Data.GetData(DataFormats.Rtf);
                                if (data == null)
                                {
                                    data = e.Data.GetData(DataFormats.Text);
                                    if (data != null)
                                        InputClipboardText(data.ToString());
                                }
                                else
                                {
                                    doc = new RtfFilter().ConvertToDocument(data.ToString());
                                }
                            }
                            else
                            {
                                var html = Clipboard.GetHtmlData(data.ToString(), true);
                                doc = HtmlFilter.ConvertToDocument(html);
                            }

                            if (doc != null)
                            {
                                SetDocument(doc);
                            }
                            e.Handled = true;
                        }
                        if (e.Handled)
                        {
                            Focus();
                        }
                        else
                        {
                            e.Effects = DragDropEffects.None;
                        }
                    }
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            if (ValidateEffects(e) && _dragTargetPainter != null)
            {
                RemovePlacePainter();
            }
        }

        protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
        {
            if (IsEnabled)
            {
                e.UseDefaultCursors = true;
                e.Handled = true;
            }
        }

        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            if (IsEnabled)
            {
                e.Handled = true;
                e.Action = DragAction.Continue;
                bool flag = (e.KeyStates & DragDropKeyStates.LeftMouseButton) == DragDropKeyStates.None;
                if (e.EscapePressed)
                {
                    e.Action = DragAction.Cancel;
                    if (_dragTargetPainter != null)
                        RemovePlacePainter();
                }
                else if (flag)
                {
                    e.Action = DragAction.Drop;
                }
            }
        }
#pragma warning restore CS1591

        bool ValidateEffects(DragEventArgs e)
        {
            if (e.Effects == DragDropEffects.Move)
                return true;
            else if (e.Effects == DragDropEffects.Copy)
                return true;
            else if (e.Effects == (DragDropEffects.Copy | DragDropEffects.Move))
                return true;
            else
                return false;
        }

        void RemovePlacePainter()
        {
            _dragTargetPainter.Position = null;
            Painters.Remove(_dragTargetPainter);
            _dragTargetPainter = null;
        }

        #region ** Scrolling
        ScrollPresenter _scrollPresenter;
        const double mouseWheelScrollDelta = 15.0 / 120.0;

        void InitializeScroll()
        {
            if (_elementHorizontalScrollBar != null)
            {
                _elementHorizontalScrollBar.IsTabStop = false;
            }
            if (_elementVerticalScrollBar != null)
            {
                _elementVerticalScrollBar.IsTabStop = false;
            }
            _scrollPresenter = new ScrollPresenter();

            _scrollPresenter.LayoutUpdated += (s, e) =>
            {
                UpdatePageNumber();
                // Update the layout when the FlowDirection is changed.
                if (LastFlowDirection != FlowDirection)
                {
                    UpdateSelectionVisibility();
                }
#if WINRT
                _elementContainer.ManipulationMode = ManipulationModes.System;

                // If the size of content is bigger that the size of viewport,
                // then we can scroll inside the content.
                bool needsInertia = false;
                if (ExtentHeight >= ViewportHeight || _resizePainter.Element != null)
                {
                    _elementContainer.ManipulationMode |= ManipulationModes.TranslateY;
                    if (ExtentHeight >= ViewportHeight)
                        needsInertia = true;
                }
                if (ExtentWidth >= ViewportWidth || ViewMode == TextViewMode.Print)
                {
                    _elementContainer.ManipulationMode |= ManipulationModes.TranslateX;
                    if (ExtentWidth >= ViewportWidth)
                        needsInertia = true;
                }
                if (needsInertia)
                    _elementContainer.ManipulationMode |= ManipulationModes.TranslateInertia;
#endif
            };

            ScrollPresenterBind(ScrollPresenter.ViewportWidthProperty, "ViewportWidth");
            ScrollPresenterBind(ScrollPresenter.ViewportHeightProperty, "ViewportHeight");
            ScrollPresenterBind(ScrollPresenter.TextWrappingProperty, "TextWrapping");
            ScrollPresenterBind(ScrollPresenter.HorizontalScrollBarVisibilityProperty, "HorizontalScrollBarVisibility");
            ScrollPresenterBind(ScrollPresenter.VerticalScrollBarVisibilityProperty, "VerticalScrollBarVisibility");
            ScrollPresenterBind(ScrollPresenter.HorizontalOffsetProperty, "HorizontalOffset");
            ScrollPresenterBind(ScrollPresenter.VerticalOffsetProperty, "VerticalOffset");
            ScrollPresenterBind(ScrollPresenter.ComputedHorizontalScrollBarVisibilityProperty, "ComputedHorizontalScrollBarVisibility");
            ScrollPresenterBind(ScrollPresenter.ComputedVerticalScrollBarVisibilityProperty, "ComputedVerticalScrollBarVisibility");
            ScrollPresenterBind(ScrollPresenter.ExtentWidthProperty, "ExtentWidth");
            ScrollPresenterBind(ScrollPresenter.ExtentHeightProperty, "ExtentHeight");
            ScrollPresenterBind(ScrollPresenter.ScrollableWidthProperty, "ScrollableWidth");
            ScrollPresenterBind(ScrollPresenter.ScrollableHeightProperty, "ScrollableHeight");
            ScrollBarBind(ScrollBar.VisibilityProperty, "Computed{0}ScrollBarVisibility");
            ScrollBarBind(ScrollBar.ValueProperty, "{0}Offset");
            ScrollBarBind(ScrollBar.ViewportSizeProperty, "Viewport{1}");
            ScrollBarBind(ScrollBar.LargeChangeProperty, "Viewport{1}");
            ScrollBarBind(ScrollBar.MaximumProperty, "Scrollable{1}");
        }

        void ScrollPresenterBind(DependencyProperty property, string propertyName)
        {
#if WINRT
            _scrollPresenter.SetBinding(property, new Binding() { Path = new PropertyPath(propertyName), Source = this, Mode = BindingMode.TwoWay });
#else
            _scrollPresenter.SetBinding(property, new Binding(propertyName) { Source = this, Mode = BindingMode.TwoWay });
#endif
        }

        void ScrollBarBind(DependencyProperty property, string propertyName)
        {
            if (_elementHorizontalScrollBar != null)
            {
                // Fixed the issue that "LayoutCycle" exception is observed when numerous(>130) C1RichTextBox controls are added at runtime.(TFS-51019)
                // The issue is just relative with _elementHorizontalScrollBar.
                DispatcherEx.BeginInvoke(Dispatcher, () =>
                {
                    _elementHorizontalScrollBar.SetBinding(property,
#if WINRT
                    new Binding() { Path = new PropertyPath(string.Format(propertyName, "Horizontal", "Width")), Source = this, Mode = BindingMode.TwoWay });
#else
                    new Binding(string.Format(propertyName, "Horizontal", "Width")) { Source = this, Mode = BindingMode.TwoWay });
#endif
                });
            }
            if (_elementVerticalScrollBar != null)
            {
                _elementVerticalScrollBar.SetBinding(property,
#if WINRT
                    new Binding() { Path = new PropertyPath(string.Format(propertyName, "Vertical", "Height")), Source = this, Mode = BindingMode.TwoWay });
#else
                    new Binding(string.Format(propertyName, "Vertical", "Height")) { Source = this, Mode = BindingMode.TwoWay });
#endif
            }
        }

        /// <summary>
        /// Scrolls the <see cref="C1RichTextBox"/> to bring a <see cref="C1TextElement"/> into view.
        /// </summary>
        /// <param name="element"></param>
        public void ScrollIntoView(C1TextElement element)
        {
            ScrollIntoView(element.ContentEnd);
            ScrollIntoView(element.ContentStart);
        }

        internal void ScrollIntoView(C1TextRange range)
        {
            ScrollIntoView(range.End);
            ScrollIntoView(range.Start);
        }

        /// <summary>
        /// Scrolls the <see cref="C1RichTextBox"/> to bring a <see cref="C1TextPointer"/> into view.
        /// </summary>
        /// <param name="position">The position to bring into view.</param>
        public void ScrollIntoView(C1TextPointer position)
        {
            if (_viewManager != null && _scrollPresenter != null)
            {
                //         _scrollPresenter.UpdateLayout(); // <<IP>> it is very bad for performance (C1XAML-19502)
                var rect = _viewManager.GetRectFromPosition(position);
                bool scrollH = true;
                bool scrollV = true;

                if (IsInput)
                {
                    var run = position.Element as C1Run;
                    if (run != null)
                    {
                        var textBlock = TextHelper.CreateTextBlock("a", run.FontFamily, run.FontWeight, run.FontStyle, run.FontSize, LineStackingStrategy.BlockLineHeight, null);
                        double height = 0.0;
#if WPF || WINRT
                        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        height = textBlock.DesiredSize.Width;
#else
                        height = textBlock.ActualHeight;
#endif
                        var top = rect.Bottom - height - VerticalOffset;
                        var bottom = rect.Bottom - VerticalOffset;
                        var left = rect.Left - HorizontalOffset;
                        var right = rect.Right - HorizontalOffset;

                        if (top >= 0 && bottom <= ViewportHeight)
                        {
                            scrollV = false;
                        }

                        if (left > 0 && right <= ViewportWidth)
                        {
                            scrollH = false;
                        }

                        if (!scrollH && !scrollV)
                        {
                            this.UpateParentScrollViewer(rect);
                            return;
                        }
                    }
                }

                var point = _viewManager.GetRelativePointFromPoint(_scrollPresenter, new Point(rect.X, rect.Y));

                if (scrollH)
                {
                    rect.X = point.X + HorizontalOffset;
                }

                if (scrollV)
                {
                    rect.Y = point.Y + VerticalOffset;
                }

                if (IsInput)
                {
                    if (rect.Y == 0.0 && rect.Bottom > ViewportHeight)
                    {
                        rect.Y = rect.Bottom - ViewportHeight;
                    }
                }
#if WINRT
                // forbid the WindowsPhone keyboard to hide the cursor
                // _txtBox.Margin = new Thickness(rect.X, rect.Y, 0, 0);
#endif
                _scrollPresenter.ScrollIntoView(rect);

                if (_scrollPresenter.VerticalOffset == 0)
                {
                    this.UpateParentScrollViewer(rect);
                }
            }
        }

        private void UpateParentScrollViewer(Rect rect)
        {
            var sv = this.GetParents().OfType<ScrollViewer>().FirstOrDefault();

            if (sv != null)
            {
                var p = _viewManager.GetRelativePointFromPoint(sv, new Point(rect.X, rect.Y));
                rect.X = p.X + sv.HorizontalOffset;
                rect.Y = p.Y + sv.VerticalOffset - sv.Padding.Top;

                if (rect.Y == 0.0 && rect.Bottom > sv.ViewportHeight)
                {
                    rect.Y = rect.Bottom - sv.ViewportHeight;
                }

                double vo = sv.VerticalOffset;
                if (vo > rect.Top
#if WINRT
                    + _keyboardHeight
#endif
                    )
                {
                    vo = rect.Top;
                }
                else if (vo < rect.Bottom - sv.ViewportHeight && sv.ViewportHeight > 0)
                {
                    vo = rect.Bottom - sv.ViewportHeight;
                }
                else
                {
                    return;
                }

                sv.ScrollToVerticalOffset(vo);
            }
        }

        /// <summary>
        /// Scrolls the <see cref="C1RichTextBox"/> so that the given position is at the top.
        /// </summary>
        /// <param name="position">A <see cref="C1TextPointer"/> to scroll to.</param>
        public void ScrollTo(C1TextPointer position)
        {
            if (_viewManager != null && _scrollPresenter != null)
            {
                var rect = _viewManager.GetRectFromPosition(position);
                var point = _viewManager.GetRelativePointFromPoint(_scrollPresenter, new Point(rect.X, rect.Y));
                VerticalOffset = point.Y + VerticalOffset;
            }
        }
        void ScrollSelection()
        {
            if (_viewManager != null)
            {
                var rect = _viewManager.GetRectFromPosition(SelectionTail);
                ScrollIntoView(SelectionTail);
                _floatingTail = new Point(rect.X, rect.Y + rect.Height / 2 - VerticalOffset);
            }
        }

        partial void OnHorizontalOffsetChanged()
        {
            HorizontalOffset = Math.Min(Math.Max(HorizontalOffset, 0), _scrollPresenter != null ? _scrollPresenter.ScrollableWidth : 0);
        }

        partial void OnVerticalOffsetChanged()
        {
            VerticalOffset = Math.Min(Math.Max(VerticalOffset, 0), _scrollPresenter != null ? _scrollPresenter.ScrollableHeight : 0);
        }

        void OnScrollDelta(object sender, C1ScrollDeltaEventArgs e)
        {
            if (ScrollableHeight > 0)
            {
                VerticalOffset -= e.DeltaScroll * mouseWheelScrollDelta;
                e.Handled = true;
            }
        }

        internal ScrollBar VerticalScrollBar
        {
            get
            {
                return _elementVerticalScrollBar;
            }
        }
        #endregion
    }

    /// <summary>
    /// Provides data for the <see cref="C1RichTextBox.TextDragMove"/> event.
    /// </summary>
    public class TextDragMoveEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the position where a placeholder will be shown. If set to null no placeholder is shown.
        /// </summary>
        public C1TextPointer Position { get; set; }
    }

    /// <summary>
    /// Provides data for the <see cref="C1RichTextBox.TextDrop"/> event.
    /// </summary>
    public class TextDropEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the position where text will be dropped.
        /// </summary>
        public C1TextPointer Position { get; private set; }
        /// <summary>
        /// Gets or sets whether text drop has already be handled. If set to true, <see cref="C1RichTextBox"/> does nothing on text drop.
        /// </summary>
        public bool Handled { get; set; }

        internal TextDropEventArgs(C1TextPointer position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="C1RichTextBox.RequestNavigate"/> event. 
    /// </summary>
    public class RequestNavigateEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new RequestNavigateEventArgs.
        /// </summary>
        /// <param name="hyperlink"></param>
        public RequestNavigateEventArgs(C1Hyperlink hyperlink)
        {
            Hyperlink = hyperlink;
        }
        /// <summary>
        /// Gets the <see cref="C1Hyperlink"/> that triggered the event.
        /// </summary>
        public C1Hyperlink Hyperlink { get; private set; }
    }

    /// <summary>
    /// Defines the mode in which <see cref="C1RichTextBox"/> fires <see cref="C1RichTextBox.RequestNavigate"/> events.
    /// </summary>
    public enum NavigationMode
    {
        /// <summary>
        /// The event is never fired.
        /// </summary>
        Never,
        /// <summary>
        /// The event is fired when the user clicks/taps on a <see cref="C1Hyperlink"/>. 
        /// In the case of Silverlight, a hand cursor is shown when the mouse hovers 
        /// the <see cref="C1Hyperlink"/>.
        /// </summary>
        Always,
        /// <summary>
        /// The event is fired when the user clicks on a <see cref="C1Hyperlink"/> and the control key is pressed. 
        /// A hand cursor is shown when the mouse hovers a C1Hyperlink and the control key is pressed.
        /// </summary>
        OnControlKey
    }

    /// <summary>
    /// Defines the way in which <see cref="C1RichTextBox"/> handles the return key.
    /// </summary>
    public enum ReturnMode
    {
        /// <summary>
        /// The Return key inserts soft line breaks if shift is held, and hard line breaks otherwise.
        /// </summary>
        Default,
        /// <summary>
        /// The Return key inserts hard line breaks.
        /// </summary>
        HardLineBreak,
        /// <summary>
        /// The Return key inserts soft line breaks.
        /// </summary>
        SoftLineBreak,
    }

    /// <summary>
    /// Defines the mode in which the <see cref="C1RichTextBox"/> uses the Clipboard.
    /// </summary>
    public enum ClipboardMode
    {
        /// <summary>
        /// Use plain text.
        /// </summary>
        PlainText,
        /// <summary>
        /// Use rich text (subject to browser support).
        /// </summary>
        RichText,
    }

    /// <summary>
    /// Defines the mode in which <see cref="C1RichTextBox"/> presents its content.
    /// </summary>
    public enum TextViewMode
    {
        /// <summary>
        /// Text is displayed in one continuous, scrollable presenter. This is the default mode.
        /// </summary>
        Draft,
        /// <summary>
        /// Text is displayed separated in pages.
        /// </summary>
        Print,
    }

    /// <summary>
    /// Defines the line numbering mode.
    /// </summary>
    public enum TextLineNumberMode
    {
        /// <summary>
        /// The line numbering is disabled.
        /// </summary>
        None,
        /// <summary>
        /// The line numbering starts with number 1 on each page.
        /// </summary>
        RestartEachPage,
        /// <summary>
        /// The line number is continuous throughout the document.
        /// </summary>
        Continuous,
    }

    /// <summary>
    /// Defines the mode for automatic capitalization.
    /// </summary>
    public enum AutoCapitalizationMode
    {
        /// <summary>
        /// Specifies that there is no automatic text capitalization. This is the default type.
        /// </summary>
        None,
        /// <summary>
        /// Specifies automatic capitalization of the first letter of each sentence.
        /// </summary>
        Sentence,
    }

    /// <summary>
    /// This Enumeration describes zoom options that determine how pages are displayed in the viewport.
    /// </summary>
    internal enum PageViewMode
    {
        /// <summary>
        /// Display pages using the current zoom value.
        /// </summary>
        Normal,
        /// <summary>
        /// Automatically update the zoom value to fit one entire page inside the viewport.
        /// </summary>
        OnePage,
        /// <summary>
        /// Automatically update the zoom value to fit the width of one page inside the viewport.
        /// </summary>
        FitWidth,
        /// <summary>
        /// Automatically update the zoom value to fit two pages inside the viewport.
        /// </summary>
        TwoPages,
    }

    class TextBoxWithoutKeyboardShortcuts : TextBox
    {
        public TextBoxWithoutKeyboardShortcuts()
        {
            Style = null;
            ContextMenu = null;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.X || e.Key == Key.C || e.Key == Key.V || e.Key == Key.Z || e.Key == Key.Y || e.Key == Key.Insert)) ||
            (Keyboard.Modifiers == ModifierKeys.Shift && (e.Key == Key.Delete || e.Key == Key.Insert)) || (Keyboard.Modifiers == ModifierKeys.Windows && (e.Key == Key.Z)) || (Keyboard.Modifiers == (ModifierKeys.Windows | ModifierKeys.Shift) && (e.Key == Key.Z)))
            {
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

    }

    /// <summary>
    /// Specifies the options to use when doing a text search.
    /// </summary>
    public enum C1FindOptions
    {
        /// <summary>
        /// Use the default text search options; namely, use case- independent, arbitrary character boundaries.
        /// </summary>
        None = 0,
        /// <summary>
        /// Match case; that is, a case-sensitive search.
        /// </summary>
        Case = 4,
    }
}
